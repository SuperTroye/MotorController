# SynchronizedDualAxisController

## Overview

`SynchronizedDualAxisController` drives two stepper motors — a **linear axis** and a **rotary axis** — from a single step-generation loop, analogous to LinuxCNC's stepgen thread. The rotary axis behaves as if it were geared to the linear axis lead screw, but the gear ratio is configured in software so it can be changed without swapping physical gears.

Implements `ISynchronizedDualAxisController`, which extends `IStepperMotorController`.

---

## Files

| File | Description |
|---|---|
| `MotorController/ISynchronizedDualAxisController.cs` | Interface extending `IStepperMotorController` with `CurrentRotaryPositionDegrees` |
| `MotorController/SynchronizedDualAxisController.cs` | Full implementation |
| `MotorController/Config/SynchronizedDualAxisConfig.cs` | Combined configuration (already existed) |
| `MotorController/Config/RotaryAxisConfig.cs` | Rotary axis pin/SPR configuration (already existed) |
| `MotorController/Config/LinearAxisConfig.cs` | Linear axis configuration (already existed) |
| `Test/StepperMotorControllerTests.cs` | 30 tests in `SynchronizedDualAxisControllerTests` class appended |

---

## Configuration

`SynchronizedDualAxisConfig` combines the two axis configs with a gear ratio:

```csharp
var config = new SynchronizedDualAxisConfig
{
    LinearAxisConfig = new LinearAxisConfig
    {
        PulsePin = 21,
        DirectionPin = 20,
        MinLimitSwitchPin = 24,
        MaxLimitSwitchPin = 23,
        StepsPerRevolution = StepsPerRevolution.SPR_400,
        LeadScrewThreadsPerInch = 5.0,
        Acceleration = 5000.0        // steps/sec²
    },
    RotaryAxisConfig = new RotaryAxisConfig
    {
        PulsePin = 16,
        DirectionPin = 12,
        EnablePin = null,            // optional
        StepsPerRevolution = StepsPerRevolution.SPR_400
    },
    GearRatio = 0.4                  // rotary revolutions per linear revolution
};
```

### GearRatio

`GearRatio` is the number of rotary axis revolutions per one revolution of the linear axis lead screw.

- `1.0` → rotary turns at exactly the same rate as the lead screw  
- `0.4` → rotary turns 0.4× per lead screw revolution (default, matches original mechanical ratio)  
- `2.0` → rotary turns twice per lead screw revolution  

The ratio can be changed at any time in configuration without hardware changes.

---

## Design

### Single Step-Generation Loop

Both axes are driven from one `Task.Run` loop inside `ExecuteMotionInternalAsync`. The linear axis owns the timing via the **David Austin algorithm**. The rotary axis receives the exact same delay value at every iteration, giving it the identical acceleration and deceleration profile automatically — no separate ramp calculation is needed for the rotary axis.

### DDA Accumulator — Zero Long-Term Drift

The rotary steps per linear step is a rational number:

```
rotaryStepsPerLinearStep = GearRatio × RotarySPR / LinearSPR
```

A `double rotaryAccumulator` carries the sub-step fractional remainder forward across every step. Integer pulses are only fired when the accumulator crosses a whole number boundary. This is mathematically equivalent to LinuxCNC's stepgen DDA and guarantees **zero long-term positional drift** between the axes regardless of how non-integer the ratio is.

**Example** — `GearRatio = 0.5`, both axes at `SPR_400`:

```
rotaryStepsPerLinearStep = 0.5 × 400 / 400 = 0.5

Step 0: acc = 0.0 + 0.5 = 0.5  → 0 rotary pulses, remainder 0.5
Step 1: acc = 0.5 + 0.5 = 1.0  → 1 rotary pulse,  remainder 0.0
Step 2: acc = 0.0 + 0.5 = 0.5  → 0 rotary pulses, remainder 0.5
...
```

Over 400 linear steps → exactly 200 rotary pulses. No drift.

### `FireStepPeriod` — Stopwatch-Anchored Timing

The entire step period is consumed inside `FireStepPeriod` using `Stopwatch.GetTimestamp()` spin-waits. This means:

- The outer loop always advances at the correct linear rate
- Rotary pulses are inserted **within** the step period, not added on top of it
- When multiple rotary pulses fall in one step, they are spaced evenly across the period

```
|<-------- one linear step period -------->|
|  linear HIGH | linear LOW | rot | rot   |
```

### Acceleration Inheritance

Because the rotary axis uses the same `delayMicroseconds` value as the linear axis at every step, it automatically follows the full David Austin acceleration and deceleration profile. No separate `Acceleration` parameter is needed or used for the rotary axis.

### GPIO

| Signal | Linear axis | Rotary axis |
|---|---|---|
| Pulse | `LinearAxisConfig.PulsePin` | `RotaryAxisConfig.PulsePin` |
| Direction | `LinearAxisConfig.DirectionPin` | `RotaryAxisConfig.DirectionPin` |
| Enable | `LinearAxisConfig.EnablePin` (optional) | `RotaryAxisConfig.EnablePin` (optional) |
| Min limit switch | `LinearAxisConfig.MinLimitSwitchPin` | **none** |
| Max limit switch | `LinearAxisConfig.MaxLimitSwitchPin` | **none** |

