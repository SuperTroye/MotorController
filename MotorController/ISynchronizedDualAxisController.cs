namespace MotorControllerApp;

/// <summary>
/// Extends <see cref="IStepperMotorController"/> with rotary axis position feedback for a
/// synchronized dual-axis controller that drives a linear axis and a rotary axis simultaneously.
/// </summary>
/// <remarks>
/// The rotary axis follows the linear axis acceleration profile automatically through the
/// synchronization mechanism. Its speed is derived by scaling the linear axis step delay by
/// the configured gear ratio, with a DDA (Digital Differential Analyzer) accumulator ensuring
/// zero long-term drift between the two axes.
/// </remarks>
public interface ISynchronizedDualAxisController : IStepperMotorController
{
    /// <summary>
    /// Gets the current angular position of the rotary axis in degrees.
    /// </summary>
    double CurrentRotaryPositionDegrees { get; }
}
