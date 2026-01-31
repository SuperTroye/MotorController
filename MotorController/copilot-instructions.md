Stepper Motor Controller
Create a c# class to control a stepper motor driver.

Use System.Device.Gpio to control pins on a Raspberry Pi. 

Create an interface called IGpioController to abstract GPIO operations, and to make unit testing easier. It should implement IDisposable and have methods for opening pins, writing to pins, reading from pins, and closing pins.

Create a wrapper class called GpioControllerWrapper that implements IGpioController and uses System.Device.Gpio.GpioController internally.

The pins are for Pulse, Direction, and an optional Enable pin.


Create a settings class called StepperMotorSettings with the following properties:
- PulsePin (int)
- DirectionPin (int)
- EnablePin (int, optional)
- StepsPerRevolution (int, default 400)
- LeadScrewThreadsPerInch (double, default 5)
- Acceleration (int, default 7000)
- MinLimitSwitchPin (int)
- MaxLimitSwitchPin (int)
- AccelerationStepsPerSecondSquared (double)

This class will be used to pass configuration to the StepperMotorController.

There will be 2 limit switches to constrain maximum and minimum travel.

Use the acceleration/deceleration property (in steps per second squared) to set motor acceleration. The default value is 7000 steps per second squared if no value is given.

The method to control motion uses RPM for stepper motor speed.

Use Stopwatch class to determine time between steps.

Create a method to run the motor until a limit switch is detected.

The stepper motor will turn a lead screw with 5 threads per inch. Use the "LeadScrewThreadsPerInch" property to set this value and a method to Move, with a parameter of inches. This will divide the desired length by the threads per inch for the distance.

Create a method to stop the motor, incorporating deceleration.

Create a method to home the motor to the minimum limit switch. This will set the current position to zero. Do not use deceleration, stop the motor immediately.

Create a method to set the current position in inches to zero.

Create a property for current position in inches which is thread safe and can be read at any time.

Create a boolean property for the limit switches which return if they are triggered. This will be used by the User Interface.

Make all methods asynchronous to avoid blocking the main thread. The Stop method should be able to be called from another thread while the motor is running, so have it set a CancellationToken to stop.

The method to control motion should throw exceptions for invalid parameters, such as moving beyond limit switches or invalid RPM values.

The limit switches should be read by using GpioController RegisterCallbackForPinValueChangedEvent.



Testing
Create a FakeGpioController class that implements IGpioController that will be used to debug the application on Windows.

Create comprehensive unit tests for the StepperMotorController class. Use NSubstitute for mocking and XUnit for testing framework and Assertions.



GUI
Create a small c# GTK application using GirCore.Gtk-4.0 with a single window.

Make the window size fit a Raspberry Pi 5 inch touchscreen 800x480 display.

It has a button to start the motor, a button to Home the motor, 2 boxes and 2 circles to show the minimum and maximum limit switch status.

Box 1 shows motor speed.
Box 2 shows the current position.

Add 2 circles at the top of the window to show limit switch status. Green fill color means no limit switch detected and red means the limit switch is detected.

Make sure to build the solution to make sure there are no errors.