Currently there is a rotary axis that is driven by gears off the lead screw. 

It rotates relative to the rotation of the lead screw.

I want to change this and drive the rotary axis using a stepper motor so that I can chage the ratio without changing gears.

Create a class that incorporates this new stepper motor, as if the rotary axis was driven by gears.

The linear movoment must be syncronized with the rotary movement with no drift.

This should be done by scaling the delay of the rotary axis by the gear ratio.

Since the new stepper only turns it won't have a min and max limit switch.

The rotary axis should automatically follow the linear axis's acceleration profile through the synchronization mechanism.