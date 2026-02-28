# StepperMotorController Class Specification

## 1. Overview

### 1.1 Purpose
The `StepperMotorController` class provides precise control of stepper motors on Raspberry Pi using GPIO pins. It implements smooth acceleration and deceleration profiles using the David Austin algorithm, supports real-time speed adjustment, and provides thread-safe position tracking.

### 1.2 Target Platform
- **Primary**: Raspberry Pi (Linux ARM)
- **Development**: Windows/Linux x64 via `FakeGpioController`
- **Framework**: .NET 10.0
- **Hardware**: Stepper motor drivers with step/direction interface

### 1.3 Key Features
- Smooth acceleration/deceleration using David Austin algorithm
- Dynamic speed adjustment during motion
- Thread-safe position tracking with microsecond-precision timing
- Limit switch support with event-driven notifications
- Graceful stop with deceleration profile
- Optional enable/disable pin control
- Asynchronous API with cancellation support
- GPIO abstraction for hardware independence

## 2. Requirements

### 2.1 Functional Requirements

#### FR-1: Motion Control
- **FR-1.1**: Support moving by a specified distance in inches
- **FR-1.2**: Support moving to hardware limit switches
- **FR-1.3**: Accept speed parameter in revolutions per minute (RPM)
- **FR-1.4**: Support bidirectional movement (forward and reverse)
- **FR-1.5**: Provide graceful stop with deceleration
- **FR-1.6**: Allow dynamic speed changes during motion

#### FR-2: Acceleration Profile
- **FR-2.1**: Use David Austin algorithm for acceleration calculation
- **FR-2.2**: Linear acceleration profile based on configured acceleration rate
- **FR-2.3**: Mirror acceleration profile for deceleration
- **FR-2.4**: Maintain constant speed between acceleration and deceleration phases
- **FR-2.5**: Adjust profiles for short motions where total steps < accel + decel

#### FR-3: Position Tracking
- **FR-3.1**: Track current position in steps internally
- **FR-3.2**: Expose current position in inches via public property
- **FR-3.3**: Support manual position reset to zero
- **FR-3.4**: Maintain position accuracy across multiple moves

#### FR-4: Limit Switches
- **FR-4.1**: Support two limit switches (minimum and maximum)
- **FR-4.2**: Active-low configuration with internal pull-up resistors
- **FR-4.3**: Event-driven notification when limit switch state changes
- **FR-4.4**: Expose current limit switch states via properties
- **FR-4.5**: Stop motion when limit switch is triggered during RunToLimitSwitch
- **FR-4.6**: Limit deceleration to 300 steps maximum when approaching limit

#### FR-5: Enable Pin Control
- **FR-5.1**: Support optional enable/disable pin
- **FR-5.2**: Enable motor (LOW) before motion
- **FR-5.3**: Disable motor (HIGH) after motion completes or is cancelled

### 2.2 Non-Functional Requirements

#### NFR-1: Performance
- **NFR-1.1**: Microsecond-precision timing for step pulses
- **NFR-1.2**: No missed steps due to timing inaccuracies
- **NFR-1.3**: Minimal overhead for position tracking
- **NFR-1.4**: Efficient CPU usage with tight timing loops

#### NFR-2: Thread Safety
- **NFR-2.1**: Thread-safe position tracking across concurrent access
- **NFR-2.2**: Thread-safe speed adjustment during motion
- **NFR-2.3**: No race conditions between stop requests and motion execution
- **NFR-2.4**: Safe disposal even during active motion

#### NFR-3: Reliability
- **NFR-3.1**: Graceful handling of cancellation requests
- **NFR-3.2**: Proper resource cleanup on disposal
- **NFR-3.3**: Prevent double disposal
- **NFR-3.4**: Always disable motor on error or cancellation

#### NFR-4: Maintainability
- **NFR-4.1**: Clear separation between interface and implementation
- **NFR-4.2**: Comprehensive XML documentation for public API
- **NFR-4.3**: Logging of important events for debugging
- **NFR-4.4**: Testable design with GPIO abstraction

## 3. Architecture

### 3.1 Class Structure
```
IStepperMotorController (interface)
    ? implements
StepperMotorController (class)
    ? depends on
IGpioController (interface)
ControllerConfig (configuration)
ILogger<StepperMotorController> (logging)
```

### 3.2 Dependencies
- **IGpioController**: GPIO abstraction for hardware-independent testing
- **ControllerConfig**: Configuration settings (pins, steps per revolution, acceleration)
- **ILogger<StepperMotorController>**: Logging abstraction
- **System.Device.Gpio**: Pin value and mode enumerations
- **System.Diagnostics**: High-resolution timing with Stopwatch

### 3.3 Thread Safety Strategy
- **Position Lock**: `SemaphoreSlim _positionLock` protects `_currentPositionSteps` field
  - Acquired with `WaitAsync(cancellationToken)` for async updates
  - Acquired with `Wait()` for synchronous property access
  - Always released in `finally` block
- **Speed Adjustment**: `Interlocked.Exchange` for atomic updates to `_targetRpm`
- **Stop Coordination**: `volatile bool _stopRequested` flag for stop signaling
- **Cancellation**: `CancellationTokenSource _stopTokenSource` for internal stop mechanism

