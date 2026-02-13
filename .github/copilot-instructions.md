# MotorController Repository - Copilot Instructions

## Repository Overview

**Purpose**: C# stepper motor controller library for Raspberry Pi with GTK-based GUI. Controls stepper motors via GPIO pins with acceleration/deceleration profiles using the David Austin algorithm.

**Project Type**: .NET 10.0 class library with separate GTK UI application  
**Size**: ~20MB, comprehensive motor control implementation  
**Target Platform**: Raspberry Pi (cross-platform development supported via FakeGpioController)  
**Namespace**: All code uses `MotorControllerApp` namespace

**Architecture**: Solution contains 3 projects:
- **MotorController** - Core library with motor control logic
- **UI** - GTK-based GUI application (executable)
- **Test** - XUnit test project with NSubstitute mocking

## Build Instructions

### Prerequisites
- .NET SDK 10.0.102 (project targets net10.0)
- The project builds successfully on Linux and Windows with .NET 10.0

### Build Commands (ALWAYS use this exact sequence)

**IMPORTANT**: Always run `dotnet restore` before building, especially after cloning or cleaning:

```bash
# 1. Restore dependencies (REQUIRED first step, takes ~90 seconds on first run)
dotnet restore

# 2. Build the solution (takes ~10 seconds)
dotnet build

# 3. Run tests (takes ~5 seconds, all tests should pass)
dotnet test

# 4. Clean build artifacts (if needed)
dotnet clean
```

**Note**: After running `dotnet clean`, you MUST run `dotnet restore` again before building.

### Known Build Warnings
- Test project may have nullable reference warnings - verify these do not affect functionality

### Testing
- **Test Framework**: XUnit 2.9.3 with NSubstitute 5.3.0 for mocking
- **All tests must pass**: Run `dotnet test` to execute the full test suite
- **Command**: `dotnet test` or `dotnet test Test/Test.csproj`
- Tests are comprehensive and cover constructors, limit switches, motion control, and disposal
- Tests use NSubstitute to mock IGpioController interface

### Running the Application
```bash
dotnet run --project UI/UI.csproj
```
The UI application automatically selects:
- **FakeGpioController** on Windows (console-based simulation)
- **GpioControllerWrapper** on Linux/Raspberry Pi (real GPIO hardware)

## Project Structure

### Root Directory Files
- `MotorController.sln` - Visual Studio solution file (contains 3 projects)
- `.gitignore` - Standard Visual Studio .gitignore
- `.gitattributes` - Git attributes configuration
- `.vscode/launch.json` - VS Code debug configuration
- `.github/copilot-instructions.md` - This file

### Key Directories

