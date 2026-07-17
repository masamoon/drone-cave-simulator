# MILESTONE 04.2 — Modular Fleet and Whole-Drone Storage

## 1. Objective

Replace the single hard-wired Safe House drone with a persistent modular fleet. The player can compare frames, store complete or incomplete drones in a three-slot physical locker, stage only a tested ready drone, and deterministically swap a locker drone into the service bay without duplicating chassis or installed parts.

Milestone 4.2 does not add money, market stock, buying, selling, or missions.

## 2. Fleet authority

`FleetSystem` is the sole authority for whole-drone location and locker occupancy. `InventorySystem` remains the sole authority for loose-part storage, returns, and salvage. Compatibility wrappers may delegate old single-drone calls to the fleet so earlier laboratory tests and schema versions remain readable, but they may not maintain a second drone-location model.

Fleet locations are explicit:

- service bay — at most one drone;
- ready shelf — at most one ready and diagnostically passed drone;
- locker slots 1–3 — damaged, incomplete, empty, or ready drones;
- external — reserved for later market and mission ownership.

Selecting a locker drone swaps it with the service-bay drone using the selected locker slot as the deterministic destination. The operation is rejected before any mutation if no valid destination exists.

## 3. Drone aggregate and identity

Each physical chassis has one `DroneActor` aggregate containing:

- one immutable `DroneFrameDefinition`;
- mutable `DroneRuntimeData`;
- one `DroneAssemblyState`;
- its authored `PartSocket` components;
- its installed runtime `InstallablePart` instances;
- its physical root transform.

Drone IDs and part IDs remain stable across service, storage, teardown, save, and load.

Authored socket names remain local, such as `drone.motor.front-left`. A `SocketRuntimeId` combines the drone instance ID and local socket ID. Only runtime IDs are used for new installed ownership and schema-5 socket records, preventing collisions when multiple drones share the same authored layout. Schema-4 local IDs resolve to the migrated service-bay drone.

## 4. Compatibility standards

`CompatibilityStandardId` replaces loose whitelist matching for new content. Existing tags remain readable and map deterministically to standards.

- Scout motors, batteries, and propellers use Compact standards;
- Survey motors, batteries, and propellers use Survey standards;
- Utility motors, batteries, and propellers use Heavy standards;
- cameras and antennas use shared utility standards.

Both Field and Professional components within a family share interfaces. Motor grades may be mixed, but four nonmatching motor definitions produce a visible control and reliability penalty.

## 5. Frame catalogue

Six immutable frame assets are authored:

| Family | Field role | Professional progression |
|---|---|---|
| Scout / Compact | fastest, quietest, inexpensive; low endurance, payload, durability | same interfaces and role, about 20% stronger performance and reliability |
| Survey / Survey | strongest endurance and observation; slower and more conspicuous | same interfaces and role, about 20% stronger performance and reliability |
| Utility / Heavy | strongest durability, payload, and control; noisy and power-hungry | same interfaces and role, about 20% stronger performance and reliability |

Professional frame value is 2.25 times the equivalent Field frame value. Derived statistics are recomputed from frame, frame condition, installed part definitions, part condition, charge, and motor matching; they are not persisted.

## 6. Safe House fleet slice

The Safe House contains:

- the current damaged Scout Field drone in the service bay;
- an incomplete Survey Professional drone in locker slot 1;
- two vacant locker slots;
- one separate ready shelf;
- a roster that mirrors physical occupancy and shows family, grade, completeness, condition, diagnostic, and major derived trade-offs;
- physical locker controls that swap the selected drone into service.

Teardown remains the Milestone-4.1 physical service interaction. Removing a component transfers that same runtime part instance to parts or returns. The empty frame remains a valid fleet actor.

## 7. Persistence

Schema version 5 stores:

- every drone runtime record;
- active service and ready drone IDs;
- the three locker occupant IDs;
- runtime socket IDs and their existing procedure progress;
- existing part identity, ownership, condition, storage, salvage, and diagnostic state.

Schema versions 1–4 remain readable. A version-4 single drone migrates into the fleet with its identity unchanged and becomes the service-bay actor. Newer authored secondary fleet actors retain their initial locations when absent from the older save. Derived statistics are recomputed after restore.

## 8. Automated validation

Edit Mode coverage must include:

- runtime socket uniqueness across two actors with identical local socket names;
- legacy tag-to-standard compatibility migration;
- family-specific motor, battery, and propeller rejection;
- shared camera and antenna compatibility;
- Scout, Survey, and Utility horizontal trade-offs;
- Professional-over-Field vertical ordering and 2.25-times value;
- mismatched-motor control and reliability penalties;
- locker capacity and unique occupancy;
- deterministic service swap and atomic rejection;
- ready-shelf readiness and diagnostic gating;
- teardown identity and empty-chassis persistence;
- schema-5 round trip and schema-4 single-drone migration.

Play Mode coverage must include:

- three visible locker bays and a roster matching occupancy;
- the Scout beginning in service and Survey Professional beginning in slot 1;
- selecting Survey swaps it with Scout deterministically;
- service camera and diagnostic retarget to the selected actor;
- incomplete and ready drones can enter the locker;
- save/load restores the complete fleet arrangement without identity duplication;
- all 58 Milestone-4.1 tests remain green.

## 9. Human acceptance

- Locker occupancy is readable from normal workshop routes.
- Frame family and grade comparisons communicate trade-offs without requiring hidden numbers.
- Swapping feels deliberate and deterministic.
- The service camera frames both Scout and larger Survey chassis comfortably.
- The player understands that the ready shelf is staging, while the locker is general fleet storage.

## 10. Exclusions and approval gate

No market, funds, listings, buying, selling, scrap spending, missions, risk, charging, crafting, frame repair recipe, permanent frame loss, or final art is included.

Milestone 4.3 must not begin until all automated tests pass, the Safe House fleet workflow is objectively validated, and locker readability, roster clarity, comparison clarity, and service swapping receive human approval.