### 3.4 Asynchronous Design
- All public motion methods return `Task`
- Motion execution uses `Task.Run()` for CPU-intensive loops
- Methods accept `CancellationToken` parameter for cancellation support
- Linked cancellation tokens combine external and internal cancellation sources

## 4. API Design

### 4.1 Public Interface

#### Properties
```csharp
double CurrentPositionInches { get; }
```
- **Purpose**: Get current motor position in inches
- **Thread Safety**: Acquires `_positionLock` synchronously
- **Calculation**: `_currentPositionSteps / StepsPerRevolution / LeadScrewThreadsPerInch`

```csharp
bool IsMinLimitSwitchTriggered { get; }
bool IsMaxLimitSwitchTriggered { get; }
```
- **Purpose**: Get current limit switch states
- **Active-Low Logic**: `true` when pin reads LOW (switch closed)
- **Updated**: Via GPIO callbacks in real-time

#### Events
```csharp
event EventHandler? MinLimitSwitchTriggered;
event EventHandler? MaxLimitSwitchTriggered;
```
- **Purpose**: Notify subscribers of limit switch state changes
- **Fired**: When rising or falling edge detected on limit switch pins
- **Thread**: Invoked on GPIO callback thread

#### Methods

##### Constructor
```csharp
StepperMotorController(
    IGpioController gpio, 
    ControllerConfig config, 
    ILogger<StepperMotorController> logger)
```
- **Purpose**: Initialize controller with dependencies
- **Validation**: All parameters null-checked
- **Side Effects**: 
  - Opens and configures GPIO pins
  - Registers limit switch callbacks
  - Reads initial limit switch states
  - Logs initialization parameters

##### MoveInchesAsync
```csharp
Task MoveInchesAsync(
    double inches, 
    double rpm, 
    CancellationToken cancellationToken = default)
```
- **Purpose**: Move motor by specified distance with acceleration/deceleration
- **Parameters**:
  - `inches`: Distance to move (positive = forward, negative = reverse)
  - `rpm`: Target speed in revolutions per minute (must be > 0)
  - `cancellationToken`: Optional cancellation token
- **Validation**: Throws `ArgumentException` if `rpm <= 0`
- **Direction**: Automatically determined by sign of `inches`
- **Behavior**:
  - Sets direction pin based on sign of inches
  - Enables motor (if EnablePin configured)
  - Delegates to `ExecuteMotionAsync` for motion execution
  - Catches `OperationCanceledException` gracefully
  - Disables motor in finally block

##### RunToLimitSwitchAsync
```csharp
Task RunToLimitSwitchAsync(
    bool toMaxLimit, 
    double rpm, 
    CancellationToken cancellationToken = default)
```
- **Purpose**: Run motor until specified limit switch is triggered
- **Parameters**:
  - `toMaxLimit`: `true` for max limit, `false` for min limit
  - `rpm`: Target speed in revolutions per minute (must be > 0)
  - `cancellationToken`: Optional cancellation token
- **Validation**: Throws `ArgumentException` if `rpm <= 0`
- **Special Handling**:
  - Monitors limit switch state every step
  - Begins deceleration immediately when limit detected
  - Limits deceleration to maximum 300 steps to prevent overshoot
  - Supports dynamic speed changes during motion
  - Stops motion after deceleration completes
- **Execution**: Uses `Task.Run()` for background processing

##### StopAsync
```csharp
Task StopAsync()
```
- **Purpose**: Request graceful stop with deceleration
- **Behavior**: Sets `_stopRequested` flag to trigger deceleration
- **Non-Blocking**: Returns immediately without waiting for stop to complete
- **Deceleration**: Running motion will decelerate according to current profile

##### SetTargetRpm
```csharp
void SetTargetRpm(double rpm)
```
- **Purpose**: Adjust target speed during motion
- **Parameters**: `rpm` - New target speed (must be > 0)
- **Validation**: Logs warning and ignores if `rpm <= 0`
- **Thread Safety**: Uses `Interlocked.Exchange` for atomic update
- **Effect**: 
  - Changes take effect during constant speed phase
  - Recalculates deceleration steps for new speed
  - Logs speed change event

##### ResetPositionAsync
```csharp
Task ResetPositionAsync()
```
- **Purpose**: Reset current position to zero
- **Thread Safety**: Acquires `_positionLock` for atomic update
- **Use Case**: Called after homing to min limit switch

#### Disposal
```csharp
void Dispose()
```
- **Purpose**: Release resources and cleanup
- **Idempotent**: Safe to call multiple times (checks `_disposed` flag)
- **Cleanup Order**:
  1. Cancel `_stopTokenSource`
  2. Dispose `_stopTokenSource`
  3. Dispose `_positionLock`
  4. Dispose `_gpio` controller (closes all pins)

### 4.2 Internal Methods

#### InitializePins
```csharp
private void InitializePins()
```
- **Purpose**: Configure GPIO pins for motor control and limit switches
- **Pin Configuration**:
  - Close pins if already open (cleanup from previous runs)
  - Open PulsePin as Output (step signal)
  - Open DirectionPin as Output (direction signal)
  - Open EnablePin as Output if configured (enable/disable motor)
  - Open MinLimitSwitchPin as Input (min limit detection)
  - Open MaxLimitSwitchPin as Input (max limit detection)
