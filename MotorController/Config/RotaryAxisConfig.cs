
namespace MotorControllerApp;

/// <summary>
/// Configuration settings for a rotary axis stepper motor.
/// </summary>
/// <remarks>
/// The rotary axis does not have limit switches since it rotates continuously.
/// Only pulse, direction, and optional enable pins are used.
/// The rotary axis automatically follows the linear axis acceleration profile through synchronization.
/// </remarks>
public class RotaryAxisConfig : IBaseConfig
{
    /// <summary>
    /// Gets or sets the GPIO pin number used to output the pulse signal.
    /// </summary>
    public int PulsePin { get; set; } = 16;

    /// <summary>
    /// Gets or sets the GPIO pin number used to control the direction of the motor.
    /// </summary>
    public int DirectionPin { get; set; } = 12;

    /// <summary>
    /// Gets or sets the pin number used to enable the motor, if applicable.
    /// </summary>
    public int? EnablePin { get; set; } = null;

    /// <summary>
    /// Gets or sets the number of steps per revolution for the rotary stepper motor.
    /// </summary>
    public StepsPerRevolution StepsPerRevolution { get; set; } = StepsPerRevolution.SPR_400;
}

