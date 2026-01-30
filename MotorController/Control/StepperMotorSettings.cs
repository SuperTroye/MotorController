
namespace MotorController;

/// <summary>
/// Provides configuration settings for controlling a stepper motor and its associated hardware pins.
/// </summary>
/// <remarks>This class encapsulates parameters required to interface with a stepper motor, including GPIO pin
/// assignments for pulse, direction, limit switches, and optional enable functionality. It also includes motor-specific
/// settings such as steps per revolution, lead screw pitch, and acceleration rate. These settings are typically used to
/// initialize and configure motor control logic in embedded or automation applications.</remarks>
public class StepperMotorSettings
{
    /// <summary>
    /// Gets or sets the GPIO pin number used to output the pulse signal.
    /// </summary>
    public int PulsePin { get; set; } = 21;

    /// <summary>
    /// Gets or sets the GPIO pin number used to control the direction of the device.
    /// </summary>
    public int DirectionPin { get; set; } = 20;

    /// <summary>
    /// Gets or sets the GPIO pin number used to detect the minimum limit switch position.
    /// </summary>
    public int MinLimitSwitchPin { get; set; } = 23;

    /// <summary>
    /// Gets or sets the GPIO pin number used for the maximum limit switch.
    /// </summary>
    public int MaxLimitSwitchPin { get; set; } = 24;

    /// <summary>
    /// Gets or sets the pin number used to enable the device, if applicable.
    /// </summary>
    public int? EnablePin { get; set; } = null;

    /// <summary>
    /// Gets or sets the number of steps per revolution for the stepper motor
    /// </summary>
    public int StepsPerRevolution { get; set; } = 400;

    /// <summary>
    /// Gets or sets the number of threads per inch for the lead screw.
    /// </summary>
    public double LeadScrewThreadsPerInch { get; set; } = 5.0;

    /// <summary>
    /// Gets or sets the acceleration/deceleration rate in steps per second squared
    /// </summary>
    public double AccelerationStepsPerSecondSquared { get; set; } = 7000;
}