- **Initial States**:
  - EnablePin set HIGH (motor disabled)
  - Read limit switch states (active-low)
- **Event Registration**:
  - Register callbacks for limit switch changes (rising and falling edges)

#### OnMinLimitSwitchChanged / OnMaxLimitSwitchChanged
```csharp
private void OnMinLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
private void OnMaxLimitSwitchChanged(object sender, PinValueChangedEventArgs e)
```
- **Purpose**: Handle limit switch state changes via GPIO callbacks
- **Logic**: `Falling` edge = triggered (active-low), `Rising` edge = released
- **Actions**:
  - Update `IsMinLimitSwitchTriggered` / `IsMaxLimitSwitchTriggered` property
  - Log state change event
  - Raise public event for subscribers

#### ExecuteMotionAsync
```csharp
internal Task ExecuteMotionAsync(
    int steps, 
    double rpm, 
    bool direction, 
    CancellationToken cancellationToken)
```
- **Purpose**: Execute motion with acceleration/deceleration profile
- **Visibility**: Internal (exposed to test project via `InternalsVisibleTo`)
- **Parameters**:
  - `steps`: Total number of steps to execute
  - `rpm`: Target speed in RPM
  - `direction`: `true` = forward/max, `false` = reverse/min
  - `cancellationToken`: Cancellation token
- **Algorithm**: See section 5.1 for detailed David Austin implementation
- **Execution**: Uses `Task.Run()` for background processing
- **Cancellation**: Creates linked token source combining external and internal tokens
- **Dynamic Speed**: Checks for RPM changes during constant speed phase
- **Stop Handling**: Monitors `_stopRequested` flag and adjusts deceleration
- **Exception Handling**: Catches and suppresses `OperationCanceledException`

#### MicrosecondSleep
```csharp
private void MicrosecondSleep(int microseconds)
```
- **Purpose**: High-precision delay for stepper motor pulse timing
- **Implementation**:
  - Uses `Stopwatch.GetTimestamp()` for high-resolution timing
  - Calculates required ticks from microseconds and stopwatch frequency
  - Tight loop comparing elapsed ticks against target
- **Rationale**: `Thread.Sleep()` is too imprecise (millisecond granularity)
- **Trade-off**: Busy-wait consumes CPU but guarantees timing accuracy
- **Usage**: Called twice per step (HIGH and LOW half-periods)

## 5. Algorithms

### 5.1 David Austin Stepper Motor Algorithm

#### Overview
The David Austin algorithm provides optimal linear acceleration for stepper motors by calculating decreasing delays between step pulses during acceleration.

#### Mathematical Foundation

**Initial Delay:**
```
c? = 0.676 × ?(2/?) × 10? microseconds
```
Where:
- `c?` = Initial delay (slowest step)
- `?` = Acceleration in steps/sec²

**Acceleration Step Delay:**
```
c? = c??? - (2 × c???) / (4n + 1)
```
Where:
- `c?` = Delay for step n
- `c???` = Previous delay
- `n` = Current step number (1-indexed)

**Deceleration Step Delay:**
```
c? = c??? + (2 × c???) / (4n - 1)
```
Where:
- `n` = Steps remaining until stop
- Formula mirrors acceleration by reversing the sequence

**Acceleration Steps Required:**
```
n_accel = v² / (2?)
```
Where:
- `v` = Maximum speed in steps/sec
- `?` = Acceleration in steps/sec²

**Constant Speed Delay:**
```
c_constant = 1,000,000 / v
```
Where:
- `v` = Maximum speed in steps/sec

#### Implementation Details

**Phase Determination:**
1. **Acceleration Phase**: `step < accelerationSteps`
   - Apply acceleration formula: `c? = c??? - (2 × c???) / (4n + 1)`
   - Delays decrease (motor speeds up)
   
2. **Constant Speed Phase**: `accelerationSteps <= step < (steps - decelerationSteps)`
   - Maintain `targetDelayMicroseconds`
   - Monitor for dynamic speed changes
   - Recalculate target delay if RPM changes
   
3. **Deceleration Phase**: `step >= (steps - decelerationSteps)`
   - Apply deceleration formula: `c? = c??? + (2 × c???) / (4n - 1)`
   - Delays increase (motor slows down)
   - `n` = steps remaining until stop

**Short Motion Handling:**
```csharp
if (accelerationSteps + decelerationSteps > steps)
{
    accelerationSteps = steps / 2;
    decelerationSteps = steps - accelerationSteps;
}
```
- Proportionally reduce both phases if total steps insufficient
- Ensures smooth start and stop even for very short moves

**Limit Switch Deceleration:**
```csharp
var decelerationSteps = Math.Min(accelerationSteps, 300);
```
- Limits deceleration to maximum 300 steps
- Prevents excessive overshoot when approaching limits
- Prioritizes stopping quickly over smooth deceleration

### 5.2 Dynamic Speed Adjustment

