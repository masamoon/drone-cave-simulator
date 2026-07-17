# MILESTONE 04.3 — Market and Salvage Economy

## 1. Objective

Turn the modular fleet into an economic decision loop before battlefield missions are introduced:

`Inspect stock → compare repair options → buy, hold, strip, sell, or wait → preserve useful fleet capacity`

This milestone adds an in-world market terminal, persistent stock, funds, asset transfers, and deterministic between-shift refresh. It does not add missions or advance market time automatically.

## 2. Market terminal

The Safe House receives one interactable terminal with four functional prototype views:

- Parts — fully disclosed loose components;
- Salvage Drones — partially disclosed incomplete or damaged aircraft;
- Fleet — the player's stored whole drones and empty frames;
- Sell — eligible loose parts, whole locker drones, empty frames, and scrap tokens.

The interface shows price, compatibility, condition disclosure, storage blockers, and the exact asset identity involved in a transaction. IMGUI remains acceptable for this prototype.

## 3. Runtime stock and ownership

`MarketDefinition` contains immutable authored stock rules and presentation. `MarketRuntimeData` and `MarketListingRuntimeData` contain the current cycle, saved seed, listing state, and stable runtime asset identities.

Market stock consists of persistent `PartRuntimeData` and `DroneActor` records. Buying transfers the existing identity and ownership; it never clones the purchased asset. Selling performs the inverse transfer. Sold and purchased listings cannot be transacted twice.

Installed parts cannot be sold separately until physically removed in service mode. A whole drone sold from the locker includes all installed parts. Non-empty frames cannot be converted to chassis scrap until stripped.

## 4. Transactions and capacity

Purchases are atomic. Before changing funds or ownership, the market validates:

- sufficient funds;
- a compatible free part-storage slot for a loose part;
- a free locker slot for a whole drone;
- listing availability;
- unique ownership of every transferred identity.

If any condition fails, no funds, listing state, storage occupancy, or ownership changes.

Loose-part sale value is:

`base value × condition multiplier × 0.55`

Whole-drone value is the condition-adjusted frame value plus the sale value of every installed component. Scrap tokens sell for 10 funds each, with an explicitly selected quantity.

## 5. Salvage-drone disclosure

Loose/new components show complete specifications and exact condition. Salvage-drone listings show:

- frame family and grade;
- installed and visibly missing components;
- broad visible condition bands;
- asking price.

Exact component faults and diagnostic details remain hidden until the purchased drone is brought into the workshop and diagnosed. Buying does not silently reveal faults.

## 6. Deterministic market cycles

Stock remains fixed during an operational shift. `AdvanceMarketCycle(seed)` replaces eligible listings deterministically from authored listing templates. Equal definitions and seeds produce equal runtime stock, identities, condition bands, and prices.

The future `OperationalShiftSystem` will be the only gameplay caller. Milestone 4.3 exposes and tests the operation but does not add a player-facing refresh button or real-time timer.

## 7. Initial economy scenario

The first Safe House state contains:

- the existing damaged Scout Field drone and its owned replacement parts;
- the incomplete Survey Professional drone in locker slot 1;
- 600 starting funds;
- Survey-compatible motor and battery listings costing 550 combined;
- one incomplete Utility Field salvage drone listing costing 520.

This permits repairing the inexpensive working drone immediately, spending nearly all funds on the premium Survey chassis, holding that chassis while waiting for compatible salvage, or stripping/selling assets to improve the working fleet.

## 8. Persistence

Schema version 6 adds:

- funds;
- market cycle and seed;
- listing runtime records;
- market-owned part records;
- market-owned drone and installed-part records;
- completed transaction state.

Versions 1–5 remain readable. Missing economy data initializes the authored first-cycle scenario without changing pre-existing player part or drone identities. Definitions and derived prices remain recomputable; authoritative funds, identities, ownership, condition, disclosure, and listing availability persist.

## 9. Public types

Add:

- `MarketDefinition`;
- `MarketRuntimeData`;
- `MarketListingRuntimeData`;
- `EconomyRuntimeData`;
- market listing category and transaction-result value types;
- a narrow market terminal interaction and presentation layer.

Extend existing part/frame definitions only with values already approved by Milestone 4.2's modular progression plan. Mutable economic state must not be stored in ScriptableObjects.

## 10. Validation

Edit Mode coverage:

- atomic purchase success and insufficient-funds rejection;
- part-storage and locker-capacity rejection;
- stable identity transfer without cloning;
- loose-part, whole-drone, empty-frame, and selected scrap valuation;
- installed-part individual-sale rejection;
- non-empty chassis-scrap rejection;
- partial salvage fault disclosure;
- one-time listing purchase and sale behavior;
- deterministic cycle generation;
- schema-6 round trip and schema-1–5 migration;
- prevention of duplication and buy/sell exploits.

Play Mode coverage:

- open the terminal through normal player interaction;
- purchase a compatible component and observe its actual storage occupancy;
- purchase the salvage drone when capacity permits;
- reject the same purchase when funds or capacity are insufficient;
- sell an eligible loose part and a locker drone;
- preserve the complete arrangement through save/load;
- retain all Milestone 4.2 Edit and Play Mode regressions.

## 11. Human acceptance and exclusions

Human feedback is required for terminal readability, fleet and part comparison clarity, compatibility messaging, transaction confirmation, storage-block explanations, and whether the initial 600-fund scenario creates meaningful pressure without obscuring the workshop loop.

Excluded:

- missions and mission rewards;
- automatic day/shift advancement;
- charging, crafting, loans, or repair recipes;
- player-triggered market rerolls;
- real-time market timers;
- chassis scrapping before complete teardown;
- permanent drone loss;
- campaign progression.

Milestone 5 mission work must not begin until this market loop passes automated validation and receives human readability/economic-pressure approval.
