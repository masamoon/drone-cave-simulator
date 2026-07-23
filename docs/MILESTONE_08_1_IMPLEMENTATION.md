# Milestone 08.1 Implementation Handoff

Implemented 2026-07-23 against `MILESTONE_08_1.md`.

## Delivered

- Replaced the nine named runtime sectors with an 11×9 odd-row-offset hex grid.
- Moved frontline advancement from the real-time pulse to the operational day transition.
- Made every active mobile unit advance by one adjacent hex per day; stationary bases do not move.
- Added broad multi-hex activity detections that conceal exact coordinates and unit type.
- Made recon reveal exact hex, type, pressure, and the next-day destination.
- Preserved a recon-confirmed destination after that move, while consuming the now-expired movement forecast.
- Required exact recon confirmation before a strike target can be selected or resolved.
- Rebuilt the tactical terminal and 512×512 physical wall map as information-safe wargame boards with distinct
  infantry, tank, artillery, base, unknown-activity, control, defense, and intent representations.
- Separated the detailed terminal's terrain-only 128×128 background from its screen-resolution hex, counter,
  uncertainty, and arrow overlay so no stretched tactical symbols are duplicated or misaligned.
- Centered counters in their hexes and added outlined arrowheads for recon-confirmed next-day movement on both
  map surfaces.
- Raised the current save schema to 15 and retained schema-14 migration through the authored node-to-hex mapping.
- Replaced the top-level mission-type buttons and planner sidebar with direct hex tasking: select the ready
  aircraft, left-click its staging hex, then right-click any reachable hex to launch one of the loadout's valid
  mission types.
- Added persistent draft/plan staging data, per-aircraft per-hex eligibility, and a dark reachability mask.
- Reduced the sidebar to active-aircraft cards and left its data contract ready for more than one active aircraft.
- Converted final-approach playback into an automatically started RenderTexture inset that leaves the workshop
  camera, terminal focus, and interaction systems intact.

## Primary implementation files

- `Assets/UnderStatic/Runtime/Missions/FrontlineHexGrid.cs`
- `Assets/UnderStatic/Runtime/Missions/FrontlineRuntimeData.cs`
- `Assets/UnderStatic/Runtime/Missions/FrontlineScenarioDefinition.cs`
- `Assets/UnderStatic/Runtime/Missions/FrontlineSystem.cs`
- `Assets/UnderStatic/Runtime/Missions/MissionSystem.cs`
- `Assets/UnderStatic/Runtime/Missions/OperationalDaySystem.cs`
- `Assets/UnderStatic/Runtime/Persistence/SaveSystem.cs`
- `Assets/UnderStatic/Runtime/UI/TacticalMapPresentation.cs`
- `Assets/UnderStatic/Runtime/UI/TacticalMapTerminal.cs`

## Validation

- Unity compilation: no C# errors.
- Edit Mode: 201 passed, 0 failed.
- Play Mode: 82 passed, 0 failed, 13 intentionally ignored legacy-pivot tests.
- Game View states inspected:
  - unknown multi-hex detections;
  - recon-identified counters and movement intent;
  - post-day movement with the forecast consumed;
  - 512×512 physical wall-map framing.
- Visual checks passed for framing, clipping, occlusion, cell and counter scale, map orientation, control-state
  contrast, interaction-target readability, and continuity across the day transition.

The Play Mode run continues to emit the existing URP shadow-atlas resolution warnings from the Safe House's
punctual lights. No new runtime errors were present.

## Human play-testing still required

The implementation is objectively functional, but these values need subjective campaign testing:

- 11×9 board density;
- two-hex detection radius;
- six authored enemy activities and their spawn days;
- one-hex-per-day movement pace;
- eight-day evacuation objective;
- counter and uncertainty contrast at the player's normal viewing distance.

Milestone 9 remains the next implementation milestone. Unmanned ground vehicles remain reserved for the future
land-drone milestone rather than being folded into this battlefield conversion.