#### Requirements
- Allow speed changes during motion without stopping
- Smooth transition to new speed
- Maintain position accuracy
- Recalculate deceleration profile for new speed

#### Implementation
1. Store target RPM in `_targetRpm` field (accessed via `Interlocked`)
2. During constant speed phase, check for RPM changes:
   ```csharp
   var latestRpm = Interlocked.CompareExchange(ref _targetRpm, 0, 0);
   if (Math.Abs(currentRpm - latestRpm) > 0.01)
   {
       // Update timing calculations
   }
   ```
3. Recalculate `maxStepsPerSecond` and `targetDelayMicroseconds`
4. Recalculate `decelerationSteps` for new speed
5. Ensure sufficient steps remain for deceleration
6. Log speed change event

#### Constraints
- Speed changes ignored during acceleration phase
- Speed changes ignored during deceleration phase
- Speed changes ignored if motion stopped or limit switch detected

### 5.3 Graceful Stop Algorithm

#### Stop Request Flow
1. External call to `StopAsync()` sets `_stopRequested = true`
2. Motion loop detects `_stopRequested` flag
3. Sets `stopRequested = true` locally and records current step
4. Calculates remaining steps: `steps = min(steps, step + decelerationSteps)`
5. Enters deceleration phase using standard deceleration formula
6. Motion completes after deceleration
7. `_stopRequested` reset to `false` in finally block

#### Advantages
- No abrupt stops (prevents mechanical stress and lost steps)
- Uses existing deceleration profile
- Predictable stop distance
- Maintains position accuracy

## 6. Configuration

### 6.1 ControllerConfig Class

#### Properties
```csharp
int PulsePin { get; set; }                          // GPIO pin for step pulses (default: 21)
int DirectionPin { get; set; }                      // GPIO pin for direction (default: 20)
int? EnablePin { get; set; }                        // Optional enable pin (default: null)
int MinLimitSwitchPin { get; set; }                 // GPIO pin for min limit (default: 23)
int MaxLimitSwitchPin { get; set; }                 // GPIO pin for max limit (default: 24)
StepsPerRevolution StepsPerRevolution { get; set; } // Microstepping (default: SPR_400)
double LeadScrewThreadsPerInch { get; set; }        // Lead screw pitch (default: 5)
double Acceleration { get; set; }                   // Acceleration in steps/sec² (default: 5000)
```

#### StepsPerRevolution Enum
```csharp
public enum StepsPerRevolution
{
    SPR_200 = 200,      // Full step
    SPR_400 = 400,      // Half step
    SPR_800 = 800,      // 1/4 step
    SPR_1600 = 1600,    // 1/8 step
    SPR_3200 = 3200,    // 1/16 step
    SPR_6400 = 6400,    // 1/32 step
    SPR_12800 = 12800   // 1/64 step
}
```

### 6.2 Pin Assignments

#### Output Pins
- **PulsePin**: Generates step pulses (square wave)
  - HIGH/LOW transitions trigger motor steps
  - Pulse width = delay/2 microseconds
  
- **DirectionPin**: Controls rotation direction
  - HIGH = forward/max limit direction
  - LOW = reverse/min limit direction
  - Must be set before starting motion
  
- **EnablePin** (optional): Enables/disables motor driver
  - LOW = motor enabled (holding torque)
  - HIGH = motor disabled (free-wheeling)
  - Set LOW before motion, HIGH after completion

#### Input Pins
- **MinLimitSwitchPin**: Detects minimum travel limit
  - Active-low with internal pull-up (`PinMode.Input`)
  - LOW = switch closed (limit reached)
  - HIGH = switch open (normal operation)
  
- **MaxLimitSwitchPin**: Detects maximum travel limit
  - Active-low with internal pull-up (`PinMode.Input`)
  - LOW = switch closed (limit reached)
  - HIGH = switch open (normal operation)

### 6.3 Physical Configuration

#### Lead Screw Calculation
```
Distance (inches) = Steps / StepsPerRevolution / LeadScrewThreadsPerInch
Steps = Distance × StepsPerRevolution × LeadScrewThreadsPerInch
```

Example:
- Move 1 inch with SPR_400 and 5 TPI lead screw
- Steps = 1 × 400 × 5 = 2000 steps

#### Speed Calculation
```
StepsPerSecond = (RPM × StepsPerRevolution) / 60
DelayMicroseconds = 1,000,000 / StepsPerSecond
```

Example:
- 60 RPM with SPR_400
- Steps/sec = (60 × 400) / 60 = 400 steps/sec
- Delay = 1,000,000 / 400 = 2500 ?s per step

## 7. Motion Execution Flow

### 7.1 MoveInchesAsync Flow
```
1. Validate rpm > 0 (throw if invalid)
2. Calculate total steps from inches
3. Determine direction from sign of inches
4. Set DirectionPin HIGH (forward) or LOW (reverse)
5. Enable motor (set EnablePin LOW if configured)
6. Call ExecuteMotionAsync(abs(totalSteps), rpm, direction, token)
7. Catch OperationCanceledException (expected for stop)
8. Finally: Disable motor (set EnablePin HIGH)
```

