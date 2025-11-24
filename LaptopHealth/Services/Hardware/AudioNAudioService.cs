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
        private readonly float[]? _buffer;
        private int _bufferIndex;
        private readonly float[]? _frequencyBands;
        private readonly float[]? _smoothedBands;
        private int[]? _binMap;
        private string? _selectedDevice;
        private bool _isCapturing;
        private readonly Lock _lockObject = new();
        private readonly ILogger _logger;
        private bool _disposed;

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
            InitializeBinMap();
        }

        /// <summary>
        /// Initializes the frequency bin mapping for FFT analysis
        /// </summary>
        private void InitializeBinMap()
        {
            _binMap = new int[FREQUENCY_BANDS + 1];
            for (int i = 0; i <= FREQUENCY_BANDS; i++)
            {
                // Logarithmic frequency mapping from 20Hz to Nyquist frequency
                float freq = 20 * (float)Math.Pow(SAMPLE_RATE / 2.0 / 20.0, i / (float)FREQUENCY_BANDS);
                _binMap[i] = Math.Min((int)(freq * BUFFER_SIZE / SAMPLE_RATE), BUFFER_SIZE / 2 - 1);
            }
        }

        public async Task<IEnumerable<string>> GetAvailableDevicesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var devices = new List<string>();

                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (capabilities.Channels > 0) // Only include devices with audio input
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
                    await StopCaptureAsync();
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

        public async Task<bool> StartCaptureAsync(CancellationToken cancellationToken)
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
                lock (_lockObject)
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
                    _bufferIndex = 0;

                    _logger.Info("Audio capture started");
                }

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

        public async Task<bool> StopCaptureAsync()
        {
            try
            {
                lock (_lockObject)
                {
                    _isCapturing = false;

                    if (_waveIn != null)
                    {
                        _waveIn.StopRecording();
                        _waveIn.Dispose();
                        _waveIn = null;
                    }

                    if (_frequencyBands != null)
                    {
                        Array.Clear(_frequencyBands, 0, _frequencyBands.Length);
                    }

                    if (_smoothedBands != null)
                    {
                        Array.Clear(_smoothedBands, 0, _smoothedBands.Length);
                    }

                    _logger.Info("Audio capture stopped");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping audio capture: {ex.Message}");
                return false;
            }
        }

        public async Task<float[]?> GetFrequencyDataAsync(CancellationToken cancellationToken)
        {
            if (!_isCapturing || _smoothedBands == null)
            {
                return null;
            }

            lock (_lockObject)
            {
                return (float[])_smoothedBands.Clone();
            }
        }

        /// <summary>
        /// Processes incoming audio data
        /// </summary>
        private void ProcessAudioData(object? sender, WaveInEventArgs e)
        {
            if (_buffer == null)
                return;

            try
            {
                for (int i = 0; i < e.BytesRecorded / 2; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    _buffer[_bufferIndex] = sample / 32768f;
                    _bufferIndex = (_bufferIndex + 1) % BUFFER_SIZE;

                    if (_bufferIndex % (BUFFER_SIZE / 2) == 0)
                    {
                        AnalyzeFrequencies();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing audio data: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzes audio frequencies using FFT
        /// </summary>
        private void AnalyzeFrequencies()
        {
            if (_buffer == null || _frequencyBands == null || _smoothedBands == null || _binMap == null)
                return;

            lock (_lockObject)
            {
                // Compute FFT magnitudes
                float[] magnitudes = ComputeFFT();

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
        /// Computes FFT magnitudes for the current buffer
        /// </summary>
        private float[] ComputeFFT()
        {
            if (_buffer == null)
                return new float[BUFFER_SIZE / 2];

            float[] magnitudes = new float[BUFFER_SIZE / 2];

            for (int k = 0; k < BUFFER_SIZE / 2; k++)
            {
                float real = 0, imag = 0;

                for (int n = 0; n < BUFFER_SIZE; n++)
                {
                    float window = 0.5f * (1 - (float)Math.Cos(2 * Math.PI * n / (BUFFER_SIZE - 1)));
                    int idx = ((_bufferIndex - BUFFER_SIZE + n) % _buffer.Length + _buffer.Length) % _buffer.Length;

                    float angle = -2f * (float)Math.PI * k * n / BUFFER_SIZE;
                    real += _buffer[idx] * window * (float)Math.Cos(angle);
                    imag += _buffer[idx] * window * (float)Math.Sin(angle);
                }

                magnitudes[k] = (float)Math.Sqrt(real * real + imag * imag) / BUFFER_SIZE;
            }

            return magnitudes;
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
                StopCaptureAsync().Wait();
                _waveIn?.Dispose();
            }

            _disposed = true;
        }

        ~AudioNAudioService()
        {
            Dispose(false);
        }
    }
}
