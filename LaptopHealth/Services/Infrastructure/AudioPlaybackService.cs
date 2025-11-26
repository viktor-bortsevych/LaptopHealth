using LaptopHealth.Services.Interfaces;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace LaptopHealth.Services.Infrastructure
{
    public class AudioPlaybackService(ILogger logger) : IAudioPlaybackService, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly Lock _lock = new();

        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioFileReader;
        private BalanceSampleProvider? _balanceProvider;

        private string? _selectedDevice;
        private float _currentBalance;
        private volatile bool _isPlaying;
        private volatile bool _isStopping;
        private bool _disposed;

        public event EventHandler? PlaybackStopped;
        public bool IsPlaying => _isPlaying;

        public IEnumerable<string> GetOutputDevices()
        {
            var devices = new List<string>();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                devices.Add($"{capabilities.ProductName} (Device {i})");
            }
            return devices;
        }

        public bool SelectOutputDevice(string deviceName)
        {
            lock (_lock)
            {
                StopPlaybackInternal();
                _selectedDevice = deviceName;
                return true;
            }
        }

        public bool PlayTestAudio(string audioType)
        {
            if (string.IsNullOrWhiteSpace(_selectedDevice))
                return false;

            lock (_lock)
            {
                try
                {
                    StopPlaybackInternal();

                    int deviceIndex = GetDeviceIndexFromName(_selectedDevice);
                    if (deviceIndex < 0 || deviceIndex >= WaveOut.DeviceCount)
                        return false;

                    _balanceProvider = new BalanceSampleProvider(CreateSignalGenerator(audioType))
                    {
                        Balance = _currentBalance
                    };

                    _waveOut = new WaveOutEvent { DeviceNumber = deviceIndex };
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    _waveOut.Init(_balanceProvider);
                    _waveOut.Play();

                    _isPlaying = true;
                    _isStopping = false;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to play test audio: {ex.Message}");
                    CleanupResources();
                    return false;
                }
            }
        }

        public bool PlayAudioFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(_selectedDevice) || !File.Exists(filePath))
                return false;

            lock (_lock)
            {
                try
                {
                    StopPlaybackInternal();

                    int deviceIndex = GetDeviceIndexFromName(_selectedDevice);
                    if (deviceIndex < 0 || deviceIndex >= WaveOut.DeviceCount)
                        return false;

                    _audioFileReader = new AudioFileReader(filePath);
                    _balanceProvider = new BalanceSampleProvider(_audioFileReader)
                    {
                        Balance = _currentBalance
                    };

                    _waveOut = new WaveOutEvent { DeviceNumber = deviceIndex };
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    _waveOut.Init(_balanceProvider);
                    _waveOut.Play();

                    _isPlaying = true;
                    _isStopping = false;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to play audio file: {ex.Message}");
                    CleanupResources();
                    return false;
                }
            }
        }

        public void StopPlayback()
        {
            if (_isStopping) return;

            lock (_lock)
            {
                StopPlaybackInternal();
            }
        }

        public async Task StopPlaybackAsync()
        {
            if (_isStopping) return;

            _isStopping = true;

            try
            {
                // Run the stop operation on a background thread to avoid blocking UI
                await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        StopPlaybackInternal();
                    }
                });
            }
            finally
            {
                _isStopping = false;
            }
        }

        public void SetStereoBalance(float balance)
        {
            lock (_lock)
            {
                _currentBalance = Math.Clamp(balance, -1f, 1f);
                if (_balanceProvider == null)
                {
                    return;
                }
                _balanceProvider.Balance = _currentBalance;
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // Update state immediately
            _isPlaying = false;

            if (e.Exception != null)
            {
                _logger.Error($"Playback error: {e.Exception.Message}");
            }

            // Raise event on UI thread
            var handler = PlaybackStopped;
            if (handler != null)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    // Use BeginInvoke for async dispatch - don't block NAudio's thread
                    dispatcher.BeginInvoke(() => handler.Invoke(this, EventArgs.Empty));
                }
                else
                {
                    handler.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void StopPlaybackInternal()
        {
            if (_waveOut != null)
            {
                try
                {
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    
                    // Stop playback - this can potentially block briefly
                    if (_waveOut.PlaybackState != PlaybackState.Stopped)
                    {
                        _waveOut.Stop();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error stopping playback: {ex.Message}");
                }
            }

            CleanupResources();
            _isPlaying = false;
        }

        private void CleanupResources()
        {
            try
            {
                _waveOut?.Dispose();
                _waveOut = null;

                _audioFileReader?.Dispose();
                _audioFileReader = null;

                _balanceProvider = null;
            }
            catch (Exception ex)
            {
                _logger.Error($"Cleanup error: {ex.Message}");
            }
        }

        private static SignalGenerator CreateSignalGenerator(string audioType)
        {
            return audioType.ToLowerInvariant() switch
            {
                "sine440" => new SignalGenerator { Frequency = 440, Type = SignalGeneratorType.Sin, Gain = 0.3 },
                "sine880" => new SignalGenerator { Frequency = 880, Type = SignalGeneratorType.Sin, Gain = 0.3 },
                "whitenoise" => new SignalGenerator { Type = SignalGeneratorType.White, Gain = 0.15 },
                "pinknoise" => new SignalGenerator { Type = SignalGeneratorType.Pink, Gain = 0.15 },
                _ => new SignalGenerator { Frequency = 440, Type = SignalGeneratorType.Sin, Gain = 0.3 }
            };
        }

        private static int GetDeviceIndexFromName(string deviceName)
        {
            // Try to extract index from "(Device N)" format
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

            // Fallback: search by product name
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                if (capabilities.ProductName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_lock)
                {
                    StopPlaybackInternal();
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    internal class BalanceSampleProvider(ISampleProvider source) : ISampleProvider
    {
        private readonly ISampleProvider _source = source.WaveFormat.Channels == 2
                ? source
                : new MonoToStereoSampleProvider(source);
        private float _balance;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public float Balance
        {
            get => _balance;
            set => _balance = Math.Clamp(value, -1f, 1f);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            float balance = _balance; // Local copy

            for (int i = 0; i < samplesRead / 2; i++)
            {
                int leftIndex = offset + (i * 2);
                int rightIndex = leftIndex + 1;

                if (balance < 0)
                {
                    buffer[rightIndex] *= (1f + balance);
                }
                else if (balance > 0)
                {
                    buffer[leftIndex] *= (1f - balance);
                }
            }

            return samplesRead;
        }
    }
}