### 7.2 ExecuteMotionAsync Flow
```
1. Initialize target RPM with Interlocked.Exchange
2. Calculate max steps/sec from RPM and StepsPerRevolution
3. Calculate target delay for constant speed
4. Calculate initial delay using David Austin formula
5. Calculate acceleration/deceleration steps needed
6. Adjust if total steps < accel + decel
7. Launch Task.Run() for motion execution:
   a. Create linked cancellation token source
   b. Initialize delay to initialDelayMicroseconds
   c. For each step:
      - Check for dynamic RPM changes (constant speed phase only)
      - Check for _stopRequested flag
      - Calculate delay based on current phase:
        * Acceleration: Decrease delay using David Austin
        * Constant: Use targetDelayMicroseconds
        * Deceleration: Increase delay (mirror acceleration)
      - Write PulsePin HIGH
      - Sleep delay/2 microseconds
      - Write PulsePin LOW
      - Sleep delay/2 microseconds
      - Update position (acquire lock, increment/decrement, release)
   d. Catch OperationCanceledException
   e. Finally: Reset _stopRequested, disable motor
```

### 7.3 RunToLimitSwitchAsync Flow
```
1. Validate rpm > 0 (throw if invalid)
2. Initialize target RPM with Interlocked.Exchange
3. Set DirectionPin HIGH (max) or LOW (min)
4. Enable motor (set EnablePin LOW if configured)
5. Launch Task.Run() for motion execution:
   a. Calculate timing parameters (same as ExecuteMotionAsync)
   b. Limit decelerationSteps to maximum 300
   c. Create linked cancellation token source
   d. Initialize tracking variables:
      - delayMicroseconds = initialDelayMicroseconds
      - step = 0
      - limitSwitchDetected = false
      - stopRequested = false
      - decelStepCounter = 0
   e. While not cancelled:
      - Check for dynamic RPM changes (constant speed phase only)
      - Check for _stopRequested flag
      - Check appropriate limit switch state
      - If limit detected or stop requested:
        * Enter deceleration mode
        * Increment decelStepCounter
        * Break if decelStepCounter >= decelerationSteps
        * Apply deceleration formula
      - Else if accelerating:
        * Apply acceleration formula
      - Else (constant speed):
        * Use targetDelayMicroseconds
      - Generate step pulse (HIGH, sleep, LOW, sleep)
      - Update position (thread-safe)
      - Increment step counter
   f. Catch OperationCanceledException
   g. Finally: Reset _stopRequested, disable motor
```

## 8. Thread Safety Guarantees

### 8.1 Position Tracking
- **Shared State**: `double _currentPositionSteps`
- **Protection**: `SemaphoreSlim _positionLock` (max count = 1)
- **Update Pattern**:
  ```csharp
  await _positionLock.WaitAsync(cancellationToken);
  try
  {
      _currentPositionSteps += direction ? 1 : -1;
  }
  finally
  {
      _positionLock.Release();
  }
  ```
- **Read Pattern** (CurrentPositionInches property):
  ```csharp
  _positionLock.Wait();
  try
  {
      return _currentPositionSteps / ... ;
  }
  finally
  {
      _positionLock.Release();
  }
  ```

### 8.2 Speed Adjustment
- **Shared State**: `double _targetRpm`
- **Protection**: `Interlocked.Exchange` / `Interlocked.CompareExchange`
- **Write Pattern**: `Interlocked.Exchange(ref _targetRpm, rpm)`
- **Read Pattern**: `Interlocked.CompareExchange(ref _targetRpm, 0, 0)`
- **Atomicity**: Guaranteed by interlocked operations (no locks needed)

### 8.3 Stop Coordination
- **Shared State**: `volatile bool _stopRequested`
- **Modifier**: `volatile` ensures visibility across threads
- **Write**: `_stopRequested = true` in `StopAsync()`
- **Read**: Motion loops check flag periodically
- **Reset**: Set to `false` in finally block after motion completes

### 8.4 Cancellation
- **Internal Token**: `CancellationTokenSource _stopTokenSource`
- **Linked Token**: `CreateLinkedTokenSource(cancellationToken, _stopTokenSource.Token)`
- **Purpose**: Combine external cancellation with internal stop mechanism
- **Disposal**: Linked token source disposed at end of motion

## 9. Timing Requirements

### 9.1 Pulse Timing
- **Precision**: Microsecond-level accuracy required
- **Square Wave**: Equal HIGH and LOW periods
- **Period**: Total delay determined by David Austin algorithm
- **Frequency Range**: Determined by minimum/maximum RPM and microstepping

### 9.2 Timing Calculations

#### Example: 60 RPM, SPR_400, Acceleration 5000 steps/sec²

**Maximum Speed:**
- Steps/sec = (60 × 400) / 60 = 400 steps/sec
- Target delay = 1,000,000 / 400 = 2,500 ?s

**Initial Delay:**
- c? = 0.676 × ?(2/5000) × 1,000,000
- c? = 0.676 × 0.02 × 1,000,000
- c? ? 13,520 ?s (very slow start)

**Acceleration Steps:**
- n_accel = 400² / (2 × 5000)
- n_accel = 160,000 / 10,000
- n_accel = 16 steps to reach 400 steps/sec

