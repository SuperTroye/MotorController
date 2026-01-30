using System.Device.Gpio;

namespace Step;

public class FakeGpioController : IGpioController
{
    private readonly Dictionary<int, PinState> _pins = new();
    private readonly object _lock = new();

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
                Value = PinValue.Low
            };

            Console.WriteLine($"[FakeGpio] Opened pin {pinNumber} with mode {mode}");
        }
    }

    public void ClosePin(int pinNumber)
    {
        lock (_lock)
        {
            if (!_pins.ContainsKey(pinNumber))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            _pins.Remove(pinNumber);
            Console.WriteLine($"[FakeGpio] Closed pin {pinNumber}");
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

            Console.WriteLine($"[FakeGpio] Read pin {pinNumber}: {pin.Value}");
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
            Console.WriteLine($"[FakeGpio] Write pin {pinNumber}: {value}");

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
            Console.WriteLine($"[FakeGpio] Registered callback for pin {pinNumber}, events: {eventTypes}");
        }
    }

    public void UnregisterCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
    {
        lock (_lock)
        {
            if (!_pins.TryGetValue(pinNumber, out var pin))
                throw new InvalidOperationException($"Pin {pinNumber} is not open.");

            pin.Callbacks.RemoveAll(c => c.Callback == callback);
            Console.WriteLine($"[FakeGpio] Unregistered callback for pin {pinNumber}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _pins.Clear();
            Console.WriteLine("[FakeGpio] Disposed");
        }
    }

    // Utility methods for testing
    public void SimulatePinChange(int pinNumber, PinValue value)
    {
        Write(pinNumber, value);
    }
}