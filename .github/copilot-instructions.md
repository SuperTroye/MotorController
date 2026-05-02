# MotorController Repository - Copilot Instructions

## Repository Overview

**Purpose**: C# stepper motor controller library for Raspberry Pi with GTK-based GUI. Controls two synchronized stepper motors (a linear axis and a rotary axis) via GPIO pins with acceleration/deceleration profiles using the David Austin algorithm.

**Project Type**: .NET 10.0 class library with separate GTK UI application  
**Size**: ~20MB, comprehensive motor control implementation  
**Target Platform**: Raspberry Pi (cross-platform development supported via FakeGpioController)  
**Namespace**: All code uses `MotorControllerApp` namespace

**Architecture**: Solution contains 3 projects:
- **MotorController** - Core library with synchronized dual-axis motor control logic (class library)
- **UI** - GTK-based GUI application with configuration screen and keypad widget (console executable)
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
- `.github/copilot-instructions.md` - This file (onboarding documentation)

### Key Directories

**MotorController/** - Core library project (class library)
- `MotorController.csproj` - Library project file targeting net10.0
- `specs.md` - Detailed specification document (read this for requirements)
- `ISynchronizedDualAxisController.cs` - Public interface for the dual-axis controller (XML documented)
- `SynchronizedDualAxisController.cs` - Main controller implementing both linear + rotary axis motion with David Austin algorithm and DDA synchronization
- `Config/IBaseConfig.cs` - Base interface shared by linear and rotary axis configs
- `Config/LinearAxisConfig.cs` - Configuration for the linear axis stepper motor (XML documented)
  - Includes `StepsPerRevolution` enum with values: 200, 400, 800, 1600, 3200, 6400, 12800
  - Properties: PulsePin, DirectionPin, MinLimitSwitchPin, MaxLimitSwitchPin, EnablePin, StepsPerRevolution, LeadScrewThreadsPerInch, Acceleration
- `Config/RotaryAxisConfig.cs` - Configuration for the rotary axis stepper motor (no limit switches)
  - Properties: PulsePin, DirectionPin, EnablePin, StepsPerRevolution
- `Config/SynchronizedDualAxisConfig.cs` - Top-level config combining both axes and GearRatio
- `LimitSwitch.cs` - Enum specifying limit switch direction (Min/Max)
- `GpioControllerWrapper.cs` - Contains:
  - `IGpioController` interface (GPIO abstraction for testing)
  - `GpioControllerWrapper` class (real GPIO implementation)
- `FakeGpioController.cs` - Mock GPIO with console output and simulated callbacks
- Exposes internals to Test project via `InternalsVisibleTo`

**UI/** - GTK GUI application project (executable)
- `UI.csproj` - Console application project file targeting net10.0
- `Program.cs` - Entry point with dependency injection setup
  - Uses Microsoft.Extensions.Hosting for dependency injection
  - Registers `LinearAxisConfig` and `RotaryAxisConfig` from `appsettings.json` sections
  - Builds `SynchronizedDualAxisConfig` from registered configs
  - Registers `ISynchronizedDualAxisController` as `SynchronizedDualAxisController` singleton
  - Platform-specific GPIO controller selection (Raspberry Pi detection via `/proc/device-tree/model`)
  - GTK application initialization and CSS loading
- `MotorControlUI.cs` - Main UI controller class
  - Receives `ISynchronizedDualAxisController` and `SynchronizedDualAxisConfig`
  - Status display with current speed (RPM) and linear position (inches) with 100ms timer update
  - Control buttons: Move to Position, Stop, Min/Max Limit, increment/decrement speed
  - Limit switch indicators: DrawingArea widgets (red = triggered, green = released)
  - Speed slider (5–350 RPM) with real-time `SetTargetSpeed` updates
  - Position entry with touch-friendly keypad dialog
  - Event subscriptions to `MinLimitSwitchTriggered` / `MaxLimitSwitchTriggered`
- `appsettings.json` - Configuration file with `LinearAxisConfig` and `RotaryAxisConfig` sections
- `Widgets/Keypad.cs` - Custom keypad widget for numeric input
- `Styles/style.css` - GTK CSS stylesheet for UI theming
- `specs.md` - UI-specific specification document
- References the MotorController library project

**Test/** - XUnit test project
- `Test.csproj` - Test project configuration
- `StepperMotorControllerTests.cs` - Comprehensive unit tests
- `specs.md` - Test-specific specification document
- References the MotorController library project

### Dependencies
**MotorController Project (Library):**
- `System.Device.Gpio` v4.1.0 (GPIO control)
- `Microsoft.Extensions.Logging` v10.0.3 (logging abstraction)
- OutputType: Library
- No GUI dependencies (pure motor control logic)
- Exposes internals to Test project via `InternalsVisibleTo`

**UI Project (Executable):**
- `GirCore.Gtk-4.0` v0.7.0 (GTK GUI library)
- `Microsoft.Extensions.Hosting` v10.0.3 (dependency injection and configuration)
- `Microsoft.Extensions.Logging.Console` v10.0.3 (console logging)
- `appsettings.json` - Configuration file with ControllerConfig values
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
- **ISynchronizedDualAxisController**: Public interface defining the dual-axis controller contract
  - Extends `IDisposable`
  - All public methods and properties for both axes
  - Events for limit switch changes (linear axis only)

- **SynchronizedDualAxisController**: Main class implementing `ISynchronizedDualAxisController`
  - **Constructor**: Requires `IGpioController`, `SynchronizedDualAxisConfig`, and `ILogger<SynchronizedDualAxisController>`
  - **Key Methods**:
    - `MoveInchesAsync(double inches, double rpm, CancellationToken)` - Move linear axis (and synchronized rotary) by distance
    - `RunToLimitSwitchAsync(LimitSwitch direction, double rpm, CancellationToken)` - Run until linear limit switch triggered
    - `StopAsync()` - Sets `_stopRequested` flag to trigger graceful stop with deceleration
    - `ResetPositionAsync()` - Reset linear position to zero (thread-safe)
    - `SetTargetSpeed(double rpm)` - Dynamically adjust speed during constant-speed phase
    - `ExecuteMotionAsync(...)` - Internal fixed-step motion (public for testing)
    - `ExecuteMotionInternalAsync(...)` - Unified motion engine for both axes
  - **Key Properties**:
    - `CurrentPositionInches` (double) - Thread-safe linear position tracking (blocks on get)
    - `CurrentRotaryPositionDegrees` (double) - Thread-safe rotary position tracking (blocks on get)
    - `IsMinLimitSwitchTriggered` (bool) - Min limit switch state (active-low, linear axis)
    - `IsMaxLimitSwitchTriggered` (bool) - Max limit switch state (active-low, linear axis)
  - **Events**:
    - `MinLimitSwitchTriggered` - Fired when min limit switch changes state
    - `MaxLimitSwitchTriggered` - Fired when max limit switch changes state

- **Dual-Axis Synchronization (DDA)**:
  - The rotary axis is synchronized to the linear axis using a DDA (Digital Differential Analyzer) accumulator
  - `rotaryStepsPerLinearStep = GearRatio × RotarySPR / LinearSPR`
  - Sub-step residuals are carried forward in the accumulator — zero long-term drift
  - Both axes share the same David Austin delay value at every iteration
  - Rotary pulses are fired within the same step period using stopwatch-anchored timing so linear pulse timing is never disturbed
  - Rotary direction is inverted relative to linear direction (by default)
  - Rotary axis has no limit switches (rotates continuously)

- **Acceleration Algorithm**: David Austin stepper motor algorithm for smooth linear acceleration
  - Initial delay formula: `c0 = 0.676 * sqrt(2/α) * 10^6` microseconds
  - Acceleration step delay: `cn = cn-1 - (2 * cn-1) / (4n + 1)` (decreasing delays)
  - Deceleration mirrors acceleration profile by reversing the sequence
  - Constant speed phase maintains target delay between accel/decel
  - Acceleration is based on the linear axis config (`Acceleration` in steps/sec²)

- **Motion Lock**:
  - `SemaphoreSlim _motionLock` serializes motion commands (only one motion at a time)
  - Acquired at the top of `MoveInchesAsync` and `RunToLimitSwitchAsync`
  - Released in finally block after motion completes

- **Thread Safety**: 
  - `SemaphoreSlim _positionLock` protects `_currentLinearPositionSteps`
  - `SemaphoreSlim _rotaryPositionLock` protects `_currentRotaryPositionSteps`
  - All position updates acquire lock asynchronously with `WaitAsync()`
  - `CurrentPositionInches` and `CurrentRotaryPositionDegrees` block synchronously with `Wait()`

- **Async Methods**: All motor control methods return `Task` or `Task<T>`
  - Motion execution uses `Task.Run()` for background processing
  - Methods accept `CancellationToken` parameter

- **Cancellation Strategy**:
  - Uses `volatile bool _stopRequested` flag for StopAsync() coordination
  - StopAsync() sets `_stopRequested` to true, causing running motion to decelerate gracefully
  - Creates linked CancellationTokenSource combining external token + internal `_stopTokenSource`
  - Motion loop checks `_stopRequested` flag and transitions to deceleration when detected
  - Motion loop checks `CancellationToken` at start of each iteration for emergency/hard stops only
  - Normal stop button press does NOT cancel the external token (preserves deceleration)
  - Resets `_stopRequested` to false after motion completes
  - Throws `OperationCanceledException` only on explicit token cancellation (emergency stop)

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
  
- **Platform-specific selection** in UI/Program.cs using `/proc/device-tree/model` to detect Raspberry Pi (not `OperatingSystem.IsWindows()`)

### Key Configuration
- **IBaseConfig interface**: Shared base for both axis configs
  - Properties: PulsePin, DirectionPin, EnablePin, StepsPerRevolution

- **LinearAxisConfig** (implements `IBaseConfig`) with XML documentation:
  - Default pins: Pulse=21, Direction=20, MinLimit=24, MaxLimit=23
  - StepsPerRevolution: Enum with default SPR_400 (values: 200, 400, 800, 1600, 3200, 6400, 12800)
  - Lead screw: 5 threads per inch (default)
  - Acceleration: 5000 steps/sec² (default)
  - EnablePin: Optional (nullable int)

- **RotaryAxisConfig** (implements `IBaseConfig`) with XML documentation:
  - Default pins: Pulse=16, Direction=12
  - StepsPerRevolution: Enum with default SPR_400
  - EnablePin: Optional (nullable int)
  - No limit switch pins (rotary axis has no limit switches)

- **SynchronizedDualAxisConfig**:
  - Contains `LinearAxisConfig` and `RotaryAxisConfig`
  - `GearRatio` (double, default 0.4) — rotary rotations per linear axis revolution

- **LimitSwitch enum**: Specifies direction for RunToLimitSwitchAsync
  - `Min` - Move toward minimum limit switch
  - `Max` - Move toward maximum limit switch

- **Configuration in UI**:
  - Uses `appsettings.json` for runtime configuration
  - `LinearAxisConfig` bound from `"LinearAxisConfig"` section
  - `RotaryAxisConfig` bound from `"RotaryAxisConfig"` section
  - `SynchronizedDualAxisConfig` built from registered singletons in DI
  - GearRatio hardcoded to 0.4 in `Program.cs` (not from appsettings)

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
  - `EnableMotors()` enables both linear and rotary motors before motion (set EnablePin LOW if configured)
  - `DisableMotors()` disables both motors after motion in finally block (set EnablePin HIGH)
- **Direction**: 
  - `SetLinearDirection(bool forward)` — HIGH = reverse, LOW = forward
  - `SetRotaryDirection(bool forward)` — inverted from linear (HIGH = forward, LOW = reverse)
  - Both directions set before starting motion
- **Pulse Generation**: 
  - Write PulsePin HIGH, sleep half delay
  - Write PulsePin LOW, sleep half delay
  - Creates square wave with period = delay
  - Applies independently to both linear and rotary pulse pins within each step period
- **Position Tracking**: 
  - Update `_currentLinearPositionSteps` within `_positionLock` for thread safety
  - Update `_currentRotaryPositionSteps` within `_rotaryPositionLock` for thread safety
  - Increment/decrement based on direction
- **Exception Handling**: 
  - Catch `OperationCanceledException` in public methods for emergency/hard cancellation
  - Normal stop via StopAsync() does NOT throw exception (graceful deceleration completes normally)
  - Always disable both motors in finally block

### UI Design
- **GTK 4.0 based** using GirCore bindings
- **Target Display**: 800x440 touchscreen (decorated, non-resizable window)
- **Dependency Injection**: Uses Microsoft.Extensions.Hosting with configured services
  - Platform-specific GPIO controller selection (Raspberry Pi detection = GpioControllerWrapper, otherwise FakeGpioController)
  - `ISynchronizedDualAxisController` registered as singleton (`SynchronizedDualAxisController`)
  - `LinearAxisConfig` and `RotaryAxisConfig` bound from appsettings.json
  - `SynchronizedDualAxisConfig` assembled in DI with GearRatio=0.4

- **Main Components** (in `MotorControlUI.cs`):
  - Accepts `ISynchronizedDualAxisController` and `SynchronizedDualAxisConfig`
  - Status display: Current speed (RPM) and linear position (inches) with 100ms timer update
  - Limit switch indicators: DrawingArea widgets (red = triggered, green = released)
  - Control buttons: Move to Position, Stop, Min/Max Limit
  - Fine-adjust buttons: ▲/▼ increment/decrement speed by 1 RPM
  - Speed slider (5–350 RPM) with real-time `SetTargetSpeed()` calls during motion
  - Position entry (inches) with touch-friendly keypad modal

- **Stop Button Behavior**:
  - Calls `StopAsync()` only (does NOT cancel CancellationToken)
  - Triggers graceful deceleration in motor controller
  - Motor decelerates smoothly before stopping
  - CancellationToken cancellation is reserved for emergency stops only

- **Keypad Widget** (`UI/Widgets/Keypad.cs`):
  - Custom Grid-based numeric keypad (0-9, backspace, confirm, cancel)
  - Shown in modal dialog when position entry field is clicked
  - Validates numeric input
  - Designed for touchscreen without physical keyboard

- **Event-driven**: Subscribes to `MinLimitSwitchTriggered` / `MaxLimitSwitchTriggered` events for real-time indicator updates
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
- Add to both `MotorController/ISynchronizedDualAxisController.cs` interface and `MotorController/SynchronizedDualAxisController.cs` implementation
- Make it async (`async Task` or `async Task<T>`)
- Add `CancellationToken cancellationToken = default` parameter
- Acquire `_motionLock` at the top using `await _motionLock.WaitAsync(cancellationToken)`
- Call `EnableMotors()` before motion and `DisableMotors()` in finally block
- Use `ExecuteMotionInternalAsync(...)` for the actual step-generation loop
- Check `_stopRequested` flag during motion for graceful stop
- Acquire `_positionLock` / `_rotaryPositionLock` before updating position fields
- Use `ILogger<SynchronizedDualAxisController>` to log important events
- Add unit tests using NSubstitute to mock `IGpioController`
- Follow existing patterns (see `MoveInchesAsync`, `RunToLimitSwitchAsync`)

**Modifying GPIO behavior:**
- Changes go in `MotorController/` project (library)
- Update `IGpioController` interface if needed (affects all implementations)
- Update `GpioControllerWrapper` to delegate to System.Device.Gpio
- Update `FakeGpioController` to simulate behavior with console output
- Test with FakeGpioController on non-Pi development machine
- Actual hardware testing requires Raspberry Pi with connected stepper motors

**GUI changes:**
- GUI code is in `UI/Program.cs` and `UI/MotorControlUI.cs` using GirCore.Gtk-4.0
- Uses Microsoft.Extensions.Hosting for dependency injection and configuration
- Configuration loaded from `UI/appsettings.json` (`LinearAxisConfig` and `RotaryAxisConfig` sections)
- `Program.cs` contains entry point and dependency injection setup
- `MotorControlUI` class contains all UI logic and receives `ISynchronizedDualAxisController`
- Designed for 800x440 touchscreen display (decorated, non-resizable)
- Custom `Keypad` widget in `UI/Widgets/Keypad.cs` for numeric input
- Platform-specific GPIO controller selection in Program.cs using Raspberry Pi detection
- CSS styling in `UI/Styles/style.css`
- See `UI/specs.md` for GUI requirements and layout
- UI project references MotorController library project

**Testing changes:**
- Tests are in `Test/StepperMotorControllerTests.cs`
- Use NSubstitute's `Substitute.For<IGpioController>()` to create mocks
- Create `SynchronizedDualAxisConfig` with `LinearAxisConfig` (and optionally `RotaryAxisConfig`)
- Setup mock behavior with `.Returns()` method
- Verify GPIO calls with `.Received()` assertions
- Test class implements IDisposable for cleanup
- Follow existing test patterns (Arrange-Act-Assert)

**Configuration changes:**
- Modify `MotorController/Config/LinearAxisConfig.cs` or `MotorController/Config/RotaryAxisConfig.cs`
- Update `IBaseConfig` if the change applies to both axes
- Update `SynchronizedDualAxisConfig` if a new cross-axis setting is needed
- Update `MotorController/ISynchronizedDualAxisController.cs` if interface changes are needed
- Add XML documentation comments (using `<summary>` tags)
- Use sensible defaults for Raspberry Pi GPIO pins
- If adding enum values, update `StepsPerRevolution` enum in `LinearAxisConfig.cs`
- Update `UI/appsettings.json` to include new configuration values
- Update tests to use new configuration options
- Document changes in `MotorController/specs.md`

## Important Notes

### Do NOT:
- Remove or modify existing tests without ensuring all continue to pass
- Change the .csproj target framework from net10.0 without testing all projects
- Modify GPIO pin defaults without understanding hardware constraints
- Skip `dotnet restore` after `dotnet clean`
- Block the main thread with synchronous GPIO operations (except MicrosecondSleep)
- Modify `_currentLinearPositionSteps` without acquiring `_positionLock`
- Modify `_currentRotaryPositionSteps` without acquiring `_rotaryPositionLock`
- Use Thread.Sleep() for pulse timing (too imprecise - use MicrosecondSleep)
- Change the namespace from `MotorControllerApp` without updating all projects
- Modify public interface without updating `ISynchronizedDualAxisController`
- Change configuration structure without updating appsettings.json
- Cancel CancellationToken from UI stop button (breaks graceful deceleration)
- Check CancellationToken in while loop condition (prevents deceleration completion)
- Bypass `_motionLock` when starting a motion command

### DO:
- Always run `dotnet restore` before building after clean
- Keep async methods with `CancellationToken` parameter
- Use `CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)` for motion control
- Acquire `_motionLock` before executing any motion command
- Check `_stopRequested` flag during motion loops for graceful stop
- Check `CancellationToken` at start of loop iteration for emergency/hard stops only
- Reset `_stopRequested` to false after motion completes
- Maintain thread safety for position tracking using SemaphoreSlim (`_positionLock` and `_rotaryPositionLock`)
- Use FakeGpioController for development/testing on non-Pi platforms
- Follow the existing David Austin algorithm + DDA synchronization pattern for motion control
- Add unit tests for new motor control functionality using NSubstitute
- Use MicrosecondSleep for accurate pulse timing (Stopwatch-based)
- Handle limit switch events properly (active-low logic: LOW = triggered) — linear axis only
- Add XML documentation comments to public APIs and interfaces
- Use ILogger for logging important events (initialization, errors, state changes)
- Update `ISynchronizedDualAxisController` interface when adding public methods
- Update appsettings.json for new configuration options
- Dispose of CancellationTokenSource and SemaphoreSlim properly (both position locks and motion lock)
- Use `Task.Run()` for CPU-intensive motion execution loops
- Use dependency injection for services in UI application
- Call StopAsync() from UI stop button (do NOT cancel CancellationToken for normal stops)
- Call EnableMotors()/DisableMotors() to control both axes simultaneously

## File Location Quick Reference

**MotorController Library:**
- Interface: `MotorController/ISynchronizedDualAxisController.cs` (XML documented)
- Main controller: `MotorController/SynchronizedDualAxisController.cs`
- Base config interface: `MotorController/Config/IBaseConfig.cs`
- Linear axis config: `MotorController/Config/LinearAxisConfig.cs` (XML documented, includes StepsPerRevolution enum)
- Rotary axis config: `MotorController/Config/RotaryAxisConfig.cs` (XML documented)
- Dual-axis config: `MotorController/Config/SynchronizedDualAxisConfig.cs` (XML documented)
- LimitSwitch enum: `MotorController/LimitSwitch.cs` (XML documented)
- GPIO interface + wrapper: `MotorController/GpioControllerWrapper.cs`
- Testing mock: `MotorController/FakeGpioController.cs`
- Library specs: `MotorController/specs.md`
- Project file: `MotorController/MotorController.csproj`

**UI Application:**
- GUI entry point: `UI/Program.cs`
  - Dependency injection setup for both axis configs and dual-axis controller
  - Platform-specific GPIO selection (Raspberry Pi detection)
- Main UI controller: `UI/MotorControlUI.cs`
  - All UI layout and interaction logic
  - Status display and control buttons
  - Event subscriptions to `ISynchronizedDualAxisController`
- Keypad widget: `UI/Widgets/Keypad.cs`
- Styles: `UI/Styles/style.css` (GTK CSS stylesheet)
- Configuration: `UI/appsettings.json` (`LinearAxisConfig` and `RotaryAxisConfig` sections)
- UI specs: `UI/specs.md`
- Project file: `UI/UI.csproj`

**Tests:**
- Unit tests: `Test/StepperMotorControllerTests.cs` (comprehensive unit tests for `SynchronizedDualAxisController`)
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
- [ ] Motion loops check `_stopRequested` flag and reset it after completion
- [ ] Motion loops check CancellationToken at start of iteration for emergency stops
- [ ] UI stop button calls StopAsync() without canceling token
- [ ] Changes maintain thread safety for shared state (`_positionLock` and `_rotaryPositionLock` acquired)
- [ ] GPIO pin operations follow existing patterns
- [ ] Motion control incorporates acceleration/deceleration (David Austin)
- [ ] Limit switch handling works correctly (active-low logic)
- [ ] XML documentation added to public APIs and interface methods
- [ ] `ISynchronizedDualAxisController` interface updated if public API changes
- [ ] ILogger used for important events (errors, state changes)
- [ ] UI appsettings.json updated if configuration structure changes (`LinearAxisConfig` / `RotaryAxisConfig` sections)
- [ ] FakeGpioController works on non-Pi platforms for testing
- [ ] All IDisposable objects properly disposed
- [ ] Both axes (linear + rotary) initialized and disposed correctly
- [ ] DDA accumulator used for rotary synchronization (no drift)

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
- **Limit switch detection**: Checks `IsMinLimitSwitchTriggered` / `IsMaxLimitSwitchTriggered` every step (linear axis)
- **Stop request handling**: Also checks `_stopRequested` flag for user-initiated stop
- **Deceleration trigger**: Once limit switch detected or stop requested, decelerates for up to 300 steps
- **Early termination**: Stops after deceleration phase completes
- **Position reset**: Resets position to zero when min limit switch is reached (in UI handler)
- **`shouldStartDeceleration` delegate**: Passed to `ExecuteMotionInternalAsync` to detect limit switch during run

### DDA Rotary Synchronization
- **Accumulator formula**: `rotaryStepsPerLinearStep = GearRatio × RotarySPR / LinearSPR`
- **Per-step logic**: Add `rotaryStepsPerLinearStep` to `rotaryAccumulator`; fire one rotary pulse for each whole unit accumulated
- **Zero long-term drift**: Sub-step residual carried forward every linear step
- **Timing**: Rotary pulses are inserted within the current linear step period using stopwatch-anchored timing
- **Linear timing unaffected**: The linear pulse delay is never disturbed by rotary pulse insertion
- **Direction**: Rotary direction is inverted relative to linear (set via `SetRotaryDirection`)

### Thread Safety Strategy
- **Linear position tracking**: Protected by `SemaphoreSlim _positionLock`
  - Acquired with `WaitAsync(cancellationToken)` before updates
  - Always released in finally block
  - `CurrentPositionInches` property uses synchronous `Wait()` / `Release()`
- **Rotary position tracking**: Protected by `SemaphoreSlim _rotaryPositionLock`
  - Same pattern as linear position lock
  - `CurrentRotaryPositionDegrees` property uses synchronous `Wait()` / `Release()`
- **Motion serialization**: `SemaphoreSlim _motionLock` prevents concurrent motion commands
  - Acquired at top of `MoveInchesAsync` and `RunToLimitSwitchAsync`
  - Released in finally block after motion completes
- **Limit switch state**: Updated via thread-safe GPIO callbacks (linear axis only)
  - `IsMinLimitSwitchTriggered` / `IsMaxLimitSwitchTriggered` written from callback thread
  - Read from motion control loop thread
- **Motion cancellation**: Two-tier cancellation mechanism
  - `_stopRequested` flag for graceful stops (user stop button) - allows deceleration
  - `CancellationToken` for emergency/hard stops - throws immediately, no deceleration
  - `_stopTokenSource` field for internal stop mechanism
  - Linked with external cancellation token using `CreateLinkedTokenSource()`
  - Motion loop checks token at start of iteration, checks `_stopRequested` during iteration

### Testing Strategy
- **Mock GPIO operations**: Use NSubstitute's `Substitute.For<IGpioController>()`
- **Create config**: Use `SynchronizedDualAxisConfig` with `LinearAxisConfig` (and optionally `RotaryAxisConfig`)
- **Setup default behavior**: Mock Read() to return PinValue.High (limit switches not triggered)
- **Verify pulse counts**: Use `.Received(count)` to match expected motion on both linear and rotary pulse pins
- **Test acceleration calculations**: Verify timing and step counts
- **Validate limit switch behavior**: Simulate PinValueChangedEventArgs events
- **Test cancellation**: Verify OperationCanceledException handling
- **Test disposal**: Ensure proper cleanup of resources including both position locks and motion lock
- **Thread safety**: Tests verify position updates are atomic

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
  3. Dispose `_rotaryPositionLock` semaphore
  4. Dispose `_motionLock` semaphore
  5. Dispose `_gpio` controller (closes all pins)
- **Test cleanup**: Tests implement IDisposable to call controller.Dispose()
- **UI cleanup**: UI uses `using var` for motorController disposal

---

**Trust these instructions**: The commands and patterns documented here have been validated against the current codebase. Only search for additional information if these instructions are incomplete or incorrect for your specific task.