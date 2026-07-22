# Milestone 07 — Frontline Salvage Loop

## Goal

Prove a 20–30 minute workshop-to-frontline loop:

`Enemy activity → salvage → repair/configure → recon or blind strike → frontline consequence → profit → repeat`

This milestone supersedes workshop exposure only in the experimental Safe House loop. The existing exposure and
field-operation implementations remain dormant until this loop receives human approval.

## Binding scope

- Nine connected battlefield sectors with a derived visible frontline.
- A continuous 90-second advance pulse and an eight-pulse evacuation objective.
- Unknown activity that recon converts into Infantry, Tank, Artillery, or Enemy Base information.
- Blind one-way strikes with target-specific pressure damage.
- Recon and strike configurations drawn from the same physical fleet.
- Airframe classes describe physical size and tradeoffs only; mission roles come from installed, serviceable equipment.
- Three fictional civilian FPV kits enter the fleet with open carbon frames, exposed component stacks, removable retail guards, and distinct physical envelopes.
- Service exposes live mass, power, and speed bars; every component contributes an explicit tradeoff.
- Civilian guards are removed through three deterministic authored steps before oversized batteries or payload hardware fit.
- A reusable rack plus a separate, sealed, visually authored payload.
- The battery charger uses a five-position modular chassis with one functional front-panel plug pair in this milestone. Batteries rest beside the unit and connect by visible leads; each future capacity upgrade exposes another plug pair rather than adding a top-mounted battery rack.
- Seeded physical salvage deliveries with one readable compromise per salvaged part.
- Mission-facing Reach, Effect, Reliability, Arrival, committed value, and margin forecasts.
- Save schema 14 for frontline, salvage, payload, compromise, and concurrent deployed-aircraft state.

## Excluded

- Direct flight, visible hands, detailed combat, explosive construction, fuzing, or arming.
- Staff, automation, hideout expansion, distributed sites, and long-range aircraft.
- Deleting legacy exposure systems before the new loop passes playtesting.
- Rewriting `GAME_SPEC.md` before human acceptance.

## Acceptance

The player must be able to identify an urgent threat visually, understand a jury-rig disadvantage without a
paragraph, make a meaningful recon-versus-immediate-strike choice, physically convert an airframe, see the
frontline consequence, earn a positive successful margin, recover from one failed sortie, and choose to begin
another repair-and-launch cycle.
