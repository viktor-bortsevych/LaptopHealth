# Camera Test Page

## Overview
The Camera Test Page demonstrates camera device enumeration and control following clean architecture principles.

## Architecture

### Layered Service Architecture
```
UI Layer (CameraTestPage)
    ?
Infrastructure Service (ICameraService / CameraService)
    ?
Hardware Service (ICameraHardwareService / CameraHardwareService)
    ?
3rd Party Library (TODO: OpenCV, DirectShow, etc.)
```

### Design Principles Applied
- **SOLID**: 
  - Single Responsibility: Each service has one clear purpose
  - Open/Closed: Services use interfaces for extensibility
  - Liskov Substitution: Interfaces allow different implementations
  - Interface Segregation: Clean, focused interfaces
  - Dependency Inversion: UI depends on abstractions, not implementations

- **DRY**: Common camera operations are centralized in services
- **KISS**: Simple, straightforward implementation with clear responsibilities

## Components

### Services

#### ICameraHardwareService
Low-level hardware abstraction that will interface with 3rd party camera libraries.

**Methods:**
- `GetAvailableDevicesAsync()` - Enumerate camera devices
- `InitializeDeviceAsync(deviceName)` - Initialize specific camera
- `StartCaptureAsync()` - Start video capture
- `StopCaptureAsync()` - Stop video capture
- `GetCurrentFrameAsync()` - Get current frame data
- `DisposeAsync()` - Release camera resources

**Status:** Stub implementation (returns mock data)
**TODO:** Integrate with OpenCV, DirectShow, or similar library

#### ICameraService
Infrastructure-level service that coordinates camera operations and manages state.

**Methods:**
- `GetAvailableDevicesAsync()` - Get available cameras
- `SelectDeviceAsync(deviceName)` - Select and initialize a camera
- `StartCameraAsync()` - Start camera capture
- `StopCameraAsync()` - Stop camera capture
- `GetCurrentFrameAsync()` - Get current frame for display

**Properties:**
- `SelectedDevice` - Currently selected camera
- `IsCameraRunning` - Camera state
- `LastAction` - Last operation performed

### UI Layout

#### Two-Column Layout

**Left Column (2/3 width):**
- Row 1: Camera preview placeholder (with camera icon and status text)
- Row 2: Description card with test information

**Right Column (1/3 width):**
- Row 1: "Select Camera Device" header
- Row 2: Camera device ComboBox (DefaultComboBoxStyle)
- Row 3: Start/Stop camera button
- Row 4: Last action display

### Design System
The page uses the unified LaptopHealth design system:
- **Colors**: Primary Blue (#132779), White (#FFFFFF), Dark Gray (#212121)
- **Typography**: HeadlineText and BodyText styles
- **Components**: DefaultComboBoxStyle, DefaultButtonStyle, CardStyle
- **Layout**: Responsive grid with proper spacing

## Usage

### For Users
1. Select a camera device from the dropdown
2. Click "Start Camera" to begin capture
3. Click "Stop Camera" to end capture
4. View last action in the status panel

### For Developers

#### Adding Real Camera Support
Replace `CameraHardwareService` stub implementation:

```csharp
// Example with OpenCV (pseudocode)
public async Task<IEnumerable<string>> GetAvailableDevicesAsync()
{
    var devices = new List<string>();
    // Use OpenCV to enumerate devices
    for (int i = 0; i < VideoCapture.DeviceCount; i++)
    {
        devices.Add($"Camera {i}");
    }
    return devices;
}
```

#### Extending Functionality
Add new methods to interfaces and implement in services:
```csharp
// In ICameraHardwareService
Task<bool> TakeSnapshotAsync(string filePath);

// In ICameraService
Task<bool> CapturePhotoAsync(string savePath);
```

## Testing
The page is registered as a test page and can be accessed through the test registry system.

## Dependencies
- Microsoft.Extensions.DependencyInjection (for DI)
- TODO: Add OpenCV, DirectShow, or camera library package

## Future Enhancements
1. Integrate real camera library (OpenCV recommended)
2. Add frame display in camera preview area
3. Implement snapshot/photo capture
4. Add camera settings (resolution, FPS, etc.)
5. Add video recording capability
6. Add error handling and retry logic
7. Add camera capability detection (resolution, formats)

## Files Created
- `/Services/Interfaces/ICameraHardwareService.cs` - Hardware service interface
- `/Services/Interfaces/ICameraService.cs` - Infrastructure service interface
- `/Services/Hardware/CameraHardwareService.cs` - Hardware service implementation
- `/Services/Infrastructure/CameraService.cs` - Infrastructure service implementation
- `/Views/Tests/CameraTestPage.xaml` - UI definition
- `/Views/Tests/CameraTestPage.xaml.cs` - UI logic
