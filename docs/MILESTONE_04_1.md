# MILESTONE 04.1 — Service Interaction Polish

## 1. Objective

Polish the approved Safe House service mode without expanding into multiple drones, fleet storage, missions, or the market. Fasteners must sit at authored motor-base positions, support reversible partial progress, and respond to explicit tighten/loosen input. Inventory parts must become their existing 3D runtime objects when dragged out of the sidebar and use the authored guidance/insertion sequence before seating.

## 2. Service controls

Service mode uses Input System actions rather than direct device-key checks:

- left mouse — begin inventory drag, tighten the pointed fastener, or operate the existing context action;
- right mouse — loosen the pointed fastener;
- middle mouse — orbit the service camera;
- mouse wheel — zoom;
- Escape — cancel an active drag first, otherwise exit service mode;
- the existing save and load actions remain available.

Controls are bound in the project input-actions asset and remain remappable. Ordinary first-person controls remain suspended while service mode is active.

## 3. Authored reversible fasteners

Each visible screw is represented by a `FastenerTarget` attached to its socket. The target contains an exact fastener index, drive pose, local thread axis, threaded visual pose, extraction travel, and visible turns. It is a controlled representation, never a free rigid body.

Rules:

- progress is continuous from `0` fully loosened to `1` fully tightened;
- the player may change direction at any intermediate progress;
- tightening and loosening operate the specifically pointed screw;
- the screwdriver travels to the current screw-head pose and spins around its drive axis;
- all screws at `1` install the component;
- all screws at `0` return the component to seated/extractable state;
- any mixed or partial set is unsecured and cannot be mission-ready;
- the first loosening movement invalidates the drone diagnostic;
- retightening reinstalls the component but does not restore the invalidated diagnostic.

Existing fastener arrays remain readable for older tests and saves. Socket fastener progress continues to persist in schema version 4.

## 4. Three-dimensional inventory drag

Pressing an inventory row initially selects its runtime instance without changing ownership. When the pointer crosses from the sidebar into the service view:

1. the exact stored instance leaves its authored slot;
2. it transitions `Loose → Held` and uses controlled physics;
3. the 2D drag card disappears;
4. the 3D part follows the pointer on a camera-facing service plane;
5. compatible empty sockets highlight;
6. entering an authored capture region starts normal guidance;
7. continued pointer motion along the insertion direction drives `Guided → Seated`;
8. the existing fastener, latch, or twist-lock procedure completes installation.

Releasing before seating, pressing Escape, leaving service mode, saving, loading, disabling the controller, or losing required references cancels the drag and returns the same instance to its exact original storage location and slot. If that slot cannot be restored, the part returns to the first valid slot in its original location, otherwise to stable workshop-loose ownership.

Dragging an eligible damaged part onto the service salvage target remains the deliberate destructive gesture and produces scrap exactly once.

## 5. Persistence and readiness

- No save-schema bump is required.
- Fastener progress remains part of `SocketRuntimeState`.
- Active drag state and active tool direction are transient and are cancelled before save/load.
- Readiness counts only fully installed or tested parts.
- A partially loosened component remains attached visually but is not mission-ready.
- Existing schema versions 1–4 remain readable.

## 6. Automated validation

### Edit Mode

- pointed targets map to the correct fastener index;
- visual position follows the authored thread axis at progress `0`, `0.5`, and `1`;
- a partially loosened fastener can immediately tighten and vice versa;
- mixed fastener progress cannot produce an installed or ready component;
- loosening invalidates the current diagnostic;
- full retightening reinstalls without restoring that diagnostic;
- fully loosened fasteners permit extraction;
- 3D drag start releases one exact storage instance;
- cancellation restores identity and deterministic occupancy;
- incompatible or incomplete drops do not mutate ownership;
- guided drag requires insertion before seating;
- save/load preserves partial progress and never restores a transient drag.

### Play Mode

- Safe House fasteners appear at motor-base positions below the propeller;
- left and right mouse reverse one pointed screw while middle mouse orbits;
- the replacement motor visibly leaves the sidebar as a 3D part;
- the player guides and seats it without freehand precision wrestling;
- cancellation returns it to the rack without duplication;
- the full faulty-motor replacement workflow still completes;
- all existing 30 Edit Mode and 20 Play Mode tests remain green.

## 7. Manual acceptance

- Screw placement no longer reads as floating near the rotor.
- Reversing an accidental partial loosen is immediate and understandable.
- The screwdriver aligns to the selected screw and visibly follows its moving head.
- The part transition from sidebar to 3D is readable and tactile.
- Cursor depth, capture strength, insertion resistance, and detent timing feel controlled rather than automatic.
- Salvage remains deliberate.
- Camera restoration, save/load, and repeated repair cycles remain stable.

## 8. Exclusions

- multiple drones, frames, fleet storage, compatibility standards, and locker UI;
- market, money, buying, selling, or scrap spending;
- missions, deployment, risk, discovery, and operational shifts;
- charging, crafting, frame repair, permanent loss, visible hands, free screws, and final art.

## 9. Definition of done

Milestone 4.1 is complete when the new automated coverage and all 50 regression tests pass, the Safe House workflow is objectively validated in Play Mode, visual evidence is captured, and subjective screw/drag feel is handed to the user for approval. Milestone 4.2 must not begin before that approval.
