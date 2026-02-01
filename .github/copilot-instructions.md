# MotorController Repository - Copilot Instructions

## Repository Overview

**Purpose**: C# stepper motor controller library for Raspberry Pi with GTK-based GUI. Controls stepper motors via GPIO pins with acceleration/deceleration profiles using the David Austin algorithm.

**Project Type**: .NET 10.0 console application with GUI capabilities  
**Size**: ~20MB, ~1,180 lines of C# code across 6 files  
**Target Platform**: Raspberry Pi (cross-platform development supported via FakeGpioController)

## Build Instructions

### Prerequisites
- .NET SDK 10.0.102 (project targets net10.0)
- The project builds successfully on Linux with .NET 10.0

### Build Commands (ALWAYS use this exact sequence)

**IMPORTANT**: Always run `dotnet restore` before building, especially after cloning or cleaning:

```bash
# 1. Restore dependencies (REQUIRED first step, takes ~90 seconds on first run)
dotnet restore

# 2. Build the solution (takes ~10 seconds)
dotnet build

# 3. Run tests (takes ~5 seconds, all 30 tests should pass)
dotnet test

# 4. Clean build artifacts (if needed)
dotnet clean
```

**Note**: After running `dotnet clean`, you MUST run `dotnet restore` again before building.

### Known Build Warnings
- Test project has one nullable reference warning (line 421 in StepperMotorControllerTests.cs) - this is expected and does not affect functionality

### Testing
- **Test Framework**: XUnit 2.9.3 with NSubstitute 5.3.0 for mocking
- **All tests must pass**: 30 tests in total, should complete in ~5 seconds
- **Command**: `dotnet test` or `dotnet test Test/Test.csproj`
- Tests are comprehensive and cover constructors, limit switches, motion control, and disposal

### Running the Application
```bash
dotnet run --project MotorController/MotorController.csproj
```
The application uses `FakeGpioController` on Windows and `GpioControllerWrapper` on Linux/Raspberry Pi.

## Project Structure

### Root Directory Files
- `MotorController.sln` - Visual Studio solution file (contains 2 projects)
- `.gitignore` - Standard Visual Studio .gitignore
- `.gitattributes` - Git attributes configuration
- `.vscode/launch.json` - VS Code debug configuration

### Key Directories

**MotorController/** - Main application project
- `MotorController.csproj` - Project file targeting net10.0
- `Program.cs` - Entry point with OS-specific GPIO controller selection
- `specs.md` - Detailed specification document (read this for requirements)
- `Control/` - Motor control implementation
  - `StepperMotorController.cs` (319 lines) - Main controller with David Austin algorithm
  - `ControllerConfig.cs` (55 lines) - Configuration settings class
  - `GpioControllerWrapper.cs` (44 lines) - Real GPIO implementation wrapper
  - `FakeGpioController.cs` (130 lines) - Mock GPIO for Windows development
  - `IGpioController` interface - Abstraction for GPIO operations

**Test/** - XUnit test project
- `Test.csproj` - Test project configuration
- `StepperMotorControllerTests.cs` (535 lines) - Comprehensive unit tests

### Dependencies
**MotorController Project:**
- `GirCore.Gtk-4.0` v0.7.0 (GTK GUI library)
- `System.Device.Gpio` v4.1.0 (GPIO control)

**Test Project:**
- `Microsoft.NET.Test.Sdk` v18.0.1
- `NSubstitute` v5.3.0 (mocking)
- `xunit` v2.9.3
- `xunit.runner.visualstudio` v3.1.5
- `coverlet.collector` v6.0.4
- Project reference to MotorController

## Architecture & Design Patterns

### Motor Control
- **StepperMotorController**: Main class implementing IDisposable
- **Acceleration Algorithm**: David Austin stepper motor algorithm for smooth motion
- **Thread Safety**: Uses SemaphoreSlim for position tracking
- **Async Methods**: All motor control methods are async to avoid blocking
- **Cancellation**: Supports CancellationToken and internal stop mechanism

### GPIO Abstraction
- **IGpioController**: Interface for GPIO operations (enables testing)
- **GpioControllerWrapper**: Production implementation using System.Device.Gpio
- **FakeGpioController**: Development/testing mock with console output
- Platform-specific selection in Program.cs using `OperatingSystem.IsWindows()`

### Key Configuration
- Default pins: Pulse=21, Direction=20, MinLimit=24, MaxLimit=23
- Steps per revolution: 400 (default)
- Lead screw: 5 threads per inch (default)
- Acceleration: 5000 steps/sec² (default)
- Enable pin: Optional

## Development Workflow

### Making Changes
1. Run existing tests first: `dotnet test` (establish baseline)
2. Make your code changes
3. Build: `dotnet build` (check for compilation errors)
4. Run tests: `dotnet test` (verify all 30 tests pass)
5. For logic changes, add new tests in StepperMotorControllerTests.cs

### Common Tasks

**Adding a new motor control method:**
- Add to `StepperMotorController.cs`
- Make it async (`Task` or `Task<T>`)
- Support CancellationToken parameter
- Add unit tests using NSubstitute mocks
- Follow existing patterns (see `MoveInchesAsync`, `HomeAsync`)

**Modifying GPIO behavior:**
- Changes go in `Control/` directory
- Update IGpioController interface if needed (affects all implementations)
- Test with FakeGpioController on development machine
- Actual hardware testing requires Raspberry Pi

**GUI changes:**
- GUI code is currently commented out in Program.cs
- Uses GirCore.Gtk-4.0 for 800x480 touchscreen display
- See specs.md for GUI requirements

## Important Notes

### Do NOT:
- Remove or modify existing tests (all 30 must continue to pass)
- Change the .csproj target framework from net10.0 without testing
- Modify GPIO pin defaults without understanding hardware constraints
- Skip `dotnet restore` after `dotnet clean`

### DO:
- Always run `dotnet restore` before building after clean
- Keep async methods with CancellationToken support
- Maintain thread safety for position tracking
- Use the FakeGpioController for development/testing on non-Pi platforms
- Follow the existing David Austin algorithm pattern for motion control
- Add unit tests for new motor control functionality

## File Location Quick Reference

- Main controller logic: `MotorController/Control/StepperMotorController.cs`
- Configuration: `MotorController/Control/ControllerConfig.cs`
- GPIO abstraction: `MotorController/Control/GpioControllerWrapper.cs`
- Testing mock: `MotorController/Control/FakeGpioController.cs`
- Unit tests: `Test/StepperMotorControllerTests.cs`
- Application entry: `MotorController/Program.cs`
- Requirements: `MotorController/specs.md`

## Validation Checklist

Before submitting changes:
- [ ] `dotnet restore` completes successfully
- [ ] `dotnet build` succeeds with no errors (1 warning is expected)
- [ ] `dotnet test` shows all 30 tests passing
- [ ] No new nullable reference warnings introduced
- [ ] Async methods maintain CancellationToken support
- [ ] Changes maintain thread safety for shared state

---

**Trust these instructions**: The commands and patterns documented here have been validated. Only search for additional information if these instructions are incomplete or incorrect for your specific task.
