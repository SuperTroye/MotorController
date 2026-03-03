# MotorController

A C# stepper motor controller library for Raspberry Pi with GTK-based GUI. Controls stepper motors via GPIO pins with smooth acceleration/deceleration profiles using the David Austin algorithm.

## Features

- **Precise Motion Control**: Smooth acceleration/deceleration using the David Austin stepper motor algorithm
- **Thread-Safe Operations**: Async/await pattern with proper cancellation support
- **Limit Switch Support**: Active-low limit switches with event-driven updates
- **Cross-Platform Development**: Mock GPIO controller for Windows development
- **Touch-Friendly GUI**: GTK 4.0-based interface designed for 800x480 touchscreen
- **Configurable**: Runtime configuration via JSON with GUI settings editor
- **Well-Tested**: Comprehensive unit tests using XUnit and NSubstitute

## Target Platform

- **Primary**: Raspberry Pi (Linux ARM)
- **Development**: Windows/Linux with FakeGpioController simulation
- **.NET Version**: .NET 10.0

## Prerequisites

- .NET SDK 10.0.102 or later
- For production: Raspberry Pi with GPIO-connected stepper motor driver
- For development: Windows or Linux (uses console-based GPIO simulation)

## Quick Start

### Build

```bash
# 1. Restore dependencies (required first step)
dotnet restore

# 2. Build the solution
dotnet build

# 3. Run tests
dotnet test
```


**Configurable via GUI**: All settings can be modified at runtime through the Settings dialog.

### StepsPerRevolution Options
- 200, 400, 800, 1600, 3200, 6400, 12800

### Default Pins (Raspberry Pi GPIO)
- **Pulse**: GPIO 21
- **Direction**: GPIO 20
- **Min Limit Switch**: GPIO 23 (active-low with pull-up)
- **Max Limit Switch**: GPIO 24 (active-low with pull-up)
- **Enable**: Optional (set to enable/disable motor driver)

## Key Concepts

### David Austin Algorithm
Implements smooth acceleration/deceleration:
- Initial delay: `c0 = 0.676 * sqrt(2/α) * 10^6` microseconds
- Acceleration: `cn = cn-1 - (2 * cn-1) / (4n + 1)`
- Constant speed phase at target RPM
- Deceleration mirrors acceleration profile

### Thread Safety
- Position tracking protected by `SemaphoreSlim`
- Async/await pattern throughout
- `CancellationToken` support for all motion methods

### Microsecond-Precision Timing
- Custom `MicrosecondSleep()` using `Stopwatch.GetTimestamp()`
- No Thread.Sleep() (too imprecise for stepper pulses)

## Usage Examples

### Basic Motion Control

```csharp
// Inject dependencies (via DI)
IStepperMotorController motor = serviceProvider.GetRequiredService<IStepperMotorController>();

// Move 5 inches at 100 RPM
await motor.MoveInchesAsync(5.0, 100.0, cancellationToken);

// Run to max limit switch at 50 RPM
await motor.RunToLimitSwitchAsync(toMaxLimit: true, 50.0, cancellationToken);

// Stop with deceleration
await motor.StopAsync();

// Reset position to zero
await motor.ResetPositionAsync();

// Get current position
double position = motor.CurrentPositionInches;
```

### Limit Switch Events

```csharp
motor.MinLimitSwitchTriggered += (sender, triggered) =>
{
    Console.WriteLine($"Min limit switch: {(triggered ? "TRIGGERED" : "Released")}");
};

motor.MaxLimitSwitchTriggered += (sender, triggered) =>
{
    Console.WriteLine($"Max limit switch: {(triggered ? "TRIGGERED" : "Released")}");
};
```

## Testing

Run all tests:
```bash
dotnet test
```

Tests use NSubstitute to mock `IGpioController` interface, allowing full unit testing without hardware.

## Dependencies

### MotorController Library
- `System.Device.Gpio` v4.1.0
- `Microsoft.Extensions.Logging` v10.0.3

### UI Application
- `GirCore.Gtk-4.0` v0.7.0
- `Microsoft.Extensions.Hosting` v10.0.3
- `Microsoft.Extensions.Logging.Console` v10.0.3

### Test Project
- `xunit` v2.9.3
- `NSubstitute` v5.3.0
- `Microsoft.NET.Test.Sdk` v18.0.1

## Hardware Setup

1. Connect stepper motor driver to Raspberry Pi GPIO pins
2. Configure pins in `UI/appsettings.json`
3. Ensure limit switches are wired active-low with pull-ups
4. Optionally connect enable pin for motor driver control


## Documentation

- **Copilot Instructions**: `.github/copilot-instructions.md` - Comprehensive development guide
- **Project Specs**: 
  - `MotorController/specs.md` - Library requirements
  - `UI/specs.md` - GUI requirements
  - `Test/specs.md` - Testing requirements

## Support

For issues, questions, or contributions, visit the GitHub repository:
https://github.com/SuperTroye/MotorController
