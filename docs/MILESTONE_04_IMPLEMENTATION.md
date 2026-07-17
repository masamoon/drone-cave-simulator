# Milestone 04 implementation notes

## Scope delivered

The Safe House now contains a physical inventory layer around the existing complete service drone. The two serviceable replacement parts begin in authored slots on the parts rack. Removed faulted equipment can be placed into a separate returns rack, damaged loose parts can be deliberately salvaged, and a successfully repaired and diagnosed drone can move between deterministic service-bay and ready-shelf anchors.

The Interaction Lab, Drone Assembly Lab, and Drone Build Lab remain inventory-free regression scenes.

## Service-mode revision

Repairing in the Safe House now uses a dedicated service view. The player enters at the bench, locomotion and center-screen interaction pause, and the camera focuses on the drone. Middle-mouse drag orbits, the wheel zooms, left mouse tightens or drags, and right mouse loosens the specifically pointed screw. Exiting restores the exact first-person camera parent and pose.

The inventory sidebar is a view over the authored storage locations, not a parallel bag. Dragging a replacement out of the sidebar releases its exact storage slot and transitions that same runtime instance into a controlled visible 3D drag. Compatible sockets highlight, guidance begins near an authored target, and a short cursor-driven insertion gesture advances the part through `Held → Guided → Seated`. This removes freehand precision wrestling while preserving compatibility, prerequisites, fastening, latch, twist-lock, diagnostic, and persistence rules.

Each motor screw is now an authored `FastenerTarget` with an exact motor-base mounting pose, moving drive pose, drive axis, thread axis, and short extraction travel. Progress is continuous from fully loosened `0` to fully tightened `1`, and direction can reverse at any intermediate value. The component is extractable only when every fastener is at zero and installed only when every fastener is at one. Beginning to loosen an installed component immediately invalidates its diagnostic; fully retightening reinstalls it but still requires a new diagnostic.

Extracted parts are automatically returned to serviceable parts or faulted returns according to condition and charge. Eligible damaged parts can be dragged from the sidebar onto the explicit salvage target; the drag is the destructive confirmation gesture. The physical racks and two-step world salvage interaction remain available outside service mode.

## Storage and ownership

`StorageLocationDefinition` ScriptableObjects define the serviceable-parts rack, faulted-returns rack, and salvage bin. Runtime `StorageLocation` components own authored slot transforms and reject incompatible, full, or invalid operations without changing ownership.

`StorageLocationId` is now the authoritative runtime location. `PartRuntimeData.currentOwner` remains as a readable compatibility field for old save schemas and existing debug displays. Installed parts use assembly-socket location IDs, held parts use `player.held`, ordinary loose drops use `workshop.loose`, and stored parts use their Safe House location IDs.

Picking up a stored part releases its exact slot before the existing `Loose → Held` interaction. While carrying a part, the focus ray ignores that same held part so the player can target a storage bin behind it. Storing returns the part to stable `Loose` state with controlled physics at a deterministic anchor.

Removing an installed serviceable component and storing it is the implemented cannibalization action. No recipe or abstract inventory menu was added.

## Salvage

The salvage bin accepts only damaged or failed loose parts. The first `E` arms confirmation for that unique runtime instance; a second `E` within the confirmation window consumes it. Successful salvage disables the same world instance, marks it persistently salvaged, increments immutable definition yield, and creates visible scrap tokens in the bin.

Serviceable parts and merely depleted batteries are rejected. Scrap spending remains absent.

## Drone readiness and relocation

`DroneRuntimeData` persists the drone instance ID, stable room location, and latest diagnostic state. Assembly installation or removal invalidates the previous diagnostic. The existing diagnostic switch records pass or failure against the current derived readiness.

The orange control on the drone moves it to the ready shelf only when the assembly is mission-ready and the current diagnostic passed. The same control follows the drone and returns it to the service bay. Relocation is an authored `0.8 s` eased movement with a small lift arc; the eleven installed instances remain attached through their sockets.

## Persistence

Safe House and lab saves now write schema version 4. The root format extends the existing part and socket records with:

- storage occupancy by stable location and unique part instance ID;
- scrap count and salvaged state;
- drone identity, stable location, and diagnostic state.

Schema versions 1–3 remain readable. Missing stable location fields migrate from legacy owner/socket information. Version 4 loading validates capacity, compatibility, and unique occupancy before applying storage state, then restores deterministic slot and drone-anchor transforms. Derived readiness is still recomputed from installed runtime parts.

## Player controls added

