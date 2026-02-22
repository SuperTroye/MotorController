using Microsoft.Extensions.Logging;
using System.Device.Gpio;

namespace MotorControllerApp;

public class FakeGpioController : IGpioController
{
    private readonly Dictionary<int, PinState> _pins = new();
    private readonly object _lock = new();


    ILogger<FakeGpioController> _logger;
    public FakeGpioController(ILogger<FakeGpioController> logger)
    {
        _logger = logger;
    }

    private class PinState
    {
        public PinMode Mode { get; set; }
        public PinValue Value { get; set; }
        public List<(PinEventTypes EventTypes, PinChangeEventHandler Callback)> Callbacks { get; } = new();
    }

    public void OpenPin(int pinNumber, PinMode mode)
    {
        lock (_lock)
        {
            if (_pins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is already open.");

            _pins[pinNumber] = new PinState
            {
                Mode = mode,
                Value = PinValue.High
            };

            _logger.LogInformation($"[FakeGpio] Opened pin {pinNumber} with mode {mode}");
        }
    }

    public void ClosePin(int pinNumber)
    {
        lock (_lock)
        {
            if (!_pins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            _pins.Remove(pinNumber);
            _logger.LogInformation($"[FakeGpio] Closed pin {pinNumber}");
        }
    }

    public bool IsPinOpen(int pinNumber)
    {
        lock (_lock)
        {
            return _pins.ContainsKey(pinNumber);
        }
    }

    public PinValue Read(int pinNumber)
    {
        lock (_lock)
        {
            if (!_pins.TryGetValue(pinNumber, out var pin))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            _logger.LogInformation($"[FakeGpio] Read pin {pinNumber}: {pin.Value}");
            return pin.Value;
        }
    }

    public void Write(int pinNumber, PinValue value)
    {
        lock (_lock)
        {
            if (!_pins.TryGetValue(pinNumber, out var pin))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            var oldValue = pin.Value;
            pin.Value = value;
            _logger.LogInformation($"[FakeGpio] Write pin {pinNumber}: {value}");

            // Simulate callbacks if value changed
            if (oldValue != value)
            {
                var eventType = value == PinValue.High ? PinEventTypes.Rising : PinEventTypes.Falling;
                foreach (var (eventTypes, callback) in pin.Callbacks)
                {
                    if (eventTypes.HasFlag(eventType))
                    {
                        Task.Run(() => callback(this, new PinValueChangedEventArgs(eventType, pinNumber)));
                    }
                }
            }
        }
    }

    public void RegisterCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
    {
        lock (_lock)
        {
            if (!_pins.TryGetValue(pinNumber, out var pin))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            pin.Callbacks.Add((eventTypes, callback));
            _logger.LogInformation($"[FakeGpio] Registered callback for pin {pinNumber}, events: {eventTypes}");
        }
    }

    public void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
    {
        lock (_lock)
        {
            if (!_pins.TryGetValue(pinNumber, out var pin))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            pin.Callbacks.RemoveAll(c => c.Callback == callback);
            _logger.LogInformation($"[FakeGpio] Unregistered callback for pin {pinNumber}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _pins.Clear();
            _logger.LogInformation("[FakeGpio] Disposed");
        }
    }

    // Utility methods for testing
    public void SimulatePinChange(int pinNumber, PinValue value)
    {
        Write(pinNumber, value);
    }
}