The rotary axis has **no limit switches** because it rotates continuously.

Active-low limit switches with internal pull-up resistors (`PinMode.InputPullUp`) — same convention as `StepperMotorController`.

---

## Public API

```csharp
// Inherited from IStepperMotorController:
Task MoveInchesAsync(double inches, double rpm, CancellationToken cancellationToken = default);
Task RunToLimitSwitchAsync(LimitSwitch direction, double rpm, CancellationToken cancellationToken = default);
Task StopAsync();
Task ResetPositionAsync();
void SetTargetSpeed(double rpm);
double CurrentPositionInches { get; }
bool IsMinLimitSwitchTriggered { get; }
bool IsMaxLimitSwitchTriggered { get; }
event EventHandler? MinLimitSwitchTriggered;
event EventHandler? MaxLimitSwitchTriggered;

// Added by ISynchronizedDualAxisController:
double CurrentRotaryPositionDegrees { get; }
```

`CurrentRotaryPositionDegrees` accumulates the total angular displacement of the rotary axis since construction (or last reset). It is thread-safe via its own `SemaphoreSlim`.

---

## Dependency Injection (UI)

Register in `Program.cs` alongside or instead of `StepperMotorController`:

```csharp
services.Configure<SynchronizedDualAxisConfig>(
    context.Configuration.GetSection("SynchronizedDualAxisConfig"));

services.AddSingleton<IGpioController>(sp =>
    OperatingSystem.IsWindows()
        ? new FakeGpioController(sp.GetRequiredService<ILogger<FakeGpioController>>())
        : new GpioControllerWrapper());

services.AddSingleton<ISynchronizedDualAxisController, SynchronizedDualAxisController>(sp =>
    new SynchronizedDualAxisController(
        sp.GetRequiredService<IGpioController>(),
        sp.GetRequiredService<IOptions<SynchronizedDualAxisConfig>>().Value,
        sp.GetRequiredService<ILogger<SynchronizedDualAxisController>>()));
```

Add the corresponding section to `UI/appsettings.json`:

```json
"SynchronizedDualAxisConfig": {
  "GearRatio": 0.4,
  "LinearAxisConfig": {
    "PulsePin": 21,
    "DirectionPin": 20,
    "MinLimitSwitchPin": 24,
    "MaxLimitSwitchPin": 23,
    "EnablePin": null,
    "StepsPerRevolution": 400,
    "LeadScrewThreadsPerInch": 5.0,
    "Acceleration": 5000
  },
  "RotaryAxisConfig": {
    "PulsePin": 16,
    "DirectionPin": 12,
    "EnablePin": null,
    "StepsPerRevolution": 400
  }
}
```

---

## Stop Behaviour

Consistent with `StepperMotorController`:

| Trigger | Mechanism | Result |
|---|---|---|
| `StopAsync()` | Sets `_stopRequested = true` | Graceful deceleration, no exception |
| `CancellationToken` cancelled | Hard cancel via linked CTS | Immediate stop, throws `OperationCanceledException` |

Both axes stop together because they share the same loop.

---

## Thread Safety

| State | Protection |
|---|---|
| `_currentLinearPositionSteps` | `SemaphoreSlim _positionLock` |
| `_currentRotaryPositionSteps` | `SemaphoreSlim _rotaryPositionLock` |
| `_targetRpm` | `Interlocked.Exchange` / `CompareExchange` |
| `_stopRequested` | `volatile bool` |
| Limit switch state | Written from GPIO callback thread, read from motion loop |

---

## Testing

30 unit tests in `Test/StepperMotorControllerTests.cs` inside `SynchronizedDualAxisControllerTests`.

Key test cases:

| Test | What it verifies |
|---|---|
| `Constructor_ShouldOpenLinearAxisPins` | All linear GPIO pins opened correctly |
| `Constructor_ShouldOpenRotaryAxisPins` | Rotary pulse and direction pins opened |
| `Constructor_ShouldOpenEnablePins_WhenConfigured` | Optional enable pins for both axes |
| `MoveInchesAsync_ShouldSetRotaryDirectionLow_WhenMovingPositive` | Rotary direction mirrors linear |
| `MoveInchesAsync_RotaryPulseCount_ShouldMatchGearRatioScaling_OneToOne` | 1:1 ratio, equal pulse counts |
| `MoveInchesAsync_RotaryPulseCount_ShouldScaleByGearRatio` | 0.5 ratio → half rotary pulses |
| `MoveInchesAsync_RotaryPositionDegrees_ShouldBeNonZeroAfterMove` | 400 rotary steps = 360° |
| `StopAsync_ShouldCompleteMotionGracefully` | Deceleration completes, no exception |
| `Dispose_ShouldBeIdempotent` | GPIO disposed exactly once |

Run with:

```powershell
dotnet test --filter "FullyQualifiedName~SynchronizedDualAxisControllerTests"
```
