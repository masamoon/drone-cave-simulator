# MILESTONE 12 — Battlefield Presentation

## 1. Goal

Make the battlefield view readable, atmospheric, and specific to *Under Static* while preserving its abstract,
intelligence-limited nature:

`Read terrain → understand pressure and intelligence → plan a route or target → follow progress → understand the result`

This milestone improves both the tactical map and the degraded final-approach feed. Neither becomes a directly
controlled flight or combat view.

## 2. Tactical-map scope

- Apply the Milestone 11 palette, typography, iconography, texture scale, and state language to the tactical
  terminal.
- Improve terrain hierarchy through authored relief, contour, road, vegetation, settlement, and landmark treatment
  derived from the existing deterministic battlefield data.
- Make the workshop, frontline, sectors, friendly pressure, unknown activity, known contact type, intelligence age,
  destroyed state, selected target, route, sensor corridor, range limit, and active aircraft progress visually
  distinct.
- Keep `Current`, `Stale`, `Disproven`, `Destroyed`, and unknown information distinguishable without color alone.
- Separate planning controls, aircraft capability, forecast, active progress, radio updates, and resolved report
  into a clear information hierarchy.
- Use motion sparingly for active pressure, route progress, signal quality, and new intelligence; static historical
  information must remain stable enough to compare.
- Preserve one consistent spatial model between planning, live progress, reports, and the saved battlefield.

## 3. Final-approach-feed scope

- Upgrade terrain materials, silhouettes, vegetation, roads, structures, haze, sky, lighting, and distance cues
  using deterministic low-poly presentation assets.
- Add restrained signal degradation, compression, exposure, and telemetry treatment that supports mood without
  obscuring mission-critical shapes.
- Improve camera motion and framing so speed, altitude, approach direction, observation, release authorization,
  signal loss, and egress read clearly without accepting flight input.
- Show only route geometry, visible intelligence, authorized search markers, and outcomes already established by
  the deterministic mission authority.
- Ensure the feed never reveals hidden contacts, confirms an unresolved strike, or replaces the report as the
  authoritative explanation.

## 4. Readability acceptance

In representative Play Mode scenarios, the player can identify within a few seconds:

- where the workshop and frontline are;
- which sector is under urgent pressure;
- whether a contact is unknown, current, stale, disproven, or destroyed;
- whether the staged aircraft can reach the planned route or target;
- what is selected, what will be committed, and what consequence is forecast;
- whether the active operation is observing, engaging, returning, awaiting confirmation, or resolved.

The final feed must retain readable terrain and target silhouettes under its strongest supported degradation.

## 5. Architecture and validation

- `BattlefieldSystem`, `FrontlineSystem`, and `MissionSystem` remain authoritative. Map and feed presentation consume
  their visible records and never mutate or infer hidden truth.
- Generated presentation remains deterministic from saved data and does not require persisting derived meshes,
  textures, props, or effects.
- Add automated tests for visibility filtering, presentation-state mapping, deterministic generation, and map/feed
  agreement where practical.
- Capture representative Game View states for route planning, stale intelligence, urgent frontline pressure,
  active recon, active strike, signal loss, destroyed contact, and resolved report.
- Perform final visual QA for framing, clipping, occlusion, scale, contrast, material consistency, label collisions,
  interaction readability, temporal continuity, and consistency between map and feed.

## 6. Excluded

- Direct FPV control, detailed ballistics, infantry simulation, an explorable outdoor world, full battlefield
  rendering, graphic casualties, omniscient cameras, and new mission-resolution rules.
- New battlefield content whose only purpose is visual variety.
- Workshop-wide visual refinement beyond integration with the Milestone 11 style language.

## 7. Definition of done

The milestone is complete when the tactical map communicates planning and battlefield state at a glance, the
final-approach feed is visually coherent and information-safe, both share the established identity, generated
presentation remains deterministic, automated tests pass, and every representative state passes Game View QA.
