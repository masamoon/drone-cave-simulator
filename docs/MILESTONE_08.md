# MILESTONE 08 — Sustainable Workshop Economy

## 1. Goal

Tune the existing salvage, market, part, and sortie economy into a sustainable workshop loop:

`Recover equipment → inspect compromises → repair, strip, buy, sell, or hold → prepare a viable sortie → absorb the result`

The economy should create scarcity and improvisation without allowing ordinary bad luck or one failed sortie to
soft-lock the run. This milestone tunes existing authorities; it does not replace them with a second economy.

## 2. Binding scope

- Audit all fund, reputation, scrap, sealed-payload, complete-aircraft, and loose-part sources and sinks.
- Tune sortie rewards, salvage quantities, sale values, purchase prices, condition multipliers, reputation
  thresholds, stock counts, and refresh cadence as one connected model.
- Keep field-grade parts needed for a basic viable build continuously purchasable when the player has sufficient
  funds.
- Keep higher-grade parts, damaged aircraft, and complete aircraft scarce enough to preserve salvage and repair
  decisions.
- Make compromised salvage readable before the player commits to installing, stripping, or selling it.
- Preserve stable runtime identities, atomic transactions, deterministic stock refresh, and existing ownership
  authority.
- Add development-only balance telemetry or a reproducible balance worksheet showing sources, sinks, inventory
  value, and viable next actions across representative runs.
- Improve market and service readouts only where required to explain value, compatibility, condition, compromise,
  and storage blockers.

## 3. Balance targets

- A fresh run can field a basic reconnaissance aircraft without relying on a rare rotating listing.
- Losing one ordinary aircraft is costly but recoverable through a combination of available stock, salvage, and
  successful follow-up play.
- Selling everything, stripping everything, and always buying the most expensive available part are not dominant
  strategies.
- A damaged aircraft can be meaningfully cheaper than its disclosed expected repair cost without being a guaranteed
  bargain.
- At least two plausible uses compete for funds after a normal successful operational day.
- Scarcity comes from tradeoffs and timing, not from unavailable mandatory components.

Exact values remain tuning parameters and require human play-testing.

## 4. Persistence and authority

`MarketSystem` remains authoritative for funds, reputation, listings, and transactions. `InventorySystem` remains
authoritative for loose parts and salvage. `FleetSystem` remains authoritative for complete aircraft and their
locations. Mission and frontline systems grant outcomes through those authorities and may not maintain parallel
balances.

Any save-schema change must migrate the immediately preceding supported schema without duplicating currency,
stock, parts, or aircraft.

## 5. Validation

- Add automated coverage for tuned transaction, refresh, reward, salvage, and no-duplication rules.
- Run deterministic multi-day balance simulations for representative successful, mixed, and loss-heavy outcomes.
- Complete human Play Mode runs that include buying, selling, stripping, installing salvage, losing an aircraft,
  and recovering to another viable sortie.
- Record the tested balance configuration and list all values still requiring subjective feedback.

## 6. Excluded

- New component categories or drone classes.
- Crafting recipes, repair materials, staff, automation, loans, insurance, or real-time market timers.
- Game-over conditions caused only by having insufficient funds.
- Hideout upgrades, discovery incidents, and final campaign progression.

## 7. Definition of done

The milestone is complete when the economy remains understandable and recoverable across representative multi-day
runs, required parts cannot disappear behind rotation, ownership remains deterministic, automated tests pass, and
the remaining subjective balance risks are documented.

Implementation and automated validation are recorded in `MILESTONE_08_IMPLEMENTATION.md`. Human balance and feel
acceptance remains intentionally deferred.
