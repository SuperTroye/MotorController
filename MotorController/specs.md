Stepper Motor Controller
Create a c# class to control a stepper motor driver.

Use System.Device.Gpio to control pins on a Raspberry Pi. 

Create an interface called IGpioController to abstract GPIO operations, and to make unit testing easier. It should implement IDisposable and have methods for opening pins, writing to pins, reading from pins, and closing pins.

Create a wrapper class called GpioControllerWrapper that implements IGpioController and uses System.Device.Gpio.GpioController internally.

The pins are for Pulse, Direction, and an optional Enable pin.

Create a settings class called ControllerConfig with the following properties:
- PulsePin (int, default 21)
- DirectionPin (int, default 20)
- EnablePin (int, optional)
- StepsPerRevolution (int, default 400)
- LeadScrewThreadsPerInch (double, default 5)
- Acceleration (double, default 5000)
- MinLimitSwitchPin (int, default 24)
- MaxLimitSwitchPin (int, default 23)

The ControllerConfig class will be used to pass configuration to the StepperMotorController class.

There will be 2 limit switches to constrain maximum and minimum travel.

The StepperMotorController class should incorporate acceleration and deceleration when starting and stopping the motor. Use a linear acceleration profile.

The Acceleration property (in steps per second squared) will be used to set motor acceleration.

The methods to control motion uses RPM for stepper motor speed.

The stepper motor will turn a lead screw. Use the LeadScrewThreadsPerInch property to set this value and a method to Move, with a parameter of inches. This will divide the desired length by the threads per inch for the distance.

Create a method to run the motor until a limit switch is detected.

Create a method to stop the motor, incorporating deceleration.

Create a method to home the motor to the minimum limit switch. This will set the current position to zero. Do not use deceleration, stop the motor immediately.

Create a method to set the current position in inches to zero.

Create a property for current position in inches which is thread safe and can be read at any time.

Create a boolean property for the limit switches (IsMinLimitSwitchTriggered and IsMaxLimitSwitchTriggered) which return true if they are triggered. This will be used by the User Interface.

Make all methods asynchronous to avoid blocking the main thread. 

The Stop method should be able to be called from another thread while the motor is running, so have it set a CancellationToken to stop.

The method to control motion should throw exceptions for invalid parameters, such as moving beyond limit switches or invalid RPM values.

The limit switches should be read by using GpioController.RegisterCallbackForPinValueChangedEvent.


GUI
Create a small c# GTK application using GirCore.Gtk-4.0 with a single window.

Make the window size fit a Raspberry Pi 5 inch touchscreen 800x480 display.

Create 2 buttons to run to min and max limit switches. They should have a left and right arrow icon respectively. These will call the RunToLimitSwitchAsync method in the StepperMotorController class.

Create a button to Home the motor. This will call the Home method in the StepperMotorController class.

Create 2 circles to show the minimum and maximum limit switch status. This should check the boolean properties (IsMinLimitSwitchTriggered and IsMaxLimitSwitchTriggered) in the StepperMotorController class. If the limit switch is triggered, the circle should be red, otherwise green.

Create 2 labels to show motor speed in RPM and current position in inches.

Create a text input to set the desired speed in RPM. Validate the input to ensure it is a valid number and within acceptable range for the motor.

Create a text input to set the desired position in inches. Validate the input to ensure it is a valid number and within the travel limits defined by the limit switches.

Create a button to Move to the desired position. This will call the MoveInchesAsync method in the StepperMotorController class with the value from the text input.

Make sure to build the solution to make sure there are no errors.



Testing
Create a FakeGpioController class that implements IGpioController that will be used to debug the application on Windows.

Create comprehensive unit tests for the StepperMotorController class. Use NSubstitute for mocking and XUnit for testing framework and Assertions.