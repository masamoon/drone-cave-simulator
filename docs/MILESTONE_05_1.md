# MILESTONE 05.1 — Procedural After-Action Reconstructions

## Objective

Turn a resolved abstract sortie into a short in-engine visual reconstruction generated from the same deterministic 2D topography used by the tactical map.

The feature is presented as an after-action reconstruction rather than an authoritative kill camera. It may visualize approach, observation, identification, a confirmed engagement, and egress, but it must never reveal a target or successful strike that the mission result did not establish.

## Player loop

`Resolve mission → inspect report and topographic map → view reconstruction → return to workshop`

The reconstruction is optional. It does not interrupt an active mission and it does not replace the readable result breakdown.

## Deterministic topography

Add an immutable `MissionReplayDefinition` containing the map grid resolution, world size, elevation scale, contour count, vegetation density, and replay duration.

Each `MissionDefinition` selects one authored topography profile:

- Road Valley for Road Watch;
- Gun Position for Counter-Battery Window;
- Broken Treeline for the armed-search request.

`MissionTopographyGenerator` combines the mission profile and saved resolution seed to produce a 2D `MissionTopographyMap` containing:

- normalized elevation samples;
- a connected route/road mask;
- vegetation and clearing masks;
- route start and end anchors;
- one target or observation anchor.

The tactical-map preview and 3D terrain mesh must consume the same map record. Identical inputs must produce identical samples and anchors. The generated mesh, texture, and prop objects are derived presentation and are never saved.

## Reconstruction director

Add `MissionReplayDirector` with explicit phases:

`Approach → Observe → Engage or Hold → Egress → Complete`

The director temporarily disables the workshop camera and movement, creates a bounded low-poly reconstruction away from the Safe House, and drives an authored camera path around the generated terrain. It restores the previous camera, controller, cursor state, and workshop view on exit or component disable.

Recon shows route coverage and an observation pass. Precision Strike may show a stationary artillery position and a restrained impact confirmation. Armed Search may show abstract distant silhouettes only after positive identification. There are no character deaths, gore, ragdolls, detailed projectile ballistics, or player-controlled camera flight.

Engagement presentation is allowed only when all are true:

- the mission archetype supports engagement;
- positive identification is recorded where required;
- the result is Limited Success, Success, or Exceptional Success;
- ordnance was consumed.

Observation Only and Aborted results show a hold/egress reconstruction without a strike.

## Tactical map integration

The selected request displays a small generated topographic preview. A resolved request exposes `VIEW RECONSTRUCTION`. Launch and active-mission flows remain unchanged.

The replay overlay shows mission name, reconstruction phase, result classification, and a visible `RETURN TO WORKSHOP` action. It automatically reaches a complete hold but waits for the player to leave, avoiding an abrupt camera cut.

## Persistence

Schema version 8 remains current after the daily-continuity amendment. The saved mission definition ID, resolution seed, archetype, outcome, identification state, and ordnance-consumed flag remain sufficient to regenerate the reconstruction.

Loading never resumes a transient replay. Save, scene unload, disable, or replay cancellation restores the workshop safely.

## Validation

Edit Mode tests cover:

- identical seed/profile generation;
- distinct seeds producing distinct terrain;
- normalized and finite elevation samples;
- connected route coverage;
- target anchors within bounds;
- tactical preview and 3D mesh consuming matching samples;
- engagement gating for recon, aborted, and unidentified results;
- deterministic camera phase evaluation.

Play Mode tests cover:

- the Safe House creates the replay director;
- the tactical map exposes a preview for a selected request;
- a resolved mission can enter the reconstruction;
- replay entry disables the workshop camera/controller;
- replay exit restores the exact prior workshop state;
- an observation-only result never produces engagement effects.

All Milestone-5 and earlier tests remain green.

## Exclusions

- direct drone piloting;
- interactive replay cameras;
- full battlefield scenes;
- detailed ballistics or damage simulation;
- graphic casualties;
- replay editing or export;
- saved generated meshes or textures;
- procedural campaign geography;
- workshop risk changes.

Human approval is required for camera motion, terrain readability, prop density, impact restraint, replay duration, and whether the reconstruction feels informative rather than triumphalist.
