using LaptopHealth.Services.Interfaces;
using LaptopHealth.ViewModels.Infrastructure;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace LaptopHealth.ViewModels
{
    public class AudioTestPageViewModel : ViewModelBase, IDisposable, IAsyncDisposable
    {
        private const string FilePrefix = "File: ";
        private readonly IAudioPlaybackService _audioPlaybackService;
        private readonly ILogger _logger;
        private bool _disposed;

        private string? _selectedOutputDevice;
        private string? _selectedTestAudio;
        private double _stereoBalance;
        private bool _isPlaying;
        private bool _canStop = true;

        public AudioTestPageViewModel(IAudioPlaybackService audioPlaybackService, ILogger logger)
        {
            _audioPlaybackService = audioPlaybackService ?? throw new ArgumentNullException(nameof(audioPlaybackService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _audioPlaybackService.PlaybackStopped += OnPlaybackStopped;

            OutputDevices = [];
            TestAudioOptions = [];

            PlayCommand = new RelayCommand(_ => PlayAudio(), _ => !IsPlaying && SelectedOutputDevice != null && SelectedTestAudio != null);
            StopCommand = new RelayCommand(_ => StopAudio(), _ => IsPlaying && CanStop);
            AddAudioFileCommand = new RelayCommand(_ => AddAudioFile());
            SetBalanceLeftCommand = new RelayCommand(_ => StereoBalance = -1.0);
            SetBalanceMidCommand = new RelayCommand(_ => StereoBalance = 0.0);
            SetBalanceRightCommand = new RelayCommand(_ => StereoBalance = 1.0);
            RefreshDevicesCommand = new RelayCommand(_ => LoadOutputDevices());

            LoadTestAudioOptions();
            LoadOutputDevices();
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
                }
            }
        }

        public string? SelectedTestAudio
        {
            get => _selectedTestAudio;
            set => SetProperty(ref _selectedTestAudio, value);
        }

        public double StereoBalance
        {
            get => _stereoBalance;
            set
            {
                if (SetProperty(ref _stereoBalance, value))
                {
                    _audioPlaybackService.SetStereoBalance((float)value);
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

        public ICommand PlayCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand AddAudioFileCommand { get; }
        public ICommand SetBalanceLeftCommand { get; }
        public ICommand SetBalanceMidCommand { get; }
        public ICommand SetBalanceRightCommand { get; }
        public ICommand RefreshDevicesCommand { get; }

        private void OnPlaybackStopped(object? sender, EventArgs e)
        {
            IsPlaying = false;
        }

        private void AddAudioFile()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Audio File",
                    Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|All Files (*.*)|*.*",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() != true) return;

                string sourceFile = openFileDialog.FileName;
                string fileName = Path.GetFileName(sourceFile);
                string soundsDir = GetSoundsDirectory();

                Directory.CreateDirectory(soundsDir);

                string destFile = GetUniqueFilePath(soundsDir, fileName);
                File.Copy(sourceFile, destFile, false);

                LoadTestAudioOptions();
                SelectedTestAudio = $"{FilePrefix}{Path.GetFileName(destFile)}";
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to add audio file: {ex.Message}");
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

        private async void PlayAudio()
        {
            if (SelectedTestAudio == null) return;

            bool success;
            if (SelectedTestAudio.StartsWith(FilePrefix))
            {
                string fileName = SelectedTestAudio[FilePrefix.Length..];
                string filePath = Path.Combine(GetSoundsDirectory(), fileName);
                success = _audioPlaybackService.PlayAudioFile(filePath);
            }
            else
            {
                string audioType = GetAudioType(SelectedTestAudio);
                success = _audioPlaybackService.PlayTestAudio(audioType);
            }

            if (success)
            {
                IsPlaying = true;
                CanStop = false;
                
                await Task.Delay(500);
                
                CanStop = true;
            }
        }

        private void StopAudio()
        {
            _audioPlaybackService.StopPlayback();
            IsPlaying = false;
        }

        private static string GetAudioType(string selection) => selection switch
        {
            "Sine Wave (440 Hz - A4)" => "sine440",
            "Sine Wave (880 Hz - A5)" => "sine880",
            "White Noise" => "whitenoise",
            "Pink Noise" => "pinknoise",
            _ => "sine440"
        };

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                _audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;
                _audioPlaybackService.StopPlayback();
            }

            // Dispose unmanaged resources (if any)

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            // Dispose managed resources asynchronously
            _audioPlaybackService.PlaybackStopped -= OnPlaybackStopped;
            await Task.Run(() => _audioPlaybackService.StopPlayback());

            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}