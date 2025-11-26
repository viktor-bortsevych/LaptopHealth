using LaptopHealth.Services.Interfaces;
using NAudio.Wave;

namespace LaptopHealth.Services.Hardware
{
    /// <summary>
    /// Hardware-level audio service using NAudio
    /// Handles microphone capture and frequency analysis using FFT
    /// </summary>
    public class AudioNAudioService : IAudioHardwareService, IDisposable
    {
        private WaveInEvent? _waveIn;
        private readonly float[] _buffer;
        private int _bufferIndex;
        private readonly float[] _frequencyBands;
        private readonly float[] _smoothedBands;
        private readonly int[] _binMap;
        private string? _selectedDevice;
        private bool _isCapturing;
        private readonly Lock _lockObject = new();
        private readonly ILogger _logger;
        private bool _disposed;

        // Pre-computed values for FFT optimization
        private readonly float[] _hammingWindow;
        private readonly Complex[] _fftBuffer;

        // Snapshot buffer for FFT to avoid race conditions
        private readonly float[] _fftInputBuffer;
        private volatile bool _bufferNeedsAnalysis;

        private const int SAMPLE_RATE = 44100;
        private const int BUFFER_SIZE = 1024;
        private const int FREQUENCY_BANDS = 32;
        private const float SMOOTHING_FACTOR = 0.7f;

        public bool IsCapturing => _isCapturing;

        public AudioNAudioService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _buffer = new float[BUFFER_SIZE];
            _frequencyBands = new float[FREQUENCY_BANDS];
            _smoothedBands = new float[FREQUENCY_BANDS];
            _fftBuffer = new Complex[BUFFER_SIZE];
            _hammingWindow = new float[BUFFER_SIZE];
            _fftInputBuffer = new float[BUFFER_SIZE];
            _binMap = new int[FREQUENCY_BANDS + 1];

            InitializeHammingWindow();
            InitializeBinMap();
        }

        /// <summary>
        /// Pre-computes Hamming window coefficients for FFT
        /// </summary>
        private void InitializeHammingWindow()
        {
            for (int n = 0; n < BUFFER_SIZE; n++)
            {
                _hammingWindow[n] = 0.54f - 0.46f * (float)Math.Cos(2 * Math.PI * n / (BUFFER_SIZE - 1));
            }
        }

        /// <summary>
        /// Initializes the frequency bin mapping for FFT analysis
        /// </summary>
        private void InitializeBinMap()
        {
            for (int i = 0; i <= FREQUENCY_BANDS; i++)
            {
                // Logarithmic frequency mapping from 20Hz to Nyquist frequency
                float freq = 20 * (float)Math.Pow(SAMPLE_RATE / 2.0 / 20.0, i / (float)FREQUENCY_BANDS);
                _binMap[i] = Math.Min((int)(freq * BUFFER_SIZE / SAMPLE_RATE), BUFFER_SIZE / 2 - 1);
            }
        }

