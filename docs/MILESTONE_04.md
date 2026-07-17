# MILESTONE 04 — Service Mode, Physical Inventory, and Readiness

## 1. Objective

Turn the Safe House placeholders into a small inventory and repair loop. The player approaches the service bay and deliberately enters a focused bench mode, repairs and tests the existing drone with a persistent inventory panel, places removed faulty equipment into returns or deliberately salvages it, and moves the tested drone between the service bay and ready shelf.

This milestone proves visible ownership and deterministic storage. It does not add missions, charging, repair crafting, workshop exposure, discovery events, or campaign progression.

## 2. Required Safe House flow

1. The service drone begins in the service bay with the existing damaged rear-left motor and depleted battery.
2. The serviceable replacement motor and charged battery begin in the parts storage rack.
3. The player enters service mode from an authored control beside the drone. The camera focuses on the drone and locomotion is suspended.
4. The player can orbit and zoom the service camera, point and click components, and drag stored parts from an always-visible inventory panel to compatible sockets.
5. Removed damaged or depleted equipment returns to inventory and can be dragged to a deliberate salvage target.
6. A damaged loose part can still be physically salvaged through the existing two-step confirmation at the salvage bin outside service mode.
7. Salvage produces visible scrap tokens; spending scrap is excluded.
8. After both faults are repaired, the player runs the existing diagnostic.
9. Only a mission-ready drone with a successful current diagnostic can move to the ready shelf.
10. The same drone can return from the ready shelf to the service bay without duplication.

## 3. Dedicated service mode

Repair interaction uses a PC-building-game style focused mode rather than requiring the player to walk around the table while carrying components.

- `E` on the service control enters the mode only while the drone is in the service bay.
- Drone-mounted parts, sockets, fasteners, latches, and diagnostics are not targetable through ordinary
  first-person interaction. The external service control is the maintenance entry point.
- Motors below the serviceability threshold show an authored warning band and stripe on the motor housing,
  so the replacement target is identifiable directly from the drone in service view.
- The first-person controller and ordinary center-screen interaction are suspended while active.
- The camera eases to an authored drone focus, supports right-mouse orbit and wheel zoom, and restores the exact prior pose on exit.
- A persistent sidebar mirrors the physical parts and returns locations without creating a second ownership model.
- Pointing at a component or socket highlights it and shows its current procedure action.
- Dragging a compatible inventory part onto an empty socket deterministically performs `Loose → Held → Guided → Seated`; releasing over the highlighted socket completes seating and preserves the authored fastening, twist-lock, or latch step.
- Holding the pointer action on a fastener spawns the screwdriver at that screw and drives it. Releasing or completing the action despawns the tool. Twist locks use the pointer action directly; latches use deliberate clicks.
- Diagnostics run from an explicit service-mode control.
- Extracted parts return to the appropriate authored inventory location and remain the same runtime instance.
- Dragging an eligible damaged part to the visible salvage target is the confirmation gesture and consumes it once.
- `Escape` or the visible exit control leaves service mode. Service mode itself is transient and is not persisted.

The drone is not freely rotated as a physics object. Orbiting the authored camera provides equivalent inspection access while retaining deterministic world and save transforms.

## 4. Storage model

Storage locations use immutable `StorageLocationDefinition` ScriptableObjects and stable `StorageLocationId` values. A location definition contains:

- stable ID;
- display name;
- storage kind;
- accepted part categories;
- capacity.

The Safe House requires:

- `safehouse.parts` — serviceable parts storage;
- `safehouse.returns` — damaged or depleted returns;
- `safehouse.salvage` — confirmed salvage input;
- `safehouse.service-bay` — drone service location;
- `safehouse.ready-shelf` — tested mission-ready drone location.

Loose parts stored in a rack use authored slot transforms. They remain existing runtime instances, use controlled physics while stored, and return to normal held or loose behaviour when removed. Capacity, compatibility, and occupancy changes must be explicit and deterministic.

## 5. Runtime ownership

`PartRuntimeData` gains a stable storage-location ID and a salvaged flag. The existing free-form owner string remains only as a readable compatibility field for schema versions 1–3.

Rules:

- a part has at most one storage location;
- an installed part belongs to its assembly socket, never a storage slot;
- pickup releases storage occupancy before entering `Held`;
- ordinary drop moves the part to the workshop-loose location;
- storing a held part returns it to stable `Loose` state at an authored slot;
- salvage removes the instance from usable inventory exactly once;
- rejected storage and salvage actions do not mutate ownership.

