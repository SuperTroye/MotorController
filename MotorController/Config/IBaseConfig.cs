
namespace MotorControllerApp;

/// <summary>
/// Base Configuration settings for a stepper motor.
/// </summary>
public interface IBaseConfig
{
    /// <summary>
    /// Gets or sets the GPIO pin number used to output the pulse signal.
    /// </summary>
    int PulsePin { get; set; }

    /// <summary>
    /// Gets or sets the GPIO pin number used to control the direction of the motor.
    /// </summary>
    int DirectionPin { get; set; }

    /// <summary>
    /// Gets or sets the pin number used to enable the motor, if applicable.
    /// </summary>
    int? EnablePin { get; set; }

    /// <summary>
    /// Gets or sets the number of steps per revolution for the rotary stepper motor.
    /// </summary>
    StepsPerRevolution StepsPerRevolution { get; set; }
}

