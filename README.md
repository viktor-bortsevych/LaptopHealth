# LaptopHealth

A comprehensive WPF application for testing and diagnosing laptop hardware components including audio, camera, microphone, and keyboard functionality.

## Build & Publish

To build and publish the application:

```bash
dotnet publish LaptopHealth\LaptopHealth.csproj -c Release -o .\publish
```

This command will:
- Compile the project in Release mode
- Optimize the application for production
- Output the published binaries to the `.\publish` folder

## Project Structure

- **Views**: WPF user controls for different hardware test pages
- **ViewModels**: MVVM view models for managing test logic and UI state
- **Services**: Hardware testing services and infrastructure
  - Audio, Camera, Microphone, and Keyboard services
  - Audio playback and processing capabilities
- **Configuration**: Dependency injection and logging setup
- **Converters**: Value converters for WPF data binding

## Features

- **Audio Testing**: Test audio input/output and frequency analysis
- **Camera Testing**: Check camera functionality using OpenCV
- **Microphone Testing**: Verify microphone performance
- **Keyboard Testing**: Test keyboard input responsiveness

## Requirements

- .NET 10
- Windows OS (WPF requirement)
- Visual Studio 2022 or higher (recommended)

## Development

To build and debug locally:

```bash
dotnet build
dotnet run
```

## License

Check the repository for license information.