Removing a usable installed component and placing it into storage is the Milestone 4 cannibalization action. No separate recipe or dismantling menu is added.

## 6. Drone runtime and ready shelf

The assembled drone gains mutable `DroneRuntimeData` containing:

- persistent drone instance ID;
- current stable location ID;
- whether the latest diagnostic passed.

Any assembly installation or removal invalidates the previous diagnostic. Running the diagnostic records the current result. The ready shelf accepts the drone only when the assembly is mission-ready and the latest diagnostic passed.

Drone relocation is an authored deterministic movement between service-bay and ready-shelf anchors. Installed
parts remain attached through assembly state, and each occupied socket reasserts its authored component pose
throughout relocation so child rigidbodies cannot visually separate. Loose parts are not moved with the drone.

## 7. Salvage

Only loose damaged or failed parts may be salvaged. Depleted but otherwise serviceable batteries belong in returns and are not salvageable in this milestone.

Outside service mode, the first interaction arms the salvage bin for the held instance for a short confirmation window and a second interaction consumes it. In service mode, dragging the same eligible part onto the explicit salvage target is itself the deliberate confirmation gesture. Successful salvage:

- marks the runtime instance salvaged;
- removes it from all storage occupancy;
- disables its world representation;
- increments scrap by the immutable definition yield;
- refreshes visible scrap tokens.

Scrap has no use during Milestone 4.

## 8. Persistence

Safe House saves upgrade to schema version 4 and retain schema 1–3 loading.

Persist:

- stable part location IDs;
- storage occupancy by unique part instance ID;
- salvaged state;
- scrap count;
- drone instance ID;
- drone service-bay or ready-shelf location;
- latest diagnostic result;
- all existing part, socket, condition, charge, and procedure state.

Loading validates unique occupancy, restores stored parts to authored slots, restores salvaged parts as unavailable, restores the drone to its deterministic anchor, and recomputes readiness from installed runtime parts.

Legacy owner strings migrate to stable locations without changing older lab behaviour.

## 9. Automated tests

### Edit Mode

- storage capacity and category rules reject without mutation;
- one instance cannot occupy two locations;
- pickup releases its exact slot;
- returns accepts damaged parts and depleted batteries;
- salvage requires two interactions with the same damaged instance;
- salvage rejects serviceable parts and cannot duplicate yield;
- ready shelf requires current mission readiness and a passed diagnostic;
- version 4 persistence restores storage, salvage, and drone runtime;
- versions 1–3 remain readable.
- service placement rejects incompatible or occupied sockets without changing ownership;
- service placement uses the guided and seated states deterministically;
- service extraction preserves the same unique part instance and assigns valid storage;
- service-mode salvage rejects ineligible parts and cannot duplicate yield.

### Play Mode

- Safe House creates all required inventory locations and deterministic slots;
- replacement motor and battery begin in parts storage;
- stored parts can be picked up through normal interaction;
- removed faulty parts can enter returns;
- a damaged part can be confirmed and salvaged;
- repaired and tested drone moves to the ready shelf and back;
- save/load restores the complete inventory arrangement without duplication;
- all Milestone 1–3 tests remain green.
- Safe House service mode suspends locomotion, restores the camera on exit, and exposes stored replacements without duplicating them.

## 10. Manual acceptance

- Entering and leaving service mode is immediate, readable, and restores the player cleanly.
- The full drone can be inspected without walking around the table.
- The replacement motor can be dragged from inventory to the correct empty socket without 3D pose wrestling.
- Pointer targets remain readable while orbiting and zooming.
- Fastening, twist-lock, latch, removal-order, and diagnostic requirements remain understandable.
- Returning an extracted part to inventory is deterministic and does not fight socket guidance.
- Returns and salvage are visually distinct.
- The salvage confirmation prompt makes destructive intent clear.
- Ready-shelf rejection explains whether maintenance or diagnostic testing is missing.
- Drone relocation is readable, deterministic, and preserves the assembly.
- Save/load preserves the visible room arrangement.
- No recurring console errors remain.

## 11. Exclusions

- battery charging or unsafe-battery simulation;
- repair recipes or scrap spending;
- missions or deployment choices;
- workshop exposure or concealment simulation;
- discovery sequences;
- operational shifts or campaign progression;
- multiple drones;
- direct flight, visible hands, final art, or third-party packages.

## 12. Definition of done

Milestone 4 is complete when the required storage, returns, salvage, ready-shelf, persistence, automated tests, and objective Play Mode checks pass. Subjective inventory readability and relocation feel must then receive human approval before Milestone 5 begins.
