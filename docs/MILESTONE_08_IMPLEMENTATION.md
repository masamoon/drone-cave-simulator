# Milestone 08 Implementation Handoff

## Status

Milestone 08's implementation and automated validation are complete. Human Play Mode balance acceptance is
deferred because no full subjective playtest was available during this pass. The values below are therefore a
tested baseline, not final tuning.

## Implemented

- Raised fresh-run funds to 1,100 and tuned field, professional, complete-aircraft, damaged-aircraft, rack, and
  sealed-payload prices as one model.
- Tuned mission rewards, partial-outcome payout, reputation thresholds, sale fraction, compromise resale penalty,
  salvage cadence, delivered condition bands, and market stock counts.
- Kept every field-grade component required by a compact reconnaissance aircraft renewable and price-stable across
  market refreshes. Rotating listings still vary from their authored base price without cumulative drift.
- Added market queries for the cheapest required part, basic reconnaissance build cost, expected damaged-aircraft
  restoration cost, and operational replacement cost.
- Mission forecasts now use the least expensive currently viable aircraft replacement route instead of raw
  installed-component value when market data is available.
- Market and service readouts disclose compromise state and resale reduction. Damaged-aircraft listings show a
  projected field-part restoration cost while preserving hidden exact faults. Purchase blockers name the required
  storage remedy.
- Salvage delivery quantities and cadence are driven by `MarketDefinition`, keeping economy tuning in one immutable
  definition rather than duplicating values in runtime systems.
- Added `EconomyBalanceSimulator`, a deterministic development audit of sources, sinks, inventory value, viable
  next actions, and successful, mixed, and loss-heavy three-day runs.
- No save-schema change was required; existing runtime ownership and transaction authorities remain unchanged.

## Tested balance configuration

| Parameter | Value |
| --- | ---: |
| Starting funds | 1,100 |
| Starting scrap | 18 |
| Scrap liquidation value | 18 each |
| Base sale fraction | 42% |
| Compromised-part sale multiplier | 70% |
| Trusted / Professional reputation | 200 / 1,000 |
| Initial / daily salvage | 3 / 2 parts |
| Recurring salvage cadence | 2 parts every 2 sorties |
| Delivered salvage condition | 48–78% |
| Basic compact reconnaissance parts | 1,260 |
| Cheapest ready field aircraft | 850 |
| Cheapest damaged aircraft | 220 |
| Field sealed payload | 140 |
| Routine motor-and-propeller reserve | 145 |

The fresh workshop already owns aircraft and components; the 1,260 figure is the full from-parts replacement path,
not an opening mandatory expense. Fresh funds can buy a ready field aircraft while preserving 250 for immediate
repair, payload, or salvage decisions.

## Representative deterministic runs

| Three-day scenario | Ending funds | Lowest funds | Can continue | Affordable action bands at end |
| --- | ---: | ---: | --- | ---: |
| Successful | 2,900 | 1,100 | Yes | 4 |
| Mixed | 1,573 | 875 | Yes | 4 |
| Loss-heavy | 1,068 | 250 | Yes | 4 |

These are regression scenarios, not probability forecasts. They verify that a normal aircraft loss hurts, does not
drive funds below zero, and leaves a deterministic route back to another operation when the player follows with
reconnaissance and a partial successful strike.

## Automated validation

- Edit Mode: 191 passed, 0 failed.
- Play Mode: 77 passed, 0 failed, 13 intentionally ignored legacy tests superseded by the Milestone 07 Safe House
  pivot.
- Dedicated live checks cover the Safe House audit, all required renewable field parts over 24 market cycles, and
  projected damaged-aircraft restoration costs.
- Existing Milestone 07 frontline and salvage coverage remains in the passing suites.

## Deferred subjective validation

A human pass still needs to judge whether:

- 1,100 opening funds creates meaningful freedom without making the first purchase automatic;
- successful strike rewards feel worth the sealed payload and aircraft risk;
- the 42% sale rate and 70% compromise multiplier discourage liquidation without making selling feel pointless;
- three initial salvage parts and the two-sortie replenishment cadence create useful scarcity;
- the 200 / 1,000 reputation gates arrive at satisfying points;
- projected restoration costs are understood as estimates rather than guarantees;
- the new market and service text remains comfortable to read during ordinary play.

The player-facing text was checked for compilation and runtime state coverage, but a complete human interaction and
feel pass must be recorded before Milestone 08 is called subjectively accepted.
