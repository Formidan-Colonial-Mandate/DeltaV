# DeltaV

## Delta-V Script: Quick Use Guide

This script outputs delta-V by thrust group and shows directional burn times. It supports runtime configuration using toolbar arguments and works with HUDLCD.
------------------------------------------------------------------------------------------------------------------------------------------------------------

## Setup
- Install the script into a programmable block.
- Create a text panel named exactly: DeltaV LCD*
- Set the text panel's content type to Text and Image.
- Make sure your cockpit is:
- On the same grid as the PB
- Oriented in the way you want to fly
- OPTIONAL: name a cockpit DeltaVCockpit to always use that cockpit’s orientation for calculations.
------------------------------------------------------------------------------------------------------------------------------------------------------------

## Output
The script shows:

Delta-V per group (or total forward thrust, minus RCS, if no groups are defined)
Burn time (in seconds) for each thrust direction
------------------------------------------------------------------------------------------------------------------------------------------------------------

## CustomData (optional)

You can define custom thruster groups in the Delta V LCD’s CustomData field like this:

groups=Main,Boost,Efficient,All Thrust

Then create terminal groups with those names that include the thrusters you want.
The script will calculate delta-V for each group.

You can also adjust the HudLCD data. This will not be touched by the script again unless you run the reset argument.
------------------------------------------------------------------------------------------------------------------------------------------------------------

## Arguments

Add the programmable block to a toolbar (if you want) and assign one of the following arguments:

**rcsToggle**
Toggles whether RCS thrusters are included in delta-V and burn time calculations.

**burnTimeToggle**
Toggles display of burn times by direction.

**allDirectionsToggle**
Toggles whether burn times are shown for all directions or just forward.

**reset**
**WARNING:** destructive. Resets custom data to factory settings, your configurations will be lost.
Use this if you broke something. I do not recommend binding this to a toolbar button.

These toggles update live: no script edit needed.
------------------------------------------------------------------------------------------------------------------------------------------------------------

## Notes

RCS thrusters aligned forward are always excluded from delta-V, even if RCS is enabled.
RCS will be included in any custom group delta-V calculations if they are added to the group.

Fuel usage is based on full-thrust values per thruster type.
Should these values change, the data will be wrong until the script is updated.

Only hydrogen thrusters are supported.
