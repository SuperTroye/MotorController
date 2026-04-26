namespace MotorControllerApp;

/// <summary>
/// Configuration settings for the synchronized dual-axis controller that controls both a linear axis
/// and a rotary axis that must move in synchronization.
/// </summary>
public class SynchronizedDualAxisConfig
{
    /// <summary>
    /// Gets or sets the configuration for the linear axis controller.
    /// </summary>
    public LinearAxisConfig LinearAxisConfig { get; set; } = new LinearAxisConfig();

    /// <summary>
    /// Gets or sets the configuration for the rotary axis controller.
    /// The rotary axis does not use limit switches.
    /// </summary>
    public RotaryAxisConfig RotaryAxisConfig { get; set; } = new RotaryAxisConfig();

    /// <summary>
    /// Gets or sets the ratio between the rotary axis and the linear axis.
    /// This represents the number of rotations of the rotary axis per rotation of the linear axis.
    /// </summary>
    public double GearRatio { get; set; } = 0.4;
}


