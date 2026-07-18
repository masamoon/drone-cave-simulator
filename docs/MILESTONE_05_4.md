# MILESTONE 05.4 — Persistent Player-Planned Sorties

## Objective

Replace authored daily requests with one persistent battlefield on which the staged drone determines the available operation:

`Stage drone → choose sortie type → plan route or target → launch → observe progress → act on persistent intelligence`

The operation remains abstract. There is no direct FPV piloting, infantry combat simulation, detailed ballistics, or location-changing mechanic.

## Persistent battlefield

- Generate one deterministic 4 km × 4 km topographic map from a saved run seed.
- Place the workshop at normalized map position `(0.15, 0.15)` and originate every sortie there.
- Seed one distant enemy base, two stationary artillery contacts, and four mobile infantry contacts.
- Guarantee that at least two infantry contacts and one artillery contact begin inside the fully charged starting Scout's reconnaissance envelope.
- Keep hidden truth separate from player-visible snapshots used by the tactical map, report, and reconstruction.
- Track contact intelligence as `Hidden`, `Current`, `Stale`, `Disproven`, or `Destroyed`.
- Artillery and the base remain stationary. Infantry moves deterministically by 0.15–0.45 km at the start of a new day.
- Preserve stale infantry markers as selectable last-known positions. A strike there resolves `NoContact`, commits its resource, and disproves that observation without revealing the new position.

## Sortie profiles and planning

The immutable profiles are Recon, Kamikaze Strike, and Grenade Drop. The ready-shelf aircraft is the assigned aircraft; changing it revalidates the saved draft without deleting the plan.

Recon requires a tested, reusable aircraft with observation capability. The player adds, drags, removes, undoes, or clears up to 12 waypoints. The workshop is the fixed start and an automatic return leg counts toward the `6 × Endurance` kilometre range. Its sensor corridor has half-width `0.10 + 0.20 × Observation` kilometres. Contacts are revealed deterministically and progressively at their closest route positions.

Kamikaze Strike requires an expendable aircraft and charged warhead. Travel is one-way and the whole aircraft is committed. Grenade Drop requires a reusable aircraft and charged drop rack; the route includes return travel and consumes one charge. Only current or stale visible contacts may be selected.

## Resolution and continuity

- Infantry and artillery have one strength; the base has three.
- Effective grenade drops deal one damage. Effective kamikaze strikes deal two.
- Grenade Drop favours infantry and is penalized against the base. Kamikaze favours artillery and the base and is slightly penalized against infantry.
- Distance affects eligibility, duration, battery, wear, and resolution.
- Destroyed contacts remain crossed out and cannot be targeted.
- Recon identification rewards are 30 funds for infantry, 70 for artillery, and 120 for the base; infantry reacquisition grants 15. Recon grants no salvage.
- Effective destruction grants 100 funds for infantry; 180 funds and three salvage for artillery. Base damage grants 100 funds per point plus 250 funds and five salvage on destruction.
- No-contact results, misses, and aborts grant no strike reward.
- One sortie may be active at a time while normal workshop interaction continues.

## Presentation and reconstruction

The terminal presents one large tactical map with sortie controls, staged-aircraft statistics, route/range feedback, a live staged-aircraft reconnaissance reach envelope, a sortie log, active progress, launch controls, and readable reports. Discovered contacts, intelligence age, destroyed state, route, sensor corridor, workshop position, and active aircraft progress all use the same map.

After-action reconstruction consumes only player-visible battlefield state plus the saved plan and actual result. Recon shows only contacts revealed by that sortie. A stale infantry no-contact result reconstructs the empty searched position and never the infantry's new hidden position.

## Persistence

Save schema 10 contains the battlefield seed, truth and intelligence state, damage, draft, active sortie progress and committed resources, discoveries, resolved history, and day state. Terrain is regenerated from the seed. Schemas 1–9 are rejected with an incompatibility message before runtime state is mutated.

## Acceptance

Automated coverage must include deterministic generation, hidden-state filtering, route/range and coordinate logic, progressive corridor discovery, intelligence movement and reacquisition, known-only targeting, stationary and multi-hit durability, fleet/resource transitions, exactly-once rewards, schema-10 round trips and legacy rejection, and reconstruction information boundaries.

Play Mode validation must exercise route editing, live recon progress, same-day and stale infantry strikes, reacquisition, both strike profiles, multi-hit base damage, save/load at each operation phase, and report/reconstruction readability. Map scale, route feel, sensor width, density, payout pacing, duration, icons, and stale-intelligence fairness require human tuning.
