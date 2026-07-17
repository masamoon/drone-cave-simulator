# Milestone 04.2 implementation notes

## Scope delivered

The Safe House now owns a persistent two-chassis fleet behind a dedicated `FleetSystem`. The original damaged Scout Field begins in the service bay. An incomplete Survey Professional begins in locker slot 1 with one motor, its dependent propeller, and the battery missing. Locker slots 2 and 3 are empty. The ready shelf remains separate deployment staging.

Market stock, money, buying, selling, and scrap spending remain absent.

## Fleet authority and physical storage

`FleetSystem` is the sole authority for whole-drone service, ready-shelf, locker, and external location. `InventorySystem` continues to own loose parts, returns, and salvage; its legacy whole-drone methods now delegate to `FleetSystem` when a fleet is configured.

The physical locker has three authored shelves, three interaction controls, and a roster that mirrors real occupancy. Pressing `E` on an occupied locker control swaps that chassis into the service bay. The existing service-bay chassis moves to the selected locker slot, so the operation never requires an unplanned temporary location. Capacity and identity are validated before mutation.

Incomplete, damaged, empty, diagnosed, and ready chassis may occupy the locker. Only a currently complete, serviceable, charged, diagnostically passed service drone may move to the ready shelf.

## Drone aggregates and socket identity

Each chassis is represented by a `DroneActor` containing its immutable frame definition, `DroneAssemblyState`, sockets, installed part instances, runtime chassis data, and physical root.

Authored socket names remain local and reusable, such as `drone.motor.front-left`. New runtime ownership and schema-5 records use `drone instance ID::local socket ID`, so both actors can use the same authored socket layout without save collisions. Schema versions 1–4 continue resolving local IDs against the migrated service actor.

The Survey chassis owns distinct runtime part identities. Removing one of its installed components through service mode transfers that same instance to parts or returns. Removing every component would leave the `DroneActor` chassis intact.

## Compatibility and progression

`PartDefinition` now exposes stable compatibility standards, equipment grade, stat modifiers, and monetary value. Existing tags migrate deterministically:

- the current motor, battery, and propeller map to Compact standards;
- Survey components use Survey standards;
- Utility components use Heavy standards;
- cameras and antennas use shared utility standards.

Six immutable `DroneFrameDefinition` assets are authored under `Resources/DroneFrames`: Scout, Survey, and Utility in Field and Professional grades. Scout prioritizes speed and low noise, Survey prioritizes endurance and observation, and Utility prioritizes durability, payload, and control. Professional variants preserve interfaces, improve performance and reliability, and cost 2.25 times the Field frame.

`DroneStatsSnapshot` is recomputed from frame stats, frame condition, installed definitions, part condition, charge, and motor matching. Mixed motor definitions apply explicit control and reliability penalties. Derived statistics are never saved.

## Service retargeting

Locker swaps retarget all service-facing systems to the selected actor:

- focused service camera and compatible socket highlights;
- diagnostic switch;
- status panel;
- inventory extraction destination;
- save/load socket ownership.

The larger Survey chassis uses the same deterministic Milestone-4.1 service interactions. A manual Play Mode pass swapped it into service, unlocked and removed one installed propeller, preserved its runtime identity, stored it in serviceable parts, and changed Survey completeness from `8/11` to `7/11`.

## Persistence

Safe House saves now use schema version 5. They store every drone runtime record, service and ready actor IDs, three locker occupant IDs, and per-drone runtime socket records alongside existing part, inventory, salvage, and fastener data.

Version-4 saves migrate the existing single drone into the service/ready fleet location without changing its identity. Authored secondary actors retain their initial locker location when absent from an older save. Laboratory scenes without a fleet continue emitting schema version 4 for regression compatibility.

## Validation completed

Validated with Unity `6000.4.8f1`:

- `44/44` Edit Mode tests passed;
- `26/26` Play Mode tests passed;
- all 58 Milestone-4.1 tests remain green;
- eight new Edit Mode tests cover standards, runtime socket uniqueness, horizontal/vertical progression, mismatch penalties, locker capacity, deterministic swaps, ready gating, schema 5, and schema-4 migration;
- four new Play Mode tests cover physical locker construction, normal `E` interaction, service/diagnostic retargeting, and full fleet save/load;
- a fresh Safe House creates two unique drone identities, 22 unique runtime socket IDs, one Scout service actor, one `8/11` Survey Professional locker actor, and two empty locker slots;
- a normal interaction with locker control 1 swaps the Survey into service;
- save/load restores service and locker identities without duplicating parts or sockets;
- after clearing screenshot-related render messages, the live service workflow produced zero runtime errors.

Visual evidence:

- `Assets/Screenshots/milestone-04-2-fleet-locker-final.png`;
- `Assets/Screenshots/milestone-04-2-survey-service.png`.

## Known limitations and tuning targets

- The locker, controls, and frame presentations are functional low-poly greybox geometry.
- The Survey currently reuses and scales the quadcopter presentation; frame-family silhouettes need later authored art, not Milestone-4.2 logic changes.
- The roster uses functional IMGUI and exact development statistics. Density, abbreviations, comparison hierarchy, and screen placement require human readability feedback.
- Locker shelf height, control placement, relocation duration, and approach route require a normal first-person comfort pass.
- The service camera uses shared framing tunables for Scout and Survey. Survey zoom limits and default framing require subjective approval.
- The existing URP punctual-light shadow-atlas warning remains. The render pipeline was not changed.

Milestone 4.3 remains unimplemented. It must not begin until locker readability, roster clarity, frame comparison clarity, service swapping, and larger-frame service framing receive human approval.
