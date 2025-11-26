using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels.Infrastructure;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace LaptopHealth.ViewModels
{
    public class AudioTestPageViewModel : ViewModelBase, IAsyncDisposable
    {
        private const string FilePrefix = "File: ";
        private readonly IAudioPlaybackService _audioPlaybackService;
        private readonly IDialogService _dialogService;
        private readonly ILogger _logger;
        private bool _disposed;

        private string? _selectedOutputDevice;
        private string? _selectedTestAudio;
        private double _stereoBalance;
        private bool _isPlaying;
        private bool _canStop = true;
        private string _playButtonContent = "Play Test Audio";
        private string _stopButtonContent = "Stop";
        private string _lastActionText = "Ready to test audio output";

        public AudioTestPageViewModel(
            IAudioPlaybackService audioPlaybackService, 
            IDialogService dialogService,
            ILogger logger)
        {
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                _audioPlaybackService.PlaybackStopped += OnPlaybackStopped;

                OutputDevices = [];
                TestAudioOptions = [];

                PlayCommand = new AsyncRelayCommand(_ => PlayAudio(), _ => !IsPlaying && SelectedOutputDevice != null && SelectedTestAudio != null);
                StopCommand = new AsyncRelayCommand(_ => StopAudio(), _ => IsPlaying && CanStop);
                AddAudioFileCommand = new RelayCommand(_ => AddAudioFile());
                DeleteAudioFileCommand = new RelayCommand(_ => DeleteAudioFile(), _ => CanDeleteAudioFile);
                SetBalanceLeftCommand = new RelayCommand(_ => SetBalanceLeft());
                SetBalanceMidCommand = new RelayCommand(_ => SetBalanceMid());
                SetBalanceRightCommand = new RelayCommand(_ => SetBalanceRight());
                RefreshDevicesCommand = new RelayCommand(_ => RefreshDevices());

                LoadTestAudioOptions();
                LoadOutputDevices();
            }
            catch
            {
                _audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;
                throw;
            }
        }

        public ObservableCollection<string> OutputDevices { get; }
        public ObservableCollection<string> TestAudioOptions { get; }

        public string? SelectedOutputDevice
        {
            get => _selectedOutputDevice;
            set
            {
                if (SetProperty(ref _selectedOutputDevice, value) && value != null)
                {
                    _audioPlaybackService.SelectOutputDevice(value);
                    LastActionText = $"Selected output device: {value}";
                }
            }
        }

        public string? SelectedTestAudio
        {
            get => _selectedTestAudio;
            set
            {
                if (SetProperty(ref _selectedTestAudio, value) && value != null)
                {
                    LastActionText = $"Selected audio: {value}";
                    OnPropertyChanged(nameof(CanDeleteAudioFile));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanDeleteAudioFile => !string.IsNullOrEmpty(SelectedTestAudio) && 
                                           SelectedTestAudio.StartsWith(FilePrefix);

        public double StereoBalance
        {
            get => _stereoBalance;
            set
            {
                if (SetProperty(ref _stereoBalance, value))
                {
                    _audioPlaybackService.SetStereoBalance((float)value);
                    string position = GetBalancePosition(value);
                    LastActionText = $"Stereo balance set to: {position} ({value:F2})";
                }
            }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    OnPropertyChanged(nameof(IsNotPlaying));
                    UpdateButtonContent();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsNotPlaying => !IsPlaying;

        public bool CanStop
        {
            get => _canStop;
            set
            {
                if (SetProperty(ref _canStop, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string PlayButtonContent
        {
            get => _playButtonContent;
            set => SetProperty(ref _playButtonContent, value);
        }

        public string StopButtonContent
        {
            get => _stopButtonContent;
            set => SetProperty(ref _stopButtonContent, value);
        }

        public string LastActionText
        {
            get => _lastActionText;
            set => SetProperty(ref _lastActionText, value);
        }

        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand AddAudioFileCommand { get; }
        public ICommand DeleteAudioFileCommand { get; }
        public ICommand SetBalanceLeftCommand { get; }
        public ICommand SetBalanceMidCommand { get; }
        public ICommand SetBalanceRightCommand { get; }
        public ICommand RefreshDevicesCommand { get; }

        private void UpdateButtonContent()
        {
            PlayButtonContent = IsPlaying ? "Playing..." : "Play Test Audio";
            StopButtonContent = "Stop";
        }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            // Only update if we weren't manually stopping (avoid race conditions)
            if (IsPlaying)
            {
                IsPlaying = false;
                LastActionText = "Playback finished";
            }
        }

        private void SetBalanceLeft()
        {
            StereoBalance = -1.0;
        }

        private void SetBalanceMid()
        {
            StereoBalance = 0.0;
        }

        private void SetBalanceRight()
        {
            StereoBalance = 1.0;
        }

        private void RefreshDevices()
        {
            LoadOutputDevices();
            LastActionText = $"Refreshed devices - {OutputDevices.Count} device(s) found";
        }

        private void AddAudioFile()
        {
            try
            {
                string? sourceFile = _dialogService.OpenFileDialog(
                    "Select Audio File",
                    "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|All Files (*.*)|*.*",
                    false);

                if (sourceFile == null) return;

                string fileName = Path.GetFileName(sourceFile);
                string soundsDir = GetSoundsDirectory();

                Directory.CreateDirectory(soundsDir);

                string destFile = GetUniqueFilePath(soundsDir, fileName);
                File.Copy(sourceFile, destFile, false);

                LoadTestAudioOptions();
                SelectedTestAudio = $"{FilePrefix}{Path.GetFileName(destFile)}";
                LastActionText = $"Added custom audio file: {Path.GetFileName(destFile)}";
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to add audio file: {ex.Message}");
                LastActionText = $"Failed to add audio file: {ex.Message}";
            }
        }

        private void DeleteAudioFile()
        {
            if (string.IsNullOrEmpty(SelectedTestAudio) || !SelectedTestAudio.StartsWith(FilePrefix))
                return;

            try
            {
                string fileName = SelectedTestAudio[FilePrefix.Length..];
                string filePath = Path.Combine(GetSoundsDirectory(), fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    LastActionText = $"Deleted audio file: {fileName}";
                    
                    LoadTestAudioOptions();
                    
                    // Select first available option or clear selection
                    SelectedTestAudio = TestAudioOptions.Count > 0 ? TestAudioOptions[0] : null;
                }
                else
                {
                    LastActionText = $"Audio file not found: {fileName}";
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to delete audio file: {ex.Message}");
                LastActionText = $"Failed to delete audio file: {ex.Message}";
            }
        }

        private void LoadTestAudioOptions()
        {
            TestAudioOptions.Clear();

            // Load custom audio files
            string soundsDir = GetSoundsDirectory();
            if (Directory.Exists(soundsDir))
            {
                var audioFiles = Directory.GetFiles(soundsDir, "*.*")
                    .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(Path.GetFileName);

                foreach (var file in audioFiles)
                {
                    TestAudioOptions.Add($"{FilePrefix}{Path.GetFileName(file)}");
                }
            }

            // Add built-in options
            TestAudioOptions.Add("Sine Wave (440 Hz - A4)");
            TestAudioOptions.Add("Sine Wave (880 Hz - A5)");
            TestAudioOptions.Add("White Noise");
            TestAudioOptions.Add("Pink Noise");
        }

        private static string GetSoundsDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Check if running from bin\Debug or bin\Release
            if (baseDir.Contains(@"\bin\"))
            {
                // Development: navigate to project root
                var projectDir = Directory.GetParent(baseDir);
                while (projectDir != null && !File.Exists(Path.Combine(projectDir.FullName, "LaptopHealth.csproj")))
                {
                    projectDir = projectDir.Parent;
                }

                if (projectDir != null)
                {
                    return Path.Combine(projectDir.FullName, "Resources", "Sounds");
                }
            }

            // Production or fallback
            return Path.Combine(baseDir, "Sounds");
        }

        private static string GetUniqueFilePath(string directory, string fileName)
        {
            string filePath = Path.Combine(directory, fileName);
            if (!File.Exists(filePath)) return filePath;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int counter = 1;

            do
            {
                fileName = $"{nameWithoutExt}_{counter}{extension}";
                filePath = Path.Combine(directory, fileName);
                counter++;
            } while (File.Exists(filePath));

            return filePath;
        }

        private void LoadOutputDevices()
        {
            var devices = _audioPlaybackService.GetOutputDevices();
            OutputDevices.Clear();

            foreach (var device in devices)
            {
                OutputDevices.Add(device);
            }

            if (OutputDevices.Count > 0 && SelectedOutputDevice == null)
            {
                SelectedOutputDevice = OutputDevices[0];
            }
        }

        private async Task PlayAudio()
        {
            if (SelectedTestAudio == null) return;

            bool success;
            string audioDescription;

            if (SelectedTestAudio.StartsWith(FilePrefix))
            {
                string fileName = SelectedTestAudio[FilePrefix.Length..];
                string filePath = Path.Combine(GetSoundsDirectory(), fileName);
                success = _audioPlaybackService.PlayAudioFile(filePath);
                audioDescription = fileName;
            }
            else
            {
                string audioType = GetAudioType(SelectedTestAudio);
                success = _audioPlaybackService.PlayTestAudio(audioType);
                audioDescription = SelectedTestAudio;
            }

            if (success)
            {
                IsPlaying = true;
                CanStop = false;
                LastActionText = $"Playing: {audioDescription}";
                
                await Task.Delay(500);
                
                CanStop = true;
            }
            else
            {
                LastActionText = $"Failed to play: {audioDescription}";
            }
        }

        private async Task StopAudio()
        {
            try
            {
                // Immediately update UI state
                IsPlaying = false;
                CanStop = false;
                LastActionText = "Stopping playback...";
                
                // Stop playback asynchronously to avoid UI freeze
                await _audioPlaybackService.StopPlaybackAsync();
                
                LastActionText = "Playback stopped";
            }
            catch (Exception ex)
            {
                _logger.Error($"Error stopping audio: {ex.Message}");
                LastActionText = $"Error stopping playback: {ex.Message}";
            }
            finally
            {
                CanStop = true;
            }
        }

        private static string GetAudioType(string selection) => selection switch
        {
            "Sine Wave (440 Hz - A4)" => "sine440",
            "Sine Wave (880 Hz - A5)" => "sine880",
            "White Noise" => "whitenoise",
            "Pink Noise" => "pinknoise",
            _ => "sine440"
        };

        private static string GetBalancePosition(double value)
        {
            if (value < -0.3)
                return "Left";
            if (value > 0.3)
                return "Right";
            return "Center";
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_disposed)
            {
                LogDebug("[AudioTestPageViewModel] DisposeAsyncCore called but already disposed, skipping");
                return;
            }

            LogDebug("[AudioTestPageViewModel] ===============================================================================");
            LogDebug("[AudioTestPageViewModel] DisposeAsyncCore called");
            LogDebug($"[AudioTestPageViewModel] Instance hash: {GetHashCode()}");

            try
            {
                LogDebug("[AudioTestPageViewModel] Unsubscribing from PlaybackStopped event");
                _audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;

                LogDebug("[AudioTestPageViewModel] Calling StopPlaybackAsync");
                await _audioPlaybackService.StopPlaybackAsync().ConfigureAwait(false);
                LogDebug("[AudioTestPageViewModel] StopPlaybackAsync completed");
            }
            catch (Exception ex)
            {
                LogDebug($"[AudioTestPageViewModel] Error during cleanup: {ex.Message}");
            }
            finally
            {
                _disposed = true;
                LogDebug("[AudioTestPageViewModel] DisposeAsyncCore completed");
                LogDebug("[AudioTestPageViewModel] ===============================================================================");
            }
        }

        public async ValueTask DisposeAsync()
        {
            LogDebug("[AudioTestPageViewModel] DisposeAsync() method called");

            await DisposeAsyncCore().ConfigureAwait(false);

            GC.SuppressFinalize(this);

            LogDebug("[AudioTestPageViewModel] DisposeAsync() completed");
        }

        #region Logging

        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        #endregion
    }
}