**Time to Reach Speed:**
- Sum of delays from c? to c?? (approximately)
- Roughly 80-100ms for this example

### 9.3 Performance Constraints
- **Minimum Delay**: Limited by CPU/timing accuracy (~100 ?s practical minimum)
- **Maximum Speed**: Determined by motor capabilities and microstepping
- **CPU Usage**: High during motion (busy-wait for timing)
- **Missed Steps**: None if timing maintained accurately

## 10. Error Handling

### 10.1 Validation Errors
- **ArgumentNullException**: Thrown if constructor parameters are null
- **ArgumentException**: Thrown if `rpm <= 0` in motion methods
- **Logging**: Warnings logged for invalid RPM in `SetTargetRpm`

### 10.2 Cancellation Handling
- **OperationCanceledException**: Caught and suppressed in motion methods
- **Expected**: Thrown when `linkedCts.Token` is cancelled
- **Side Effects**: Motor disabled in finally block
- **Position**: Updated up to point of cancellation

### 10.3 Limit Switch Handling
- **Detection**: Checked every step during `RunToLimitSwitchAsync`
- **Action**: Begin deceleration immediately when triggered
- **Overshoot**: Limited to maximum 300 steps of deceleration
- **Events**: Fired via GPIO callbacks (async from motion thread)

### 10.4 Disposal Protection
- **Flag**: `bool _disposed` prevents operations after disposal
- **Check**: Should be added to public methods (currently not implemented)
- **Cleanup**: Always succeeds even if partially initialized

## 11. Testing Requirements

### 11.1 Unit Testing Strategy
- **Framework**: XUnit with NSubstitute for mocking
- **GPIO Mock**: Use `Substitute.For<IGpioController>()`
- **Test Scope**: Internal methods exposed via `InternalsVisibleTo`

### 11.2 Test Coverage

#### Constructor Tests
- Verify all pins opened correctly
- Verify initial limit switch states read
- Verify callbacks registered
- Verify null parameter validation

#### Motion Tests
- Verify correct number of pulses generated
- Verify direction pin set correctly
- Verify enable pin toggled correctly
- Verify position tracking accuracy
- Verify acceleration/deceleration profiles

#### Limit Switch Tests
- Verify events fired on state changes
- Verify properties updated correctly
- Verify active-low logic (Falling = triggered)
- Verify motion stops at limits

#### Cancellation Tests
- Verify cancellation token honored
- Verify `StopAsync()` triggers graceful stop
- Verify motor disabled on cancellation
- Verify `OperationCanceledException` suppressed

#### Speed Adjustment Tests
- Verify `SetTargetRpm` updates target atomically
- Verify speed changes applied during constant speed phase
- Verify deceleration recalculated for new speed
- Verify invalid RPM values ignored

#### Disposal Tests
- Verify resources cleaned up correctly
- Verify double disposal is safe
- Verify cancellation token cancelled on dispose

### 11.3 Mock Setup Patterns
```csharp
var gpio = Substitute.For<IGpioController>();
gpio.Read(_config.MinLimitSwitchPin).Returns(PinValue.High); // Not triggered
gpio.Read(_config.MaxLimitSwitchPin).Returns(PinValue.High); // Not triggered

var controller = new StepperMotorController(gpio, config, logger);
await controller.MoveInchesAsync(1.0, 60);

gpio.Received(2000).Write(_config.PulsePin, PinValue.High); // 2000 steps × 1 pulse
```

## 12. Usage Examples

### 12.1 Basic Movement
```csharp
var gpio = new GpioControllerWrapper();
var config = new ControllerConfig();
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<StepperMotorController>();

using var controller = new StepperMotorController(gpio, config, logger);

// Move 5 inches forward at 60 RPM
await controller.MoveInchesAsync(5.0, 60);

// Move 2 inches backward at 30 RPM
await controller.MoveInchesAsync(-2.0, 30);

// Check position
Console.WriteLine($"Position: {controller.CurrentPositionInches:F3} inches");
```

### 12.2 Homing Sequence
```csharp
// Run to min limit switch at 30 RPM
await controller.RunToLimitSwitchAsync(toMaxLimit: false, rpm: 30);

// Reset position to zero at limit
await controller.ResetPositionAsync();

Console.WriteLine("Homing complete");
```

### 12.3 Limit Switch Monitoring
```csharp
controller.MinLimitSwitchTriggered += (s, e) =>
{
    Console.WriteLine($"Min limit: {controller.IsMinLimitSwitchTriggered}");
};

controller.MaxLimitSwitchTriggered += (s, e) =>
{
    Console.WriteLine($"Max limit: {controller.IsMaxLimitSwitchTriggered}");
};

await controller.RunToLimitSwitchAsync(toMaxLimit: true, rpm: 60);
```

### 12.4 Cancellation and Stop
```csharp
var cts = new CancellationTokenSource();

// Start long movement
var moveTask = controller.MoveInchesAsync(100.0, 60, cts.Token);

// Cancel after 2 seconds
await Task.Delay(2000);
await controller.StopAsync(); // Graceful stop with deceleration

await moveTask;
Console.WriteLine($"Stopped at: {controller.CurrentPositionInches:F3} inches");
```

