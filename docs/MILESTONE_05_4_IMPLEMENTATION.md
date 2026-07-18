# Milestone 05.4 Implementation — Persistent Player-Planned Sorties

## Delivered

- Added `BattlefieldSystem` with deterministic 4 km × 4 km generation, a fixed workshop marker, separated hidden truth and visible intelligence, seven seeded contacts, a guaranteed early cluster of two infantry and one artillery contact, stationary durability, deterministic infantry movement, stale/disproven intelligence, and contact-owned rewards.
- Reworked `MissionSystem` around a persistent `SortieDraftData`, immutable Recon/Kamikaze/Grenade Drop profiles, ready-shelf validation, player-authored routes and targets, one active sortie, progressive discovery, distance-aware resolution, resource commitment, fleet transitions, and resolved history.
- Replaced request cards with a persistent tactical map showing the workshop, topography, route and automatic return, staged-aircraft reconnaissance reach, sensor corridor, active aircraft progress, discovered contacts and age, damage/destruction, range feedback, reports, and reconstruction controls.
- Adapted reconstruction to saved routes, aimed positions, actual outcomes, and sortie-owned discoveries. A no-contact reconstruction shows the searched position without reading relocated infantry truth.
- Added separate `KamikazeWarhead` and `GrenadeDrop` part capabilities while retaining the strike-rack category. Expendable starter aircraft receive an integrated warhead definition; the loose reusable rack remains a grenade-drop rack.
- Advanced mission saves to schema 10 and included the battlefield, draft, active progress, contact state and damage, history, day state, and committed fleet/part resources. Mission-enabled loads reject schema 1–9 before restoration begins.
- Removed the three legacy request assets, legacy mission definition, deployment-site assets, and deployment-site definition from the active project.
- Updated the settled game specification and marked Milestones 05–05.3 as historical where 05.4 supersedes their request flow.

## Automated coverage

Edit Mode coverage includes deterministic generation and spawn constraints, hidden-state filtering, automatic return and range validation, closest-route progressive discovery, day movement and stale intelligence, no-contact resolution, stationary and multi-hit durability, known-only targeting, distinct loadout roles, expendable versus reusable transitions, exactly-once rewards, draft/active/contact round trips, legacy-schema rejection, and reconstruction information boundaries.

Play Mode coverage includes safe-house system/profile construction, normal terminal interaction, staged-scout recon planning and completion, schema-10 integration, persistent-map preview, no-contact reconstruction, kamikaze signal loss, and representative visual reconstruction checks.

## Validation performed

- Runtime assembly compiled successfully with Unity 6 Roslyn.
- Edit Mode test assembly compiled successfully against the resulting runtime reference assembly.
- Play Mode test assembly compiled successfully against the resulting runtime reference assembly.
- `git diff --check` reported no patch whitespace errors; line-ending warnings belong to the existing mixed-line-ending worktree.

The Unity editor was already open in a Test Runner session. Its Play Mode runner ended with a Unity Test Framework cleanup `NullReferenceException` in `PlayModeRunTask`, so this implementation does not claim a completed in-editor Play Mode pass. No editor process was closed or interrupted.

## Human tuning and known limitations

- Map scale, waypoint dragging, sensor corridor width, contact density, payout pacing, sortie duration, icon readability, and the fairness of stale infantry require hands-on tuning.
- Location changes remain unimplemented; all routes start at the saved workshop marker.
- Recon reconstruction renders the contacts recorded by that sortie and never selects a hidden or relocated contact from battlefield truth.
- Supported Game View resolutions still require visual inspection for report and map layout. Report text uses wrapping and clipping so it remains contained by its panel.
