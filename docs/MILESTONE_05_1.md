# MILESTONE 05.1 — Procedural Final-Approach Live Feeds

> July 2026 amendment: the post-mission reconstruction has moved into the active sortie as an optional degraded
> final-approach feed. Milestone 05.4 still supersedes request-selected topography and adapts presentation to the
> persistent battlefield and player-authored sortie plan.

## Objective

Turn the final approach of an active abstract sortie into a short degraded first-person feed generated from the
same deterministic 2D topography used by the tactical map.

The feed is not direct flight control and is not an authoritative omniscient camera. Before resolution it may show
only route geometry, visible intelligence, and unconfirmed search markers. The deterministic result may then
authorize identification, a confirmed engagement, hold, signal loss, or egress. It must never reveal hidden truth
or a successful strike that the mission result did not establish.

## Player loop

`Continue workshop work → final approach becomes available → optionally view degraded feed → receive report`

The feed is optional and begins only through the tactical map after the active sortie reaches 60% progress. The
player may leave it at any time. Ignoring or leaving the feed does not change deterministic resolution. The readable
result breakdown remains authoritative, and resolved reports expose no replay action.

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

## Live-feed director

Add `MissionReplayDirector` with explicit phases:

`Approach → Observe → Engage or Hold → Egress → Complete`

The director temporarily disables the workshop camera and movement, creates a bounded low-poly presentation away
from the Safe House, and drives an authored first-person camera path around the generated terrain. It restores the
previous camera, controller, cursor state, and workshop view on exit or component disable.

Recon shows route coverage and an observation pass. Precision Strike may show a stationary artillery position and a restrained impact confirmation. Armed Search may show abstract distant silhouettes only after positive identification. There are no character deaths, gore, ragdolls, detailed projectile ballistics, or player-controlled camera flight.

Engagement presentation is allowed only when all are true:

- the mission archetype supports engagement;
- positive identification is recorded where required;
- the result is Limited Success, Success, or Exceptional Success;
- ordnance was consumed.

Observation Only and Aborted results show a hold/egress feed without a strike.

## Tactical map integration

The tactical map displays the generated topographic preview. During final approach it exposes
`OPEN DEGRADED LIVE FEED`. Resolved requests expose only report acknowledgement.

The overlay shows sortie type, live-feed phase, link degradation, and whether the terminal result is still pending.
Escape returns to the workshop at any time. Otherwise, the feed returns automatically after a two-second terminal
hold: from `Signal Lost` for kamikaze sorties, or from the completed endpoint for other sorties.

## Persistence

The current schema remains unchanged. Feed availability is derived from saved active progress and transmitter state.
The generated terrain, textures, current camera phase, and feed-active flag remain transient. Loading never resumes
a feed; it returns to the workshop and the active sortie continues from its authoritative saved state.

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
- an active mission below 60% cannot enter the feed;
- an active mission in final approach can enter the feed;
- feed entry disables the workshop camera/controller;
- feed exit restores the exact prior workshop state;
- an observation-only result never produces engagement effects.

All Milestone-5 and earlier tests remain green.

## Exclusions

- direct drone piloting or camera steering;
- interactive replay cameras;
- full battlefield scenes;
- detailed ballistics or damage simulation;
- graphic casualties;
- feed recording, replay, editing, or export;
- saved generated meshes or textures;
- procedural campaign geography;
- workshop risk changes.

Human approval is required for camera motion, terrain readability, degradation strength, prop density, impact
restraint, feed duration, and whether the view feels tense and informative rather than triumphalist.