### 12.5 Dynamic Speed Adjustment
```csharp
// Start moving to limit at 30 RPM
var limitTask = controller.RunToLimitSwitchAsync(toMaxLimit: true, rpm: 30);

// Speed up after 1 second
await Task.Delay(1000);
controller.SetTargetRpm(60); // Double the speed

await limitTask;
```

## 13. Design Decisions

### 13.1 Asynchronous API
- **Rationale**: Non-blocking motion control for UI responsiveness
- **Pattern**: All motion methods return `Task`
- **Execution**: CPU-intensive loops run via `Task.Run()`
- **Cancellation**: Standard `CancellationToken` support throughout

### 13.2 GPIO Abstraction
- **Interface**: `IGpioController` decouples from hardware
- **Benefits**:
  - Unit testing without Raspberry Pi
  - Development on Windows/Mac
  - Mock behavior for test scenarios
- **Implementations**:
  - `GpioControllerWrapper`: Production (System.Device.Gpio)
  - `FakeGpioController`: Development/testing (console output)

### 13.3 David Austin Algorithm
- **Selection**: Industry-standard for stepper motor acceleration
- **Advantages**:
  - Optimal linear acceleration (constant jerk)
  - No vibration or resonance issues
  - Mathematically proven smooth profile
  - Simple iterative calculation (no lookup tables)
- **Alternative Considered**: S-curve profiles (rejected: complexity vs. benefit)

### 13.4 Microsecond Sleep Implementation
- **Busy-Wait**: Tight loop with `Stopwatch.GetTimestamp()`
- **Rationale**: 
  - `Thread.Sleep()` has millisecond granularity (insufficient)
  - `Task.Delay()` even less precise
  - Stepper motors require microsecond timing accuracy
- **Trade-off**: High CPU usage during motion (acceptable for dedicated motor controller)

### 13.5 Active-Low Limit Switches
- **Configuration**: `PinMode.Input` (uses internal pull-up resistors)
- **Logic**: LOW = triggered, HIGH = released
- **Advantages**:
  - Fail-safe: Wire break appears as "released" (safe)
  - Standard industrial convention
  - Simpler wiring (no external pull-up resistors)

### 13.6 Stop Mechanism
- **Deceleration**: Graceful stop with deceleration profile
- **Rationale**:
  - Prevents mechanical stress
  - Maintains step count accuracy
  - No lost steps from abrupt stop
- **Alternative Considered**: Immediate stop (rejected: inaccuracy and mechanical damage)

### 13.7 Enable Pin as Optional
- **Rationale**: Some motor drivers don't have enable pin
- **Default**: `null` (not used)
- **When Used**: Set LOW to enable, HIGH to disable
- **Purpose**: Reduce motor holding current when idle

## 14. Logging Strategy

### 14.1 Logged Events
- **Initialization**: All configuration parameters at startup
- **Limit Switches**: State changes with pin numbers
- **Speed Changes**: RPM adjustments during motion with current step
- **Warnings**: Invalid RPM values in `SetTargetRpm`

### 14.2 Log Levels
- **Information**: Normal operational events
  - Initialization complete
  - Limit switch state changes
  - Speed adjustment confirmations
- **Warning**: Invalid parameters
  - Invalid RPM in `SetTargetRpm`

### 14.3 Structured Logging
- Uses named parameters for log message templates
- Example: `"Min limit switch {Status} (Pin {Pin})"`
- Enables filtering and analysis in log aggregation systems

## 15. Extension Points

### 15.1 Custom GPIO Controllers
- Implement `IGpioController` interface
- Examples: Network-controlled GPIO, I²C expanders, custom hardware
- Pattern: Wrapper around specific GPIO library

### 15.2 Alternative Algorithms
- Override or replace `ExecuteMotionAsync` for different profiles
- Examples: S-curve, trapezoidal, custom velocity curves
- Maintain same interface contract

### 15.3 Additional Sensors
- Pattern established with limit switches
- Extend for: encoders, current sensors, temperature monitors
- Use same event-driven callback pattern

### 15.4 Multi-Axis Control
- Instantiate multiple `StepperMotorController` instances
- Coordinate motion in higher-level controller
- Share `IGpioController` instance for efficiency

## 16. Limitations and Constraints

### 16.1 Known Limitations
- **Single Motion**: Cannot queue multiple moves
- **No Position Feedback**: Open-loop control (no encoder)
- **CPU Intensive**: Busy-wait during motion (not suitable for shared systems)
- **Memory**: Position stored as double (potential floating-point drift over time)
- **Platform**: Microsecond sleep may vary by OS scheduler

### 16.2 Hardware Constraints
- **GPIO Speed**: Limited by Raspberry Pi GPIO maximum toggle rate
- **Driver Capability**: Maximum step rate depends on motor driver
- **Motor Limits**: Maximum RPM limited by motor torque curve
- **Mechanical**: Lead screw pitch affects speed/torque trade-off

### 16.3 Software Constraints
- **No Queuing**: Only one motion at a time
- **No Interruption**: Cannot change motion parameters mid-acceleration/deceleration
- **Dispose Required**: Must call `Dispose()` to clean up GPIO resources
- **Event Thread**: Limit switch events fire on GPIO callback thread (not UI thread)