- `E` on the bench service control — enter the focused repair view;
- middle-mouse drag — orbit the drone;
- mouse wheel — zoom the service camera;
- left click / hold — drag a stored part, tighten the pointed screw, or operate the highlighted latch or twist lock;
- right click / hold — loosen the pointed screw;
- drag an inventory part out of the sidebar — promote the same runtime instance into a controlled 3D drag;
- guide the 3D part into a compatible socket — complete the insertion gesture and seat it deterministically;
- drag eligible damaged part to `SALVAGE` — consume it and produce scrap;
- `Escape` — cancel the active drag first, otherwise restore first-person mode;
- `EXIT SERVICE` — restore first-person mode;
- `E` on a stored part outside service mode — retrieve it through the normal pickup interaction;
- `E` while holding a part and aiming at a valid rack — store it;
- `E` twice while holding a damaged part at salvage — confirm salvage;
- `E` on the orange drone control — move between service bay and ready shelf when permitted;
- existing `1` save and `2` load include Milestone 4 state.

## Validation completed

Validated through Milestone 4.1 with Unity `6000.4.8f1`:

- `36/36` Edit Mode tests passed;
- `22/22` Play Mode tests passed;
- all 50 pre-Milestone-4.1 tests remained green;
- dedicated service tests cover authored screw travel, partial direction reversal, diagnostic invalidation, extraction gating, identity-preserving drag cancellation, and service action bindings;
- Play Mode coverage verifies the Safe House screw targets are grounded and clickable and that the replacement motor visibly transitions from its sidebar slot through guided 3D seating;
- deterministic service placement, incompatible-drop rejection, identity-preserving extraction, and one-time service salvage passed dedicated Edit Mode coverage;
- normal `E` input entered service mode, suspended locomotion and center-screen interaction, detached the camera, and restored its exact parent and local pose on exit;
- the authored rear-left workflow removed the blocking propeller, loosened and returned the faulty motor, seated and secured the stored replacement without freehand positioning, and reinstalled the same propeller instance;
- the Safe House created three storage locations, eleven authored part slots, two initially occupied serviceable-part slots, one ready-shelf anchor, and one persistent drone identity;
- a stored motor was retrieved through the normal first-person interaction path;
- a damaged installed motor was normalized to loose test state, picked up normally, and required two salvage interactions before producing one scrap token;
- an otherwise ready drone was rejected before diagnostic, accepted after a passing diagnostic, moved to the ready shelf, returned to service, and retained `11/11` assembly occupancy;
- schema-v4 round trips restored visible storage occupancy, salvage state, scrap, drone location, and diagnostic state without duplication;
- a clean Safe House boot produced zero runtime errors;
- the final inventory view is stored at `Assets/Screenshots/milestone-04-inventory-final.png`.
- the implemented service view is stored at `Assets/Screenshots/milestone-04-service-mode.png`.
- final Milestone 4.1 fastener placement is stored at `Assets/Screenshots/milestone-04-1-fastener-placement.png`;
- final Milestone 4.1 3D drag evidence is stored at `Assets/Screenshots/milestone-04-1-3d-part-drag.png`.

## Known limitations and tuning targets

- Drone relocation is a short authored whole-drone movement rather than a continuous player-carried interaction. Its duration, lift arc, and readability require human feedback.
- Storage, returns, salvage geometry, labels, and scrap tokens are functional low-poly greybox assets.
- Parts use fixed authored slot rotations; category-specific presentation poses can be tuned after the rack is assessed from a normal player route.
- The service interface uses functional IMGUI greybox styling. Sidebar width, row density, opacity, target highlight strength, and typography require a human readability pass.
- Orbit sensitivity, pitch limits, zoom range, initial framing, and camera transition duration are exposed tuning targets and require subjective feedback.
- 3D drag depth, sidebar capture threshold, socket capture radius, magnetic guidance strength, insertion travel, and seating detent timing require subjective mouse-feel approval.
- Fastener extraction travel, rotation count, screwdriver alignment speed, and tighten/loosen rate remain authored tuning parameters.
- Pointer operation preserves authored fastener, twist-lock, latch, prerequisite, and removal-blocker logic, but the complete repair sequence still needs a human comfort pass with normal mouse input.
- Scrap has no use until a later approved milestone.
- Depleted but serviceable batteries can enter returns but cannot be salvaged or charged.
- Human feedback is required for service-control approach clarity, pointer target reliability, text size, prompt clarity, and whether drag-to-salvage feels deliberate enough.
- The Safe House still emits a URP warning that punctual-light shadows are downscaled to fit the current `2048×2048` shadow atlas. This predates the inventory feature and does not produce runtime errors; render-pipeline settings were not changed.

Milestone 4.2 is now implemented after Milestone-4.1 approval. See `MILESTONE_04_2_IMPLEMENTATION.md`. Milestone 4.3 remains gated on human locker, fleet-roster, comparison, swapping, and larger-frame service-framing approval.
