# MotorController Repository - Copilot Instructions

## Repository Overview

**Purpose**: C# stepper motor controller library for Raspberry Pi with GTK-based GUI. Controls stepper motors via GPIO pins with acceleration/deceleration profiles using the David Austin algorithm.

**Project Type**: .NET 10.0 class library with separate GTK UI application  
**Size**: ~20MB, comprehensive motor control implementation  
**Target Platform**: Raspberry Pi (cross-platform development supported via FakeGpioController)  
**Namespace**: All code uses `MotorControllerApp` namespace

**Architecture**: Solution contains 3 projects:
- **MotorController** - Core library with motor control logic (class library)
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
- `IStepperMotorController.cs` - Public interface for motor controller (~75 lines with XML docs)
- `StepperMotorController.cs` - Main controller with David Austin algorithm (~492 lines)
- `LinearAxixConfig.cs` - Configuration settings class with XML documentation (~64 lines)
  - Includes `StepsPerRevolution` enum with values: 200, 400, 800, 1600, 3200, 6400, 12800
- `LimitSwitch.cs` - Enum specifying limit switch direction (Min/Max) (~18 lines)
- `GpioControllerWrapper.cs` - Contains:
  - `IGpioController` interface (GPIO abstraction for testing)
  - `GpioControllerWrapper` class (real GPIO implementation) (~43 lines)
- `FakeGpioController.cs` - Mock GPIO with console output and simulated callbacks (~138 lines)
- Exposes internals to Test project via `InternalsVisibleTo`

