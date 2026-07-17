# Milestone 5.1 Implementation — Procedural After-Action Reconstructions

## Outcome

Resolved missions now expose an optional in-engine after-action reconstruction. A deterministic 2D topography record generated from the mission's saved resolution seed drives both the tactical-map preview and a temporary low-poly 3D battlefield reconstruction.

The reconstruction does not replace mission resolution and does not claim to be omniscient footage. Recon shows an observation pass. Confirmed successful armed results may show a restrained equipment-impact confirmation. Aborted or unidentified results show a hold-and-egress sequence without inventing a target, figures, or strike.

## Runtime architecture

`MissionReplayDefinition` contains immutable grid, world-size, elevation, contour, vegetation, and duration tuning. Each `MissionDefinition` selects Road Valley, Gun Position, or Broken Treeline topography.

`MissionTopographyGenerator` produces a `MissionTopographyMap` containing normalized elevation samples, connected road cells, vegetation cells, route endpoints, and an objective/search anchor. `MissionTopographyPresentation` converts that same record into the 2D preview texture and terraced 3D mesh.

`MissionReplayPlan` decides whether the result permits an engagement and provides explicit Approach, Observe, Engage/Hold, Egress, and Complete phases. `MissionReplayDirector` builds the temporary terrain, road, vegetation, mission target, drone, light, and camera, then destroys all derived objects on exit.

No generated mesh, texture, prop, camera phase, or replay-active flag is persisted. Schema 7 remains current because the existing mission definition ID, result seed, outcome, identification, and ordnance state fully regenerate the presentation.

## Safe House integration

The tactical map displays the selected request's generated contour/feature preview. Resolved reports replace the unused Accept action with `VIEW RECONSTRUCTION`.

Replay entry disables the workshop camera, player movement, interaction system, service UI, market UI, tactical UI, status panels, and debug overlays while recording each component's previous enabled state. Exit restores those states, cursor settings, and camera exactly. A visible button permits early return; reaching the twelve-second endpoint holds the final frame until the player leaves.

## Result integrity

An engagement effect requires an armed archetype, recorded ordnance consumption, a positive identification, and Limited Success or better. Recon can never show a strike. Observation Only and Aborted outcomes cannot show one.

An unidentified Armed Search reconstruction substitutes an unconfirmed search-area marker for human silhouettes. The generated map's amber objective mark represents the reported search area, not confirmed hidden knowledge.

## Validation

- Edit Mode: 74/74 passed, including 9 new deterministic generation, mesh, preview, engagement-gating, and camera-path tests.
- Play Mode: 39/39 passed, including 4 new Safe House preview, camera/controller restoration, unidentified-hold, and confirmed-impact reconstruction tests.
- Unity compiled with no errors.
- A visual Play Mode capture confirmed the generated terrain, road, vegetation, artillery representation, drone, reconstruction HUD, and isolated reconstruction camera render in engine.
- Workshop HUDs no longer render over the reconstruction, and all suspended behaviours restore after exit.
- The existing URP punctual-light shadow-atlas warning remains unrelated to the replay system.

## Subjective follow-up

The twelve-second duration, four phase boundaries, camera height/radius, 33×33 grid, nine contour bands, 52-metre reconstruction, vegetation density, target abstraction, and impact size require human feedback.

Current props remain functional low-poly primitives. There is no audio pass, replay scrubbing, alternate camera selection, terrain biome library, weather reconstruction, projectile simulation, graphic damage, or replay export.
