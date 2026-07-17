# Milestone 5 Implementation — Daily Sorties and Abstract Missions

## Outcome

The Safe House now supports a complete abstract sortie loop: review one of three daily requests, stage and assign the physically ready drone, choose a deployment site, launch, continue working in the workshop while the timer and radio updates run, receive the same aircraft back with deterministic wear and charge depletion, and inspect its readable mission report.

Daily capacity is driven by prepared aircraft, battery state, installed capability, and strike-rack charges rather than a hidden action-point limit. Only one mission may be assigned or active at once, but another prepared drone can perform a later sortie before the player ends operations.

## Authored content

Immutable Resources assets define three initial requests:

- `Road Watch` — reconnaissance weighted toward observation, endurance, control, and reliability;
- `Counter-Battery Window` — precision strike against a confirmed stationary artillery position;
- `Broken Treeline` — armed search whose engagement is prohibited unless the drone achieves positive identification.

Two authored deployment sites trade faster workshop-adjacent response against remote-team handling time, wear, and future exposure contribution. These exposure values are recorded but Milestone 5 does not implement workshop risk.

## Runtime architecture

`MissionSystem` owns mission state transitions, eligibility, deterministic resolution, radio progress, consumable use, and return wear. `OperationalDaySystem` owns only the day seed, offered runtime mission identities, completed-sortie count, and voluntary day ending. Fleet ownership remains solely in `FleetSystem`.

Mission runtime follows:

`Available → Accepted → Assigned → Active → Returning → Resolved`

The assignment seed, active timer, result breakdown, assigned drone identity, site, radio progress, charge consumption, and return state are mutable runtime data. Derived drone statistics are recomputed from the current frame and installed parts.

## Physical capability and tactical map

The modular frame layout now has an optional shared strike-rack socket. A loose Field strike rack begins in serviceable storage with one persistent charge. Armed launches require the installed charged instance and consume its charge exactly once; the empty rack remains owned and installed.

The in-world tactical map opens through normal interaction and presents request briefing, archetype, value, duration, requirements, ready-aircraft statistics and component value, deployment-site modifiers, expected frame wear, and mission actions. It closes on launch so normal service, locker, storage, and market interaction remain available while the sortie progresses. A compact active-sortie overlay reports authored radio updates.

## Resolution and persistence

Resolution combines the assigned aircraft's current statistics, mission weights, deployment-site modifiers, readiness, and saved uncertainty roll. Armed Search treats positive identification as a hard gate and returns an observation-only outcome when the threshold is missed.

The same `DroneActor` returns with deterministic battery depletion, frame wear, component wear, and an invalidated diagnostic. Recovery enters the service bay when it is free; otherwise the mission remains explicitly `Returning` until a valid destination exists.

Safe House saves now use schema version 8. Active timing, saved seeds, report state, pending return, deployed fleet identity, day state, radio progress, consumable charges, expendable roles, fleet losses, and one-time rewards restore with the existing schema-6 economy. Versions 1–7 remain readable.

## Validation

- Edit Mode: 65/65 passed, including 11 Milestone-5 mission and persistence tests.
- Play Mode: 35/35 passed, including 5 Milestone-5 interaction tests.
- Normal `E` input opens the tactical map.
- Road Watch launches the ready Scout while locker/service interaction remains usable.
- Recovery waits when the service bay is occupied, then resolves with the same aircraft identity after the bay is cleared.
- Installing the physical strike rack permits one armed launch and changes its charge from one to zero.
- Schema-8 loading resumes an active mission with the same assigned aircraft and elapsed timer.
- Unity compiled with no errors. The existing URP punctual-light shadow-atlas warning remains unrelated to mission logic.

## Subjective follow-up and exclusions

Mission-board density, radio cadence, the current 28/34/42-second prototype timers, result-language clarity, and whether the aircraft/site trade-offs feel consequential require a human play pass.

Strike-rack reloading, multiple simultaneous missions, loss of aircraft not explicitly designated expendable,
and workshop risk remain excluded. Battlefield reconstruction was added by Milestone 5.1.

## Daily continuity amendment

Milestone 5.3 adds mission payouts, player-triggered next-day advancement, overnight battery turnaround,
market-cycle advancement, and explicitly designated expendable strike-airframe loss.
A fresh Safe House now starts with one repairable Scout and two complete one-way strike drones. Mission results
award the existing economy's funds and inventory salvage exactly once. The tactical terminal changes from
`END OPERATIONS` to `BEGIN NEXT DAY`, and schema 8 persists rewards and fleet losses. See
`MILESTONE_05_3.md` for the binding scope and remaining exclusions.