**UI/** - GTK GUI application project (executable)
- `UI.csproj` - Console application project file targeting net10.0
- `Program.cs` - Entry point with dependency injection setup (~92 lines)
  - Uses Microsoft.Extensions.Hosting for dependency injection
  - Platform-specific GPIO controller selection (Raspberry Pi detection)
  - GTK application initialization and CSS loading
- `MotorControlUI.cs` - Main UI controller class (~517 lines)
  - Contains all UI layout and interaction logic
  - Status display with current speed and position
  - Control buttons and limit switch indicators
  - Event subscriptions to motor controller
- `appsettings.json` - Configuration file with ControllerConfig values
- `Widgets/Keypad.cs` - Custom keypad widget for numeric input (~149 lines)
- `Widgets/SettingsDialog.cs` - Settings dialog for configuration editing (~290 lines)
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
- **IStepperMotorController**: Public interface defining motor controller contract
  - All public methods and properties
  - Events for limit switch changes
  - Implements IDisposable

- **StepperMotorController**: Main class implementing IStepperMotorController
  - **Constructor**: Requires `IGpioController`, `ControllerConfig`, and `ILogger<StepperMotorController>`
  - **Key Methods**:
    - `MoveInchesAsync(double inches, double rpm, CancellationToken)` - Move specified distance
    - `RunToLimitSwitchAsync(LimitSwitch direction, double rpm, CancellationToken)` - Run until limit switch triggered (uses Task.Run internally)
    - `StopAsync()` - Sets `_stopRequested` flag to trigger graceful stop with deceleration
    - `ResetPositionAsync()` - Reset current position to zero (thread-safe)
    - `SetTargetSpeed(double rpm)` - Dynamically adjust speed during motion
    - `ExecuteMotionInternalAsync(...)` - Internal motion execution with acceleration/deceleration
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
  
- **Platform-specific selection** in UI/Program.cs using `OperatingSystem.IsWindows()`

### Key Configuration
- **ControllerConfig class** with XML documentation comments:
  - Default pins: Pulse=21, Direction=20, MinLimit=23, MaxLimit=24
  - StepsPerRevolution: Enum with default SPR_400 (values: 200, 400, 800, 1600, 3200, 6400, 12800)
  - Lead screw: 5 threads per inch (default)
  - Acceleration: 5000 steps/sec² (default)
  - EnablePin: Optional (nullable int)

- **LimitSwitch enum**: Specifies direction for RunToLimitSwitchAsync
  - `Min` - Move toward minimum limit switch
  - `Max` - Move toward maximum limit switch

- **Configuration in UI**:
  - Uses `appsettings.json` for runtime configuration
  - Integrated with Microsoft.Extensions.Configuration
  - ControllerConfig bound from "ControllerConfig" section
  - Injected via dependency injection with `IOptions<ControllerConfig>`

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
  - Catch `OperationCanceledException` for emergency/hard cancellation (explicit token cancellation)
  - Normal stop via StopAsync() does NOT throw exception (graceful deceleration completes normally)
  - Always disable motor in finally block

### UI Design
- **GTK 4.0 based** using GirCore bindings
- **Target Display**: 800x480 touchscreen (decorated, non-resizable window)
- **Dependency Injection**: Uses Microsoft.Extensions.Hosting with configured services
  - Platform-specific GPIO controller selection (Windows = FakeGpioController, Linux = GpioControllerWrapper)
  - IStepperMotorController registered as singleton
  - ControllerConfig bound from appsettings.json

- **Main Components** (in `MotorControlUI.cs`):
  - Status display: Current speed (RPM) and position (inches) with 100ms timer update
  - Limit switch indicators: DrawingArea widgets (red = triggered, green = released)
  - Control buttons: Move to Position, Stop, Min/Max Limit, Settings, Power
  - Input fields: Speed (RPM: 0-350) and position (inches) with touch-friendly keypad
  - Settings dialog: Full configuration editor with dropdown for StepsPerRevolution enum and slider for Acceleration

- **Stop Button Behavior**:
  - Calls `StopAsync()` only (does NOT cancel CancellationToken)
  - Triggers graceful deceleration in motor controller
  - Motor decelerates smoothly before stopping
  - CancellationToken cancellation is reserved for emergency stops only

- **Keypad Widget** (`UI/Widgets/Keypad.cs`):
  - Custom Grid-based numeric keypad (0-9, backspace, confirm, cancel)
  - Shown in modal dialog when entry fields are clicked
  - Validates input ranges for speed and position
  - Designed for touchscreen without physical keyboard

- **Settings Dialog** (`UI/Widgets/SettingsDialog.cs`):
  - Separate dialog class for configuration editing
  - GPIO pin configuration fields
  - StepsPerRevolution dropdown
  - LeadScrewThreadsPerInch and Acceleration sliders
  - EnablePin optional configuration
  - Warning about requiring restart for changes

- **Event-driven**: Subscribes to motor controller events for real-time limit switch updates
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
- Add to both `MotorController/IStepperMotorController.cs` interface and `MotorController/StepperMotorController.cs` implementation
- Make it async (`async Task` or `async Task<T>')
- Add `CancellationToken cancellationToken = default` parameter
- Use `CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)` for cancellation
- Check `_stopRequested` flag periodically during motion
- Wrap in try/catch for `OperationCanceledException`
- Use finally block to reset `_stopRequested` and disable motor (EnablePin HIGH)
- Acquire `_positionLock` before updating `_currentPositionSteps`
- Pass `ILogger<StepperMotorController>` to log important events
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
- GUI code is in `UI/Program.cs` and `UI/MotorControlUI.cs` using GirCore.Gtk-4.0
- Uses Microsoft.Extensions.Hosting for dependency injection and configuration
- Configuration loaded from `UI/appsettings.json`
- `Program.cs` contains entry point and dependency injection setup (~92 lines)
- `MotorControlUI` class contains all UI logic (~517 lines)
- Designed for 800x480 touchscreen display (decorated, non-resizable)
- Custom `Keypad` widget in `UI/Widgets/Keypad.cs` for numeric input (~149 lines)
- `SettingsDialog` class in `UI/Widgets/SettingsDialog.cs` for configuration editing (~290 lines)
- Settings dialog allows editing all ControllerConfig properties at runtime
- Includes limit switch indicators (DrawingArea), speed/position displays, control buttons
- Platform-specific GPIO controller selection in Program.cs using Raspberry Pi detection
- CSS styling in `UI/Styles/style.css`
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
- Update `MotorController/IStepperMotorController.cs` if interface changes are needed
- Add XML documentation comments (using `<summary>` tags)
- Use sensible defaults for Raspberry Pi GPIO pins
- If adding enum values, update `StepsPerRevolution` enum
- Update `UI/appsettings.json` to include new configuration values
- Update settings dialog in `UI/Widgets/SettingsDialog.cs` to allow editing new values
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
- Modify public interface without updating IStepperMotorController
- Change configuration structure without updating appsettings.json and UI settings dialog
- Cancel CancellationToken from UI stop button (breaks graceful deceleration)
- Check CancellationToken in while loop condition (prevents deceleration completion)

### DO:
- Always run `dotnet restore` before building after clean
- Keep async methods with `CancellationToken` parameter
- Use `CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)` for motion control
- Check `_stopRequested` flag during motion loops for graceful stop
- Check `CancellationToken` at start of loop iteration for emergency/hard stops only
- Reset `_stopRequested` to false after motion completes
- Maintain thread safety for position tracking using SemaphoreSlim
- Use FakeGpioController for development/testing on non-Pi platforms
- Follow the existing David Austin algorithm pattern for motion control
- Add unit tests for new motor control functionality using NSubstitute
- Use MicrosecondSleep for accurate pulse timing (Stopwatch-based)
- Handle limit switch events properly (active-low logic: LOW = triggered)
- Add XML documentation comments to public APIs and interfaces
- Use ILogger for logging important events (initialization, errors, state changes)
- Update IStepperMotorController interface when adding public methods
- Update appsettings.json and UI settings dialog for new configuration options
- Dispose of CancellationTokenSource and SemaphoreSlim properly
- Use `Task.Run()` for CPU-intensive motion execution loops
- Use dependency injection for services in UI application
- Call StopAsync() from UI stop button (do NOT cancel CancellationToken for normal stops)

## File Location Quick Reference

**MotorController Library:**
- Interface: `MotorController/IStepperMotorController.cs` (~75 lines, XML documented)
- Main controller: `MotorController/StepperMotorController.cs` (~492 lines)
- Configuration: `MotorController/ControllerConfig.cs` (~64 lines, XML documented, includes StepsPerRevolution enum)
- LimitSwitch enum: `MotorController/LimitSwitch.cs` (~18 lines, XML documented)
- GPIO interface + wrapper: `MotorController/GpioControllerWrapper.cs` (~43 lines)
- Testing mock: `MotorController/FakeGpioController.cs` (~138 lines)
- Library specs: `MotorController/specs.md`
- Project file: `MotorController/MotorController.csproj`

**UI Application:**
- GUI entry point: `UI/Program.cs` (~92 lines with dependency injection)
  - Includes dependency injection setup
  - Platform-specific GPIO selection (Raspberry Pi detection)
- Main UI controller: `UI/MotorControlUI.cs` (~517 lines)
  - All UI layout and interaction logic
  - Status display and control buttons
  - Event subscriptions to motor controller
- Settings dialog: `UI/Widgets/SettingsDialog.cs` (~290 lines)
  - Configuration editor for all ControllerConfig properties
- Keypad widget: `UI/Widgets/Keypad.cs` (~149 lines)
- Styles: `UI/Styles/style.css` (GTK CSS stylesheet)
- Configuration: `UI/appsettings.json` (ControllerConfig values)
- UI specs: `UI/specs.md`
- Project file: `UI/UI.csproj`

**Tests:**
- Unit tests: `Test/StepperMotorControllerTests.cs` (comprehensive unit tests)
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
- [ ] Changes maintain thread safety for shared state (_positionLock acquired)
- [ ] GPIO pin operations follow existing patterns
- [ ] Motion control incorporates acceleration/deceleration (David Austin)
- [ ] Limit switch handling works correctly (active-low logic)
- [ ] XML documentation added to public APIs and interface methods
- [ ] IStepperMotorController interface updated if public API changes
- [ ] ILogger used for important events (errors, state changes)
- [ ] UI appsettings.json updated if configuration structure changes
- [ ] Settings dialog in UI updated for new configuration options
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
- **Stop request handling**: Also checks `_stopRequested` flag for user-initiated stop
- **Deceleration trigger**: Once limit switch detected or stop requested, decelerates for up to 300 steps
- **Early termination**: Stops after deceleration phase completes
- **Position reset**: Resets position to zero when min limit switch is reached (in UI handler)

### Thread Safety Strategy
- **Position tracking**: Protected by `SemaphoreSlim _positionLock`
  - Acquired with `WaitAsync(cancellationToken)` before updates
  - Always released in finally block
  - CurrentPositionInches property uses synchronous `Wait()` / `Release()`
- **Limit switch state**: Updated via thread-safe GPIO callbacks
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
- **UI cleanup**: UI uses `using var` for motorController disposal

---

**Trust these instructions**: The commands and patterns documented here have been validated against the current codebase. Only search for additional information if these instructions are incomplete or incorrect for your specific task.