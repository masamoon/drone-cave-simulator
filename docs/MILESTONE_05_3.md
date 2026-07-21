# MILESTONE 05.3 — Daily Continuity and Sortie Economy

> Historical scope note: Milestone 05.4 supersedes daily request replacement and extends the saved mission state with a persistent battlefield, planning draft, active progress, and contact history. Milestone 13 supersedes the player-facing terminal day-advance flow with cot-based sleep. Economy and continuity decisions not explicitly changed by those documents remain applicable.

## Objective

Close the prototype campaign loop around the existing mission, fleet, inventory, and market authorities:

`Prepare fleet → run one or more sorties → receive funds and salvage → end operations → begin the next day → repair, replace, and upgrade`

This slice changes the earlier Milestone-5 exclusions for mission rewards, player-facing day advancement,
overnight battery turnaround, and planned expendable-airframe loss. It does not add direct flight or a parallel
economy.

## Starting fleet

A fresh Safe House owns three drones:

- the existing repairable Scout Field in the service bay;
- two complete, charged, tested Expendable Strike Field drones in locker slots 1 and 2.

Each expendable aircraft has an installed single-charge strike rack and an explicit persisted expendable role.
An expendable aircraft is removed from fleet ownership after an armed sortie resolves. Recon sorties do not
consume an aircraft merely because it has an expendable role.

## Sortie rewards

Resolution grants funds from the mission's authored operational value and outcome:

- exceptional success: 125%;
- success: 100%;
- limited success: 65%;
- observation-only: 45%;
- abort: 20%.

Recovered salvage grants zero to four base tokens from outcome, plus one token for a non-recon sortie that
recovers any salvage. Rewards are recorded on the mission runtime before its resolved event and are granted
exactly once. Funds remain authoritative in `MarketSystem`; salvage remains authoritative in `InventorySystem`.

## Day transition

The tactical terminal exposes `END OPERATIONS` whenever no mission is active. After ending operations, the same
control becomes `BEGIN NEXT DAY`. Beginning a day:

- advances the deterministic day seed and replaces expired requests;
- resets the completed-sortie counter;
- advances the existing market cycle once;
- restores installed battery charge on owned, non-deployed aircraft;
- preserves condition, faults, installed parts, funds, salvage, and fleet losses.

Overnight turnaround does not repair condition, replace lost aircraft, reload strike racks, or automatically pass
diagnostics.

The initial market's 300-fund motor and 250-fund battery listings are professional Compact-standard Scout
upgrades, so sortie income can improve the surviving reusable aircraft instead of targeting a chassis the player
does not own.

## Persistence

Schema 8 persists expendable fleet roles and each mission's airframe-loss and reward-grant state. Versions 1–7
remain readable. Consumed aircraft stay absent from owned fleet state after load, while their authored runtime
objects remain available for identity-safe restoration of an earlier save in the same session.

## Validation

Automated coverage must prove:

- a fresh Safe House owns exactly three drones and two are ready expendable strike aircraft;
- two separate armed sorties can resolve sequentially during one day;
- each expendable aircraft leaves owned fleet state after its sortie;
- rewards increase the existing funds and salvage balances;
- the tactical terminal can end operations and begin day two;
- next-day requests, market cycle, sortie count, and battery turnaround update deterministically;
- schema-8 fleet, market, mission, and socket ownership restore without duplication.

Human validation remains required for button discoverability, reward readability, starting-fleet readability,
economic pacing, and whether expendable loss feels sufficiently explicit before launch.

## Exclusions

- multiple simultaneous active missions;
- automatic condition repair or passing diagnostics;
- dynamic creation of replacement market identities;
- strike-rack reloading or crafting;
- loans, passive income, or procedural campaign generation;
- direct drone control or combat simulation.
