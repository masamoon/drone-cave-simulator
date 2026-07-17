# MILESTONE 03 — Complete Drone Mounting and Serviceability

## 1. Objective

Turn the reusable Milestone 2 component interactions into one persistent contemporary quadcopter assembly. The player must be able to service a complete mounted drone, identify an installed damaged motor and depleted battery, remove them through the authored reverse procedures, install serviceable replacements, and verify mission readiness.

This milestone includes complete assembly state, condition-aware readiness, battery charge state, reversible replacement, derived statistics, and tactile audio feedback. It does not include storage/inventory ownership, charging, salvage, missions, flight, or workshop risk.

## 2. Required drone

The `DroneAssemblyLab` contains one workshop quadcopter with:

- four motor sockets;
- four corresponding propeller locks;
- one battery tray and latch;
- one two-fastener camera bracket;
- one keyed antenna connector;
- one persistent drone assembly identity;
- one diagnostic control.

All final transforms are authored and deterministic. Propellers block motor removal until removed.

## 3. Initial service scenario

The drone begins physically complete but not mission-ready:

- the rear-left motor is installed and damaged;
- the installed battery is depleted;
- all remaining installed components are serviceable;
- one serviceable replacement motor and one charged replacement battery are loose on the workbench.

The intended player flow is:

1. run the diagnostic and receive a maintenance result;
2. unlock and remove the rear-left propeller;
3. loosen and extract the damaged rear-left motor;
4. install and fasten the serviceable motor;
5. reinstall and lock the propeller;
6. open the battery latch and extract the depleted battery;
7. guide the charged battery into the tray and close the latch;
8. run the diagnostic and receive a ready result.

All other mounted components remain removable and reinstallable through the shared framework.

## 4. Runtime data

Immutable part definitions retain category, compatibility, reliability, mass, power draw, and capability. Mutable runtime instances retain condition and battery charge level in addition to interaction, ownership, and persistence state.

Condition bands:

- serviceable: `>= 45%`;
- damaged: `< 45%`;
- failed: `< 20%`.

A battery at `<= 5%` charge is depleted. Charging is excluded; replacement is the only Milestone 3 recovery action.

## 5. Drone readiness

The assembly derives:

- completeness;
- overall condition;
- reliability;
- endurance from installed battery charge and condition;
- observation quality from the camera;
- control reliability from motors and antenna;
- a readable maintenance summary;
- mission-ready state.

Mission-ready requires every required socket category, no damaged required part, and a non-depleted battery.

## 6. Audio feedback

Procedural audio must provide distinct layered feedback for:

- pickup and drop;
- guidance capture and cancellation;
- seating/contact;
- twist detents;
- screwdriver ratchet;
- final torque;
- latch open/close;
- extraction;
- diagnostic success/failure.

Audio is synthesized at runtime and requires no external asset package.

## 7. Persistence

Milestone 3 saves use `under-static-milestone-03.json`. Persist all part identities, condition, charge, stable interaction state, world pose, socket occupancy, and procedure progress. Existing Milestone 1 and 2 save loading remains compatible where possible.

## 8. Acceptance

- Milestone 1 and 2 regression tests pass.
- A complete assembly reports `11/11` mounted parts.
- Initial readiness fails for the damaged motor and depleted battery.
- Replacing only the battery still fails because of the motor.
- Replacing both faults produces mission-ready state.
- Propeller occupancy blocks removal of its underlying motor.
- Condition and charge survive save/load.
- The complete replacement flow works in actual Play Mode input.
- No recurring console errors remain.

## 9. Exclusions

- battery charging, heat, or unsafe-state simulation;
- physical inventory/storage and quantities;
- salvage or cannibalization;
- mission assignment or deployment;
- direct drone flight or aerodynamics;
- additional drone categories;
- final art, visible hands, or third-party packages.