## 17. Future Enhancements

### 17.1 Potential Improvements
- **Motion Queue**: Accept multiple motion commands in sequence
- **S-Curve Profiles**: Alternative acceleration algorithm for smoother motion
- **Encoder Support**: Closed-loop position feedback and error correction
- **Jog Mode**: Continuous motion at constant speed (no target position)
- **Velocity Mode**: Constant speed motion without acceleration/deceleration
- **Emergency Stop**: Immediate stop without deceleration (safety feature)
- **Step Loss Detection**: Monitor for missed steps via encoder or current sensing
- **Backlash Compensation**: Adjust for mechanical backlash in direction changes
- **Multi-Axis Coordination**: Synchronized motion across multiple motors

### 17.2 API Additions
```csharp
// Proposed future methods (not implemented)
Task JogAsync(bool direction, double rpm, CancellationToken cancellationToken);
Task MoveStepsAsync(int steps, double rpm, CancellationToken cancellationToken);
Task EmergencyStopAsync(); // Immediate stop, no deceleration
void SetAcceleration(double stepsPerSecondSquared);
```

## 18. Dependencies and References

### 18.1 NuGet Packages
- **System.Device.Gpio** (v4.1.0): GPIO pin control
- **Microsoft.Extensions.Logging** (v10.0.3): Logging abstraction

### 18.2 Related Documents
- **IStepperMotorController.cs**: Public interface contract
- **ControllerConfig.cs**: Configuration class specification
- **GpioControllerWrapper.cs**: GPIO interface and wrapper implementation
- **FakeGpioController.cs**: Development/testing mock implementation
- **Test/StepperMotorControllerTests.cs**: Unit test suite

### 18.3 External References
- David Austin: "Generate stepper-motor speed profiles in real time" (Embedded Systems Programming, 2004)
- Raspberry Pi GPIO documentation: https://www.raspberrypi.org/documentation/usage/gpio/
- System.Device.Gpio documentation: https://docs.microsoft.com/en-us/dotnet/iot/

## 19. Validation Criteria

### 19.1 Acceptance Criteria
- ? Motor moves specified distance accurately (±1 step tolerance)
- ? Acceleration is smooth with no vibration or resonance
- ? Deceleration mirrors acceleration symmetrically
- ? Limit switches stop motion with maximum 300-step overshoot
- ? Position tracking accurate across multiple moves
- ? Stop request triggers graceful deceleration
- ? Dynamic speed changes apply smoothly during motion
- ? Enable pin controls motor power correctly
- ? All unit tests pass with 100% success rate
- ? No resource leaks on disposal
- ? Thread-safe under concurrent access

### 19.2 Performance Benchmarks
- **Timing Accuracy**: Within ±10 ?s of target delay
- **Position Accuracy**: Within ±1 step per 10,000 steps
- **Stop Distance**: Predictable within deceleration profile
- **Speed Change**: Applied within one step pulse period
- **Event Latency**: Limit switch events fire within 1ms of state change

## 20. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025 | Initial | Initial specification based on implemented code |

---

## Appendix A: David Austin Algorithm Derivation

The David Austin algorithm derives from the kinematic equation for constant acceleration:
```
v² = v?² + 2?s
```

For stepper motors:
- `v? = 0` (starting from rest)
- `v = ?(2?s)` (velocity after s steps)
- `1/delay = v` (frequency proportional to velocity)

The recursive formula:
```
c? = c??? - (2 × c???) / (4n + 1)
```

Approximates the ideal delay sequence for linear acceleration without requiring expensive square root calculations at each step.

## Appendix B: Timing Accuracy Analysis

### Thread.Sleep vs. Stopwatch Busy-Wait

**Thread.Sleep(milliseconds):**
- Resolution: ~15-16ms on Windows, 1ms on Linux
- Granularity: Millisecond level
- Overhead: Low CPU usage
- Accuracy: Poor for microsecond timing

**Stopwatch Busy-Wait:**
- Resolution: ~1-10 ?s (depends on CPU frequency)
- Granularity: Microsecond level
- Overhead: High CPU usage (100% core utilization)
- Accuracy: Excellent for microsecond timing

**Conclusion**: Busy-wait required for stepper motor pulse generation despite CPU cost.

## Appendix C: Position Tracking Precision

### Floating-Point Considerations
- **Storage**: `double _currentPositionSteps` (64-bit)
- **Precision**: ~15-16 significant decimal digits
- **Integer Range**: Exact representation up to 2?³ (9 quadrillion)
- **Motor Context**: Even at 12,800 steps/rev, 100,000 revolutions = 1.28 billion steps (well within precision)
- **Drift**: Negligible for practical motor controller usage

### Alternative: Integer Storage
- **Pros**: Perfect precision, no floating-point errors
- **Cons**: Requires separate tracking for fractional position
- **Decision**: Double is sufficient for this application

---

**Document Status**: Complete specification based on current implementation  
**Intended Audience**: Developers maintaining or extending the motor controller library  
**Maintenance**: Update when adding features or changing algorithms