**MotorController/** - Core library project (class library)
- `MotorController.csproj` - Library project file targeting net10.0
- `specs.md` - Detailed specification document (read this for requirements)
- `StepperMotorController.cs` - Main controller with David Austin algorithm (~370 lines)
- `ControllerConfig.cs` - Configuration settings class with XML documentation
- `GpioControllerWrapper.cs` - Contains:
  - `IGpioController` interface (GPIO abstraction for testing)
  - `GpioControllerWrapper` class (real GPIO implementation)
- `FakeGpioController.cs` - Mock GPIO with console output and simulated callbacks
- Exposes internals to Test project via `InternalsVisibleTo`

**UI/** - GTK GUI application project (executable)
- `UI.csproj` - Console application project file targeting net10.0
- `Program.cs` - Entry point with GTK GUI implementation (~400+ lines)
- `specs.md` - UI-specific specification document
- References the MotorController library project

**Test/** - XUnit test project
- `Test.csproj` - Test project configuration
- `StepperMotorControllerTests.cs` - Comprehensive unit tests (~600+ lines)
- `specs.md` - Test-specific specification document
- References the MotorController library project

### Dependencies
**MotorController Project (Library):**
- `System.Device.Gpio` v4.1.0 (GPIO control)
- OutputType: Library
- No GUI dependencies (pure motor control logic)

**UI Project (Executable):**
- `GirCore.Gtk-4.0` v0.7.0 (GTK GUI library)
- Project reference to MotorController
- OutputType: Exe

**Test Project:**
- `Microsoft.NET.Test.Sdk` v18.0.1
- `NSubstitute` v5.3.0 (mocking framework)
- `xunit` v2.9.3
- `xunit.runner.visualstudio` v3.1.5
- `coverlet.collector` v6.0.4
- Project reference to MotorController

## Architecture & Design Patterns

### Motor Control
- **StepperMotorController**: Main class implementing IDisposable
  - **Key Methods**:
    - `MoveInchesAsync(double inches, double rpm, CancellationToken)` - Move specified distance
    - `RunToLimitSwitchAsync(bool toMaxLimit, double rpm, CancellationToken)` - Run until limit switch triggered (uses Task.Run internally)
    - `StopAsync()` - Cancel motion via internal CancellationTokenSource
    - `ResetPositionAsync()` - Reset current position to zero (thread-safe)
    - `ExecuteMotionAsync(int steps, double rpm, CancellationToken)` - Internal motion execution with acceleration/deceleration (uses Task.Run)
  - **Key Properties**:
    - `CurrentPositionInches` (double) - Thread-safe position tracking (blocks on get)
    - `IsMinLimitSwitchTriggered` (bool) - Min limit switch state (active-low)
    - `IsMaxLimitSwitchTriggered` (bool) - Max limit switch state (active-low)
  - **Events**:
    - `MinLimitSwitchTriggered` - Fired when min limit switch changes state
    - `MaxLimitSwitchTriggered` - Fired when max limit switch changes state
  
- **Acceleration Algorithm**: David Austin stepper motor algorithm for smooth linear acceleration
  - Initial delay formula: `c0 = 0.676 * sqrt(2/α) * 10^6` microseconds
  - Acceleration step delay: `cn = cn-1 - (2 * cn-1) / (4n + 1)` (decreasing delays)
  - Deceleration mirrors acceleration profile by reversing the sequence
  - Constant speed phase maintains target delay between accel/decel
  
- **Thread Safety**: 
  - `SemaphoreSlim _positionLock` protects `_currentPositionSteps` field
  - All position updates acquire lock asynchronously with `WaitAsync()`
  - CurrentPositionInches property blocks synchronously with `Wait()`
  
- **Async Methods**: All motor control methods return `Task` or `Task<T>`
  - Motion execution uses `Task.Run()` for background processing
  - Methods accept `CancellationToken` parameter
  
- **Cancellation Strategy**: 
  - Maintains internal `CancellationTokenSource _stopTokenSource` for StopAsync()
  - Creates linked CancellationTokenSource combining external token + internal stop token
  - StopAsync() cancels and recreates `_stopTokenSource` for reusability
  - Catches `OperationCanceledException` for graceful stop
  
- **Precise Timing**: 
  - `MicrosecondSleep(int microseconds)` method uses Stopwatch for accurate pulse timing
  - Tight loop comparing `Stopwatch.GetTimestamp()` against calculated ticks
  - No Thread.Sleep() calls (too imprecise for stepper motor pulses)

### GPIO Abstraction
- **IGpioController**: Interface for GPIO operations (enables testing)
  - Methods: `OpenPin`, `ClosePin`, `IsPinOpen`, `Read`, `Write`
  - Event handling: `RegisterCallbackForPinValueChangedEvent`, `UnregisterCallbackForPinValueChangedEvent`
  - Implements IDisposable
  - Located in `GpioControllerWrapper.cs` file
  
- **GpioControllerWrapper**: Production implementation wrapping System.Device.Gpio.GpioController
  - Thin wrapper delegating to real GpioController
  - Located in `GpioControllerWrapper.cs` file
  
- **FakeGpioController**: Development/testing mock with features:
  - Thread-safe dictionary `_pins` storing pin state
  - Console output for all GPIO operations (debugging)
  - Simulates callbacks when pin values change via Write()
  - `SimulatePinChange(int pinNumber, PinValue value)` utility for testing
  - Located in `FakeGpioController.cs` file
  
- **Platform-specific selection** in UI/Program.cs using `OperatingSystem.IsWindows()`

### Key Configuration
- **ControllerConfig class** with XML documentation comments:
- Default pins: Pulse=21, Direction=20, MinLimit=23, MaxLimit=24
- Steps per revolution: 400 (default for 0.9° stepper motor)
- Lead screw: 5 threads per inch (default)
- Acceleration: 5000 steps/sec² (default)
- EnablePin: Optional (nullable int)

### Limit Switches
- **Active-low configuration** with internal pull-up resistors (PinMode.InputPullUp)
- **Event-driven updates** via GPIO callbacks:
- `OnMinLimitSwitchChanged` and `OnMaxLimitSwitchChanged` handlers
- Updates `IsMinLimitSwitchTriggered` / `IsMaxLimitSwitchTriggered` properties
- Raises public events (`MinLimitSwitchTriggered`, `MaxLimitSwitchTriggered`)
- **Monitored during motion** to prevent over-travel
- **Initial state read** in `InitializePins()` method
- **Position resets to zero** when RunToLimitSwitchAsync reaches min limit switch

### Motion Control Patterns
- **Enable/Disable**: 
- Enable motor before motion (set EnablePin LOW if configured)
- Disable motor after motion in finally block (set EnablePin HIGH)
- **Direction**: Set DirectionPin before starting motion (HIGH = forward/max, LOW = reverse/min)
- **Pulse Generation**: 
- Write PulsePin HIGH, sleep half delay
- Write PulsePin LOW, sleep half delay
- Creates square wave with period = delay
- **Position Tracking**: 
- Update `_currentPositionSteps` within `_positionLock` for thread safety
- Increment/decrement based on direction
- **Exception Handling**: 
- Catch `OperationCanceledException` for graceful stop (expected behavior)
- Always disable motor in finally block

### UI Design
- **GTK 4.0 based** using GirCore bindings
- **Target Display**: 800x480 touchscreen (undecorated, non-resizable window)
- **Main Components**:
- Status display: Current speed (RPM) and position (inches)
- Limit switch indicators: Visual feedback via DrawingArea widgets
- Control buttons: Move, Stop, Min/Max limit homing, Power off
- Input fields: Speed (RPM) and position (inches) entry
- **Event-driven**: Subscribes to motor controller events for real-time updates
- **See UI/specs.md** for detailed UI requirements

## Development Workflow

### Making Changes
1. Run existing tests first: `dotnet test` (establish baseline - should show all tests passing)
2. Make your code changes in the appropriate project
3. Build: `dotnet build` (check for compilation errors)
4. Run tests: `dotnet test` (verify all tests still pass)
5. For motor control logic changes, add new tests in `StepperMotorControllerTests.cs`

### Common Tasks

**Adding a new motor control method:**
- Add to `MotorController/StepperMotorController.cs`
- Make it async (`async Task` or `async Task<T>')
- Add `CancellationToken cancellationToken = default` parameter
- Use `CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)` for cancellation
- Wrap in try/catch for `OperationCanceledException`
- Use finally block to disable motor (EnablePin HIGH)
- Acquire `_positionLock` before updating `_currentPositionSteps`
- Add unit tests using NSubstitute to mock IGpioController
- Follow existing patterns (see `MoveInchesAsync`, `RunToLimitSwitchAsync`)

**Modifying GPIO behavior:**
- Changes go in `MotorController/` project (library)
- Update `IGpioController` interface if needed (affects all implementations)
- Update `GpioControllerWrapper` to delegate to System.Device.Gpio
- Update `FakeGpioController` to simulate behavior with console output
- Test with FakeGpioController on Windows development machine
- Actual hardware testing requires Raspberry Pi with connected stepper motor

**GUI changes:**
- GUI code is in `UI/Program.cs` using GirCore.Gtk-4.0
- `MotorControlUI` class contains all UI logic
- Designed for 800x480 touchscreen display (undecorated, non-resizable)
- Includes limit switch indicators, speed/position displays, control buttons
- See `UI/specs.md` for GUI requirements and layout
- UI project references MotorController library project

**Testing changes:**
- Tests are in `Test/StepperMotorControllerTests.cs`
- Use NSubstitute's `Substitute.For<IGpioController>()` to create mocks
- Setup mock behavior with `.Returns()` method
- Verify GPIO calls with `.Received()` assertions
- Test class implements IDisposable for cleanup
- Follow existing test patterns (Arrange-Act-Assert)

**Configuration changes:**
- Modify `MotorController/ControllerConfig.cs` for new settings
- Add XML documentation comments (using `<summary>` tags)
- Use sensible defaults for Raspberry Pi GPIO pins
- Update tests to use new configuration options
- Document changes in `MotorController/specs.md`

## Important Notes

### Do NOT:
- Remove or modify existing tests without ensuring all continue to pass
- Change the .csproj target framework from net10.0 without testing all projects
- Modify GPIO pin defaults without understanding hardware constraints
- Skip `dotnet restore` after `dotnet clean`
- Block the main thread with synchronous GPIO operations (except MicrosecondSleep)
- Modify `_currentPositionSteps` without acquiring `_positionLock`
- Use Thread.Sleep() for pulse timing (too imprecise - use MicrosecondSleep)
- Change the namespace from `MotorControllerApp` without updating all projects

### DO:
- Always run `dotnet restore` before building after clean
- Keep async methods with `CancellationToken` parameter
- Use `CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)` for motion control
- Maintain thread safety for position tracking using SemaphoreSlim
- Use FakeGpioController for development/testing on non-Pi platforms
- Follow the existing David Austin algorithm pattern for motion control
- Add unit tests for new motor control functionality using NSubstitute
- Use MicrosecondSleep for accurate pulse timing (Stopwatch-based)
- Handle limit switch events properly (active-low logic: LOW = triggered)
- Add XML documentation comments to public APIs
- Dispose of CancellationTokenSource and SemaphoreSlim properly
- Use `Task.Run()` for CPU-intensive motion execution loops

## File Location Quick Reference

**MotorController Library:**
- Main controller: `MotorController/StepperMotorController.cs` (~370 lines)
- Configuration: `MotorController/ControllerConfig.cs` (~50 lines, XML documented)
- GPIO interface + wrapper: `MotorController/GpioControllerWrapper.cs` (~45 lines)
- Testing mock: `MotorController/FakeGpioController.cs` (~130 lines)
- Library specs: `MotorController/specs.md`
- Project file: `MotorController/MotorController.csproj`

**UI Application:**
- GUI entry point: `UI/Program.cs` (~400+ lines with MotorControlUI class)
- UI specs: `UI/specs.md`
- Project file: `UI/UI.csproj`

**Tests:**
- Unit tests: `Test/StepperMotorControllerTests.cs` (~600+ lines)
- Test specs: `Test/specs.md`
- Project file: `Test/Test.csproj`

**Root:**
- Solution file: `MotorController.sln`
- Onboarding: `.github/copilot-instructions.md` (this file)

## Validation Checklist

Before submitting changes:
- [ ] `dotnet restore` completes successfully
- [ ] `dotnet build` succeeds with no errors
- [ ] `dotnet test` shows all tests passing
- [ ] No new nullable reference warnings introduced
- [ ] Async methods maintain CancellationToken support
- [ ] Changes maintain thread safety for shared state (_positionLock acquired)
- [ ] GPIO pin operations follow existing patterns
- [ ] Motion control incorporates acceleration/deceleration (David Austin)
- [ ] Limit switch handling works correctly (active-low logic)
- [ ] XML documentation added to public APIs
- [ ] FakeGpioController works on Windows for testing
- [ ] All IDisposable objects properly disposed

## Key Implementation Details

### David Austin Algorithm
The acceleration/deceleration profile uses the David Austin algorithm for stepper motors:
- **Initial delay**: `c0 = 0.676 * sqrt(2/α) * 10^6` microseconds (slowest step)
- **Acceleration steps**: `n = (v²) / (2α)` where v = max speed (steps/sec), α = acceleration (steps/sec²)
- **Step delay update (acceleration)**: `cn = cn-1 - (2 * cn-1) / (4n + 1)` (delays decrease)
- **Step delay update (deceleration)**: `cn = cn-1 + (2 * cn-1) / (4n - 1)` (delays increase)
- **Deceleration**: Mirrors acceleration by reversing the delay sequence
- **Constant speed**: Maintains `targetDelayMicroseconds = 1,000,000 / maxStepsPerSecond`
- **Short motion handling**: If total steps < accel+decel, reduces both proportionally

### RunToLimitSwitchAsync Special Handling
- **Deceleration limit**: Max 300 steps of deceleration to prevent overshoot
- **Limit switch detection**: Checks `IsMinLimitSwitchTriggered` / `IsMaxLimitSwitchTriggered` every step
- **Deceleration trigger**: Once limit switch detected, decelerates for up to 300 steps
- **Early termination**: Stops after deceleration phase completes

### Thread Safety Strategy
- **Position tracking**: Protected by `SemaphoreSlim _positionLock`
- Acquired with `WaitAsync(cancellationToken)` before updates
- Always released in finally block
- CurrentPositionInches property uses synchronous `Wait()` / `Release()`
- **Limit switch state**: Updated via thread-safe GPIO callbacks
- `IsMinLimitSwitchTriggered` / `IsMaxLimitSwitchTriggered` written from callback thread
- Read from motion control loop thread
- **Motion cancellation**: Coordinated using CancellationTokenSource
- `_stopTokenSource` field for internal stop mechanism
- Linked with external cancellation token using `CreateLinkedTokenSource()`

### Testing Strategy
- **Mock GPIO operations**: Use NSubstitute's `Substitute.For<IGpioController>()`
- **Setup default behavior**: Mock Read() to return PinValue.High (limit switches not triggered)
- **Verify pulse counts**: Use `.Received(count)` to match expected motion
- **Test acceleration calculations**: Verify timing and step counts
- **Validate limit switch behavior**: Simulate PinValueChangedEventArgs events
- **Test cancellation**: Verify OperationCanceledException handling
- **Test disposal**: Ensure proper cleanup of resources
- **Thread safety**: Tests verifyposition updates are atomic

### MicrosecondSleep Implementation
- **Purpose**: Achieve microsecond-precision delays for stepper motor pulses
- **Method**: Tight loop using `Stopwatch.GetTimestamp()` for high-resolution timing
- **Calculation**: Convert microseconds to ticks using `Stopwatch.Frequency`
- **Blocking**: Spins on CPU without yielding (necessary for precision)
- **Usage**: Called twice per step (HIGH half-period, LOW half-period)

### Disposal Pattern
- **IDisposable implementation**: Proper cleanup of unmanaged resources
- **Disposed flag**: `_disposed` field prevents double disposal
- **Resource cleanup order**:
1. Cancel and dispose `_stopTokenSource`
2. Dispose `_positionLock` semaphore
3. Dispose `_gpio` controller (closes all pins)
- **Test cleanup**: Tests implement IDisposable to call controller.Dispose()

---

**Trust these instructions**: The commands and patterns documented here have been validated against the current codebase. Only search for additional information if these instructions are incomplete or incorrect for your specific task.