using System.Device.Gpio;

namespace Step;

public interface IGpioController : IDisposable
{
    void OpenPin(int pinNumber, PinMode mode);
    void ClosePin(int pinNumber);
    bool IsPinOpen(int pinNumber);
    PinValue Read(int pinNumber);
    void Write(int pinNumber, PinValue value);
    void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback);
    void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback);
}

public class GpioControllerWrapper : IGpioController
{
    private readonly GpioController _gpioController;

    public GpioControllerWrapper(GpioController gpioController) =>
        _gpioController = gpioController ?? throw new ArgumentNullException(nameof(gpioController));

    public void OpenPin(int pinNumber, PinMode mode) => _gpioController.OpenPin(pinNumber, mode);

    public void ClosePin(int pinNumber) => _gpioController.ClosePin(pinNumber);

    public bool IsPinOpen(int pinNumber) => _gpioController.IsPinOpen(pinNumber);

    public PinValue Read(int pinNumber) => _gpioController.Read(pinNumber);

    public void Write(int pinNumber, PinValue value) => _gpioController.Write(pinNumber, value);

    public void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback) =>
        _gpioController.RegisterCallbackForPinValueChangedEvent(pinNumber, eventTypes, callback);

    public void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback) =>
        _gpioController.UnregisterCallbackForPinValueChangedEvent(pinNumber, callback);

    public void Dispose()
    {
        _gpioController?.Dispose();
    }
}
