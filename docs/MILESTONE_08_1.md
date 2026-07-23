# MILESTONE 08.1 — Hex Battlefield Intelligence Board

## 1. Goal

Replace the small named-node frontline with a broad, deterministic hex board that reads like a restrained
tabletop wargame:

`Detect activity area → route reconnaissance → identify exact hex, unit type, and next-day move → act or wait →
advance the operational day`

The board remains an intelligence-limited planning abstraction. It does not introduce direct control, detailed
combat, individual soldiers, or a second battlefield authority.

## 2. Binding board model

- The initial board is an 11×9 odd-row-offset hex grid covering the existing four-kilometre strategic area.
- Hexes replace named sectors as the frontline's movement, control, defense, targeting, and persistence space.
- The workshop occupies one authored hex. Friendly, contested, and enemy control are represented per hex.
- Enemy activities retain persistent identities, pressure, type, movement cadence, and stationary/mobile rules.
- Every mobile living unit moves to one adjacent hex when a new operational day begins. Bases remain stationary.
- Movement is deterministic from saved state and advances toward the workshop unless authored behavior says
  otherwise.
- The evacuation objective counts survived day advances rather than real-time pulses.

## 3. Intelligence rules

- An active unknown unit initially exposes only a broad detected activity area. This may not reveal its exact hex
  or type.
- A reconnaissance route that covers the unit's true hex reveals its exact hex, type, pressure, and authored
  next-day destination.
- Reconnaissance does not alter pressure or hidden movement truth.
- If a unit advances along a recon-confirmed next move, its new exact hex remains known but its following move
  becomes unknown. If its move was not known, the exact location is lost and only a new broad activity area is
  shown.
- Strikes require an exact identified hex. Arrival may no longer convert a broad activity area into a free blind
  target.
- The physical wall map and tactical terminal consume the same visible board state. Hidden coordinates and types
  must not affect their output.

## 4. Presentation

- Every board cell has a readable hex boundary with restrained friendly, contested, or enemy control treatment.
- Detected areas use an uncertainty treatment spanning multiple hexes.
- Identified Infantry, Tank, Artillery, and Enemy Base activities use distinct two-dimensional wargame counters.
- Recon-confirmed next-day movement uses a stable directional arrow.
- Named location labels and the real-time advance timer are removed.
- The 128×128 terminal texture and approved 512×512 physical wall texture remain deterministic derivatives.
- The tactical terminal uses a map-first sortie flow:
  `select a ready aircraft → left-click a staging hex → inspect reachable cells → right-click a target hex`.
- The staging leg consumes aircraft range. Cells for which the selected aircraft has no valid supported mission
  are darkened without hiding the underlying board state.
- Mission type is selected only from the target-hex context menu. A configuration carrying both observation and
  strike capability offers both valid choices; selecting one is the launch confirmation.
- The terminal sidebar is an aircraft roster, not a second planner. Its selection contract supports multiple
  active aircraft even though the current workshop still exposes one ready-shelf launch slot.
- An available final-approach feed starts automatically and remains a small non-interactive inset outside the
  map. It must not replace the workshop camera, disable interaction systems, or take over terminal input.

## 5. Persistence and migration

- Save schema 15 stores hex control, unit coordinates, detection areas, observation day, and known intent.
- Schema 14 frontline saves migrate once through the authored legacy-node-to-hex mapping.
- Migration must not duplicate activities, pressure, rewards, currency, aircraft, or battlefield ownership.
- Every activity and hex resolves to one valid board location after restore.

## 6. Validation

- Add Edit Mode coverage for grid validity, adjacency, deterministic movement, day-only advancement, recon
  disclosure, hidden-truth filtering, objective resolution, strike gating, and schema-14 migration.
- Add Play Mode coverage for day advancement, terminal/physical-map agreement, exact-hex discovery, and target
  selection, plus aircraft selection, staging, per-hex reachability, contextual tasking, and embedded feed state.
- Capture unknown-area, identified-counter, known-next-move, and post-day-advance Game View states.
- Inspect framing, label/counter collisions, hex readability, map orientation, clipping, contrast, and continuity
  on both the terminal and physical wall map.

## 7. Excluded

- Direct flight or driving, detailed ballistics, infantry simulation, terrain traversal, supply lines, fog-of-war
  algorithms beyond authored activity detection, procedural campaigns, and additional unit categories.
- Milestone 9 discovery incidents and game over.
- Milestone 14 unmanned ground vehicles.

## 8. Definition of done

The milestone is complete when the nine-node real-time graph is no longer authoritative, daily movement and recon
intelligence operate entirely on the hex board, strikes require identified hexes, schema 14 migrates safely, both
map surfaces present the same information-safe wargame view, and automated plus Game View validation pass.
