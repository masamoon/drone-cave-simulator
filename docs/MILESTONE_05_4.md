# MILESTONE 05.4 — Visual Diagnosis and Component Tooltips

## Objective

Move workshop diagnosis from persistent text summaries onto the physical drone. In service mode, hovering a component should answer two questions immediately:

1. What is this part?
2. What state is it in?

This is a UI-readability milestone. It does not change condition values, readiness thresholds, repair procedures, mission wear, or ownership.

## Service-mode inspection

The existing service-mode pointer raycast remains authoritative. A small tooltip follows the cursor while it is over:

- an installed or seated part;
- a screw belonging to an occupied socket;
- an occupied socket;
- an empty required socket;
- the drone frame.

Screws and occupied sockets resolve to the same runtime part instance they service. The tooltip never creates a second component record.

Each component tooltip shows:

- authored display name;
- condition band and percentage when known;
- a short category/state line;
- battery charge when relevant;
- a compact colour-coded condition bar.

Condition language uses the existing thresholds: `FAILED`, `DAMAGED`, `WORN`, and `SERVICEABLE`. A depleted battery prioritizes `DEPLETED` while retaining condition detail. Empty sockets show `MISSING` and their required component category.

The tooltip is clamped to the screen, does not intercept pointer input, disappears during a part drag, and does not replace interaction prompts.

## Diagnostic disclosure

Installed component condition is shown as `UNDIAGNOSED` until workshop diagnosis has disclosed the drone's faults. Running the existing diagnostic switch marks fault details as disclosed and persists through the existing `DroneRuntimeData` field.

Loose owned parts retain their known condition because their specifications are already available in inventory. Mission return clears exact fault disclosure so newly accumulated wear must be diagnosed before exact component condition is shown.

Missing components remain visually disclosed because the absence is directly observable.

## Reduced text dependency

In the Safe House, replace the large persistent drone statistics panel with a compact readiness strip containing only:

- service-bay identity;
- `UNDIAGNOSED`, `READY`, or `MAINTENANCE` state;
- a short prompt to inspect the drone in service mode.

Detailed derived statistics remain available where they inform deployment and fleet comparisons. Laboratory scenes keep their development-oriented detailed status panels.

After a service-mode diagnostic, the status message directs the player to hover highlighted components rather than listing every fault in a large text block.

## Architecture

- Add a pure `ServiceInspectionPresenter` that converts existing part, socket, and frame runtime state into a testable display snapshot.
- Add a narrow `DroneFrameInspectionTarget` for service-mode chassis hover detection.
- Keep rendering in `DroneServiceModeController` using the existing prototype IMGUI layer.
- Keep `InstallablePart`, `PartSocket`, `DroneActor`, and `DroneRuntimeData` authoritative.
- Do not add persistence fields or increase the schema version.

## Validation

Edit Mode tests cover:

- condition-band boundaries;
- undisclosed versus disclosed installed faults;
- loose-part visibility;
- depleted-battery priority;
- empty-socket and frame snapshots.

Play Mode tests cover:

- Safe House frame inspection target creation;
- diagnostic disclosure;
- screw-to-part and socket-to-part inspection resolution;
- compact Safe House status presentation;
- service-mode entry, dragging, fastening, and exit regressions.

Human approval is required for tooltip size, cursor offset, colour contrast, percentage usefulness, hover stability across small screws, and whether the compact readiness strip leaves enough context.

## Exclusions

- new diagnostic minigames or tools;
- x-ray views or hidden internal faults;
- animated damage decals;
- final UI Toolkit conversion;
- changes to market disclosure before purchase;
- workshop risk or discovery logic;
- changes to mission resolution, condition thresholds, or save schema.