        public IEnumerable<string> GetAvailableDevices()
        {
            try
            {
                var devices = new List<string>();

                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (capabilities.Channels > 0)
                    {
                        devices.Add($"{capabilities.ProductName} (Device {i})");
                    }
                }

                _logger.Info($"Found {devices.Count} microphone device(s)");
                return devices;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error retrieving audio devices: {ex.Message}");
                return [];
            }
        }

        public async Task<bool> InitializeDeviceAsync(string deviceName, CancellationToken cancellationToken)
        {
            try
            {
                // Stop current capture if running
                if (_isCapturing)
                {
                    StopCapture();
                }

                // Find device index from name
                int deviceIndex = GetDeviceIndexFromName(deviceName);
                if (deviceIndex < 0)
                {
                    _logger.Warn($"Device not found: {deviceName}");
                    return false;
                }

                _selectedDevice = deviceName;
                _logger.Info($"Initialized audio device: {deviceName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error initializing audio device: {ex.Message}");
                return false;
            }
        }

        public bool StartCapture()
        {
            if (_selectedDevice == null)
            {
                _logger.Warn("No audio device selected");
                return false;
            }

            if (_isCapturing)
            {
                _logger.Info("Audio capture already running");
                return true;
            }

            try
            {
                int deviceIndex = GetDeviceIndexFromName(_selectedDevice);
                if (deviceIndex < 0)
                {
                    _logger.Error($"Device not found: {_selectedDevice}");
                    return false;
                }

                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(SAMPLE_RATE, 1)
                };

                _waveIn.DataAvailable += ProcessAudioData;
                _waveIn.StartRecording();
                _isCapturing = true;

                lock (_lockObject)
                {
                    _bufferIndex = 0;
                    _bufferNeedsAnalysis = false;
                }

                _logger.Info("Audio capture started");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error starting audio capture: {ex.Message}");
                _isCapturing = false;
                _waveIn?.Dispose();
                _waveIn = null;
                return false;
            }
        }

        public bool StopCapture()
        {
            try
            {
                _isCapturing = false;

                if (_waveIn != null)
                {
                    _waveIn.DataAvailable -= ProcessAudioData;
                    _waveIn.StopRecording();
                    _waveIn.Dispose();
                    _waveIn = null;
                }

                lock (_lockObject)
                {
                    Array.Clear(_frequencyBands, 0, _frequencyBands.Length);
                    Array.Clear(_smoothedBands, 0, _smoothedBands.Length);
                    _bufferNeedsAnalysis = false;
                }

                _logger.Info("Audio capture stopped");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping audio capture: {ex.Message}");
                return false;
            }
        }

        public float[]? GetFrequencyData()
        {
            if (!_isCapturing)
            {
                return null;
            }

            // Perform analysis if new data is available
            if (_bufferNeedsAnalysis)
            {
                AnalyzeFrequencies();
            }

            lock (_lockObject)
            {
                return (float[])_smoothedBands.Clone();
            }
        }

        /// <summary>
        /// Processes incoming audio data (runs on background thread)
        /// </summary>
        private void ProcessAudioData(object? sender, WaveInEventArgs e)
        {
            if (!_isCapturing)
                return;

            try
            {
                // Write to circular buffer without locking for performance
                for (int i = 0; i < e.BytesRecorded / 2; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    int currentIndex = _bufferIndex;
                    _buffer[currentIndex] = sample / 32768f;

                    // Update index atomically
                    int nextIndex = (currentIndex + 1) % BUFFER_SIZE;
                    _bufferIndex = nextIndex;

                    // Signal analysis needed when we have a full buffer
                    if (nextIndex % (BUFFER_SIZE / 2) == 0)
                    {
                        _bufferNeedsAnalysis = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing audio data: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzes audio frequencies using FFT (thread-safe snapshot approach)
        /// </summary>
        private void AnalyzeFrequencies()
        {
            lock (_lockObject)
            {
                if (!_bufferNeedsAnalysis)
                    return;

                _bufferNeedsAnalysis = false;

                // Create snapshot of current buffer state
                int snapshotIndex = _bufferIndex;
                for (int n = 0; n < BUFFER_SIZE; n++)
                {
                    int idx = ((snapshotIndex - BUFFER_SIZE + n) % BUFFER_SIZE + BUFFER_SIZE) % BUFFER_SIZE;
                    _fftInputBuffer[n] = _buffer[idx];
                }

                // Compute FFT magnitudes from snapshot
                float[] magnitudes = ComputeFFT(_fftInputBuffer);

                // Extract frequency bands using logarithmic mapping
                for (int band = 0; band < FREQUENCY_BANDS; band++)
                {
                    int startBin = _binMap[band];
                    int endBin = _binMap[band + 1];

                    // Ensure valid range
                    startBin = Math.Max(0, Math.Min(startBin, magnitudes.Length - 1));
                    endBin = Math.Max(startBin + 1, Math.Min(endBin, magnitudes.Length));

                    float sum = 0;
                    for (int k = startBin; k < endBin; k++)
                    {
                        sum += magnitudes[k];
                    }

                    float avg = sum / (endBin - startBin);
                    float db = 20 * (float)Math.Log10(Math.Max(avg, 0.00001f));
                    _frequencyBands[band] = Math.Clamp((db + 80) / 8, 0, 10);

                    // Apply exponential smoothing
                    _smoothedBands[band] = _smoothedBands[band] * SMOOTHING_FACTOR + _frequencyBands[band] * (1 - SMOOTHING_FACTOR);
                }
            }
        }

        /// <summary>
        /// Computes FFT magnitudes
        /// </summary>
        private float[] ComputeFFT(float[] inputBuffer)
        {
            // Prepare FFT input with windowing
            for (int n = 0; n < BUFFER_SIZE; n++)
            {
                _fftBuffer[n] = new Complex(inputBuffer[n] * _hammingWindow[n], 0);
            }

            // Perform in-place FFT
            FFTCooleyTukey(_fftBuffer);

            // Compute magnitudes
            float[] magnitudes = new float[BUFFER_SIZE / 2];
            for (int k = 0; k < BUFFER_SIZE / 2; k++)
            {
                magnitudes[k] = _fftBuffer[k].Magnitude / BUFFER_SIZE;
            }

            return magnitudes;
        }

        /// <summary>
        /// Cooley-Tukey FFT algorithm (in-place, iterative, radix-2)
        /// Complexity: O(n log n)
        /// </summary>
        private static void FFTCooleyTukey(Complex[] buffer)
        {
            int n = buffer.Length;

            // Bit-reversal permutation
            int bits = (int)Math.Log2(n);
            for (int i = 0; i < n; i++)
            {
                int j = ReverseBits(i, bits);
                if (j > i)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            // Cooley-Tukey decimation-in-time radix-2 FFT
            for (int size = 2; size <= n; size *= 2)
            {
                int halfSize = size / 2;
                float angleStep = -2f * (float)Math.PI / size;

                for (int start = 0; start < n; start += size)
                {
                    for (int k = 0; k < halfSize; k++)
                    {
                        float angle = angleStep * k;
                        Complex w = new((float)Math.Cos(angle), (float)Math.Sin(angle));

                        int evenIndex = start + k;
                        int oddIndex = start + k + halfSize;

                        Complex even = buffer[evenIndex];
                        Complex odd = buffer[oddIndex] * w;

                        buffer[evenIndex] = even + odd;
                        buffer[oddIndex] = even - odd;
                    }
                }
            }
        }

        /// <summary>
        /// Reverses bits of an integer for bit-reversal permutation
        /// </summary>
        private static int ReverseBits(int value, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (value & 1);
                value >>= 1;
            }
            return result;
        }

        /// <summary>
        /// Extracts device index from formatted device name
        /// </summary>
        private static int GetDeviceIndexFromName(string deviceName)
        {
            try
            {
                // Device name format: "Device Name (Device X)"
                int startIndex = deviceName.LastIndexOf("Device ") + 7;
                int endIndex = deviceName.LastIndexOf(')');

                if (startIndex > 6 && endIndex > startIndex)
                {
                    string indexStr = deviceName[startIndex..endIndex];
                    if (int.TryParse(indexStr, out int deviceIndex))
                    {
                        return deviceIndex;
                    }
                }

                // Fallback: try to find device by name
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (capabilities.ProductName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return -1;
            }
            catch
            {
                return -1;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                StopCapture();
                _waveIn?.Dispose();
            }

            _disposed = true;
        }

        ~AudioNAudioService()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Represents a complex number for FFT calculations
    /// </summary>
    internal readonly struct Complex(float real, float imaginary)
    {
        public float Real { get; } = real;
        public float Imaginary { get; } = imaginary;

        public float Magnitude => (float)Math.Sqrt(Real * Real + Imaginary * Imaginary);

        public static Complex operator +(Complex a, Complex b) =>
            new(a.Real + b.Real, a.Imaginary + b.Imaginary);

        public static Complex operator -(Complex a, Complex b) =>
            new(a.Real - b.Real, a.Imaginary - b.Imaginary);

        public static Complex operator *(Complex a, Complex b) =>
            new(
                a.Real * b.Real - a.Imaginary * b.Imaginary,
                a.Real * b.Imaginary + a.Imaginary * b.Real
            );
    }
}
