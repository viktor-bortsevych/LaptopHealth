using System.Windows;
using System.Windows.Controls;
using LaptopHealth.Services.Interfaces;

namespace LaptopHealth.Views
{
    /// <summary>
    /// Interaction logic for CameraTestPage.xaml
    /// Demonstrates camera device enumeration and control
    /// Uses Infrastructure Service -> Hardware Service architecture
    /// </summary>
    public partial class CameraTestPage : UserControl, ITestPage
    {
        private readonly ICameraService _cameraService;

        public string TestName => "Camera Test";
        public string TestDescription => "Tests camera device enumeration and control";

        public CameraTestPage()
        {
            InitializeComponent();

            _cameraService = App.ServiceProvider?.GetService(typeof(ICameraService)) as ICameraService
                ?? throw new InvalidOperationException("ICameraService is not registered");

            LoadAvailableDevices();
        }

        /// <summary>
        /// Loads available camera devices into the ComboBox
        /// </summary>
        private async void LoadAvailableDevices()
        {
            try
            {
                var devices = await _cameraService.GetAvailableDevicesAsync();
                
                DeviceComboBox.Items.Clear();
                foreach (var device in devices)
                {
                    DeviceComboBox.Items.Add(device);
                }

                if (DeviceComboBox.Items.Count > 0)
                {
                    DeviceComboBox.SelectedIndex = 0;
                }

                UpdateLastAction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CameraTestPage] Error loading devices: {ex.Message}");
                LastActionText.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles camera device selection change
        /// </summary>
        private async void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceComboBox.SelectedItem == null) return;

            try
            {
                var deviceName = DeviceComboBox.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(deviceName))
                {
                    await _cameraService.SelectDeviceAsync(deviceName);
                    UpdateLastAction();
                    UpdateCameraStatus();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CameraTestPage] Error selecting device: {ex.Message}");
                LastActionText.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Handles Start/Stop button click
        /// </summary>
        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cameraService.IsCameraRunning)
                {
                    // Stop the camera
                    await _cameraService.StopCameraAsync();
                    StartStopButton.Content = "Start Camera";
                    CameraStatusText.Text = "Camera Stopped";
                }
                else
                {
                    // Start the camera
                    var result = await _cameraService.StartCameraAsync();
                    
                    if (result)
                    {
                        StartStopButton.Content = "Stop Camera";
                        CameraStatusText.Text = "Camera Running";
                    }
                    else
                    {
                        CameraStatusText.Text = "Failed to Start";
                    }
                }

                UpdateLastAction();
                UpdateCameraStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CameraTestPage] Error toggling camera: {ex.Message}");
                LastActionText.Text = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Updates the last action text from the service
        /// </summary>
        private void UpdateLastAction()
        {
            LastActionText.Text = _cameraService.LastAction;
        }

        /// <summary>
        /// Updates the camera status display
        /// </summary>
        private void UpdateCameraStatus()
        {
            if (_cameraService.SelectedDevice != null)
            {
                if (_cameraService.IsCameraRunning)
                {
                    CameraStatusText.Text = "Camera Running";
                    StartStopButton.Content = "Stop Camera";
                }
                else
                {
                    CameraStatusText.Text = "Camera Ready";
                    StartStopButton.Content = "Start Camera";
                }
            }
            else
            {
                CameraStatusText.Text = "No Device Selected";
                StartStopButton.Content = "Start Camera";
            }
        }
    }
}
