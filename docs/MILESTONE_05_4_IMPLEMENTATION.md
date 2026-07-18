# Milestone 05.4 Implementation — Visual Diagnosis and Component Tooltips

## Outcome

Diagnosis now lives on the physical drone in service mode. Hovering an installed component, its socket, or one of its screws opens a compact cursor-adjacent card showing the authored part name, diagnostic status, condition percentage, component state, and a colour-coded condition bar. Hovering the chassis shows frame condition, and empty sockets report the missing component category.

## Disclosure behavior

- Installed parts and the frame show `UNDIAGNOSED` until the existing diagnostic is run.
- Running the diagnostic sets the existing persisted `diagnosticFaultsDisclosed` runtime field.
- Loose owned parts retain known condition because inventory already exposes their specification.
- Battery cards prioritize `DEPLETED` while still showing physical condition.
- Mission return clears exact fault disclosure so newly accumulated wear must be diagnosed.
- Missing sockets remain visible because absence is directly observable.

No new condition, ownership, or persistence model was added, and the save schema remains version 8.

## Reduced panel dependency

The Safe House's large persistent drone statistics panel is now a compact readiness strip showing only `UNDIAGNOSED`, `READY`, or `MAINTENANCE` and directing the player into service view. Laboratory scenes retain their detailed development panels.

The fleet roster is suppressed while service mode is active. Deployment and normal workshop views retain the roster because comparative fleet statistics remain decision-relevant there.

After diagnosis, the service status directs the player to hover components rather than printing a full fault list.

## Architecture

- `ServiceInspectionPresenter` converts existing runtime state into testable display snapshots.
- `DroneFrameInspectionTarget` adds narrow service-mode chassis hover detection without enabling first-person interaction.
- `DroneServiceModeController` reuses its existing deterministic raycast, resolves screws and occupied sockets to their mounted part, and renders the prototype IMGUI tooltip.
- `DroneAssemblyState`, `InstallablePart`, `PartSocket`, `DroneActor`, and `DroneRuntimeData` remain authoritative.

## Validation

- Focused Edit Mode: 11/11 passed.
- Focused Play Mode: 3/3 passed.
- Full Edit Mode regression: 93/93 passed.
- Full Play Mode regression: 53/53 passed.
- Manual Play Mode inspection confirmed the rear-left motor card resolves from its physical body and reports `FAILED · CONDITION 18%` after diagnosis.
- Manual inspection confirmed the tooltip remains near the cursor, does not intercept service input, and the fleet roster is absent during service mode.

Reference capture:

- `Assets/Screenshots/milestone-05-4-component-tooltip-1.png`

## Known limitations and tuning

- The prototype tooltip remains IMGUI and mouse-oriented; final controller navigation and UI Toolkit styling are outside this milestone.
- Condition uses both a band and percentage. Human testing should determine whether the exact percentage adds useful repair information or unnecessary precision.
- Tooltip width, cursor offset, opacity, colour contrast, and condition-bar thickness require subjective approval.
- Small screw hover remains intentionally precise because screws are actionable targets; the resolved card identifies the parent component to reduce ambiguity.
