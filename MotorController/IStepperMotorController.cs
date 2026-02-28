
namespace MotorControllerApp;

public interface IStepperMotorController : IDisposable
{
    /// <summary>
    /// Asynchronously moves the device the specified distance in inches at the given speed in revolutions per minute
    /// (RPM).
    /// </summary>
    /// <param name="inches">The distance to move, in inches. Can be positive or negative to indicate direction.</param>
    /// <param name="rpm">The speed at which to move, in revolutions per minute. Must be greater than zero.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the move operation.</param>
    /// <returns>A task that represents the asynchronous move operation.</returns>
    Task MoveInchesAsync(double inches, double rpm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously resets the position to its initial state.
    /// </summary>
    /// <returns>A task that represents the asynchronous reset operation.</returns>
    Task ResetPositionAsync();

    /// <summary>
    /// Moves the carriage toward the specified limit switch at the given speed until the switch is reached or the
    /// operation is canceled.
    /// </summary>
    /// <param name="toMaxLimit">true to move toward the maximum limit switch; false to move toward the minimum limit switch.</param>
    /// <param name="rpm">The speed, in revolutions per minute, at which to move the carriage. Must be greater than zero.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the carriage reaches the specified
    /// limit switch or the operation is canceled.</returns>
    Task RunToLimitSwitchAsync(bool toMaxLimit, double rpm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the target speed in revolutions per minute (RPM).
    /// </summary>
    /// <param name="rpm"></param>
    void SetTargetRpm(double rpm);

    /// <summary>
    /// Asynchronously stops the carriage.
    /// </summary>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Gets the current position of the carriage in inches.
    /// </summary>
    double CurrentPositionInches { get; }

    /// <summary>
    /// Gets a value indicating whether the maximum limit switch is triggered.
    /// </summary>
    bool IsMaxLimitSwitchTriggered { get; }

    /// <summary>
    /// Gets a value indicating whether the minimum limit switch is triggered.
    /// </summary>
    bool IsMinLimitSwitchTriggered { get; }

    /// <summary>
    /// Occurs when the minimum limit switch is triggered.
    /// </summary>
    /// <remarks>This event is typically raised when a device or mechanism reaches its minimum allowable
    /// position or state, as detected by a limit switch. Subscribers can use this event to perform actions such as
    /// stopping movement or initiating safety procedures.</remarks>
    event EventHandler? MinLimitSwitchTriggered;
    
    /// <summary>
    /// Occurs when the maximum limit switch is triggered.
    /// </summary>
    /// <remarks>This event is typically raised when a device or mechanism reaches its maximum allowable
    /// position or state, as detected by a limit switch. Subscribers can use this event to perform actions such as
    /// stopping movement or initiating safety procedures.</remarks>
    event EventHandler? MaxLimitSwitchTriggered;
}
