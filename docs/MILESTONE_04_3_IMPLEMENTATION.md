# Milestone 4.3 Implementation — Market and Salvage Economy

## Outcome

The Safe House now contains a persistent in-world exchange terminal backed by runtime asset ownership rather than cloned purchases. The first scenario starts with 600 funds, Survey Field motor and battery listings worth 550 together, and one incomplete Utility Field salvage drone at 520.

The player can buy disclosed loose components, buy a partially disclosed salvage aircraft, compare the owned fleet, sell eligible stored parts, sell whole locker drones, and sell selected scrap tokens. Capacity and funds are validated before any mutation. Purchased and resold assets preserve their part or drone identity.

## Runtime architecture

`MarketSystem` owns funds, listing availability, market cycle state, valuation, and atomic transfer orchestration. `InventorySystem` remains the only writer of loose-part occupancy, while `FleetSystem` remains the only writer of whole-drone locker occupancy. The market calls narrow acquire/release methods rather than editing either system's collections.

`MarketDefinition` stores immutable starting economy values. `EconomyRuntimeData`, `MarketRuntimeData`, and `MarketListingRuntimeData` store mutable funds, cycle seed, listing state, disclosure, prices, and stable asset IDs.

Loose-part sale value uses the approved `base value × condition multiplier × 0.55` rule. Whole-drone value combines the condition-adjusted frame with the sale values of installed parts. Player-sold assets re-enter persistent stock at a higher repurchase price, preventing a buy/sell gain loop.

## Safe House integration

The initial market stock contains:

- one 96% Survey Field motor for 300 funds;
- one 93% Survey Field battery for 250 funds;
- one worn, incomplete Utility Field salvage drone for 520 funds.

Loose components disclose exact specifications and condition. The Utility listing discloses its frame, visible condition band, and installed/missing count while keeping exact faults hidden until workshop diagnosis.

The terminal opens through the normal interaction system. Its functional prototype has Parts, Salvage Drones, Fleet, and Sell views, plus explicit purchase confirmation and transaction/storage status.

## Persistence

Safe House saves now use schema version 6. Funds, cycle/seed, listings, transaction availability, market-owned identities, fleet occupancy, inventory occupancy, and diagnostic disclosure restore together. Versions 1–5 remain readable.

Save/load preparation reconciles market-owned and player-owned drone registration before fleet occupancy is restored, allowing a purchased salvage drone—or a sold player drone—to retain the same aggregate identity across loading.

## Validation

- Edit Mode: 54/54 passed, including 10 new economy tests.
- Play Mode: 30/30 passed, including 4 new market interaction and persistence tests.
- Normal `E` interaction opens the physical market terminal.
- Purchasing the Survey motor transfers the same instance into an authored part-storage slot.
- Purchasing the Utility salvage drone occupies the next physical locker slot, preserves hidden faults, and survives schema-6 save/load.
- The existing URP punctual-light shadow-atlas warning remains; no market or ownership runtime errors were observed.

## Subjective follow-up

Terminal layout, comparison density, physical placement, confirmation feel, and the severity of the initial 600-fund pressure still require human judgment. Market refresh remains callable only by future shift coordination; there is no player reroll or real-time timer.
