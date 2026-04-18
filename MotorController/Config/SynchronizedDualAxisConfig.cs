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
    /// Gets or sets the gear ratio between the rotary axis and the linear axis.
    /// This represents the number of rotations of the rotary axis per inch of linear travel.
    /// </summary>
    /// <remarks>
    /// For example, if the rotary axis should make 2 full rotations per inch of linear movement,
    /// set this value to 2.0. The controller will automatically synchronize the rotary motor's
    /// step timing to maintain this ratio throughout acceleration, constant speed, and deceleration phases.
    /// </remarks>
    public double GearRatio { get; set; } = 2.0;
}


