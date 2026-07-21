# MILESTONE 10 — Functional Hideout Upgrades

## 1. Goal

Let the player invest workshop earnings in a small set of permanent, physically visible improvements:

`Earn resources → choose an upgrade → see the workshop change → gain a concrete operating advantage`

Upgrades should strengthen attachment to the hideout while forcing meaningful spending choices. This is not a
freeform decoration system.

## 2. Binding scope

Implement three authored upgrade lines:

1. **Parts storage** — increases compatible loose-part capacity through a visible storage fixture expansion.
2. **Battery turnaround** — adds charging throughput through a visible charger or charging-rack improvement.
3. **Emission control** — improves one clearly stated concealment property, such as powered-transmission exposure
   rate or discovery-response time, through a visible shielding or cutoff-system improvement.

Each line has a base state and no more than two purchased tiers. Exact costs and effects are authored immutable
definitions. Purchased tier, availability, and installation state are mutable saved runtime data.

## 3. Upgrade rules

- Purchases use the existing economy authority and are atomic.
- The terminal discloses cost, current tier, next effect, and any prerequisite before purchase.
- A purchase immediately changes the relevant physical workshop fixture and functional capability.
- Upgrades never create or duplicate parts, aircraft, currency, or storage ownership.
- Capacity reductions during migration or content changes must resolve safely without deleting owned items.
- Concealment upgrades modify the existing risk contract rather than creating a second risk meter.
- Visual changes remain grounded low-poly additions consistent with the existing room.

## 4. Progression targets

- A player normally chooses between equipment and a hideout improvement rather than buying every upgrade
  immediately.
- Every tier has a noticeable functional effect and a readable physical change.
- No upgrade is mandatory to recover from an ordinary aircraft loss.
- No upgrade completely removes scarcity, exposure, or the possibility of discovery.
- The workshop remains spatially legible and all existing stations remain reachable after every upgrade state.

Exact costs, tier effects, and pacing require human play-testing against the Milestone 8 economy.

## 5. Persistence and validation

- Persist purchased tiers and restore both capability and physical presentation deterministically.
- Add automated tests for prerequisites, atomic spending, tier limits, capability application, migration, and
  save/load.
- Validate all upgrade combinations in Play Mode for station access, storage ownership, charging behavior, and
  discovery integration.
- Perform Game View visual QA for every tier, inspecting framing, clipping, occlusion, scale, material consistency,
  interaction-target readability, lighting, and continuity between upgrade states.

## 6. Excluded

- Free placement, furniture rearrangement, cosmetic-only purchases, staff, automation, new rooms, workshop
  relocation, multiple hideouts, and a generic construction system.
- New drone categories, mission types, or battlefield systems.
- Upgrades that make the workshop immune to discovery.

## 7. Definition of done

The milestone is complete when all three upgrade lines can be purchased, seen, used, saved, and loaded; their costs
create meaningful competition with equipment spending; existing workshop interactions remain reachable and valid;
automated tests pass; and every visual state passes final Game View QA.
