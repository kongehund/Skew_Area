# Skew_Area

A plugin for OpenTabletDriver which creates a parallelogram inside the display area based on an angle "Y Skew".
Enabling the filter will constrain the input area to the parallelogram. The input is then transformed to fit the entire display area.

![Skew](https://github.com/kongehund/Skew_Area/assets/63306696/98f49615-382d-4964-9a05-93cb62afb18b)

## Status
The plugin is in a usable state, but the following should be taken into account:
- There's no limit to the angle you choose, so the parallelogram won't stay within the display area if the angle is greater than the angle to the diagonal line.
- There's some math errors, so the angle you choose isn't the same as the actual angle. This isn't a problem unless you want your parallelogram to have a specific angle instead of experimenting to find one that works for you.

## Technical description
The parallelogram has flat bottom and top edges, and the Y-axis is rotated counter-clockwise by the angle - the angle is thus the one between the vertical axis and the desired parallelogram angle. 
The parallelogram will automatically reside within the input area and maximize its size - i.e. if you have an angle of 0, you have the whole input area rectangle - when you increase the angle from 0 and upwards, the top-left and bottom-right corner of the rectangle stay put, whereas the top-right and bottom-left corner move to the left and right, respectively, turing the rectangle into a parallelogram whose horizontal edges get smaller and the whole parallelogram thinner as you increase the angle.

