# MILESTONE 14 — Unmanned Ground Vehicle Operations

## 1. Goal

Add contemporary unmanned ground vehicles (land drones) as persistent workshop equipment whose main value is
moving risk away from the operator:

`Prepare UGV and aircraft → stage them at a remote site → launch or recover aircraft away from the hideout →
collect salvage → return, lose contact, or abandon the platform`

The UGV may provide an offensive support option, but its defining roles are a mobile launch-and-recovery site for
flying drones and a way to retrieve salvage without putting the player physically in the field.

This is a future milestone. It does not authorize implementation while an earlier active milestone is unfinished.

## 2. Binding scope

- Add one grounded, contemporary wheeled or tracked UGV category with authored physical limits and a persistent
  runtime identity.
- Let the player own, store, inspect, repair, configure, deploy, recover, sell, and lose UGVs through the existing
  economy, inventory, mission, and persistence authorities.
- Support an authored carrier configuration that stages a limited number of compatible aerial drones at a remote
  launch-and-recovery site.
- Track each staged aircraft explicitly: carrier, remote site, availability, condition, payload, mission state, and
  whether it returned to the UGV, another recovery point, or was lost.
- Support salvage-recovery assignments with bounded capacity, travel time, route risk, and a legible returned cargo
  manifest.
- Support a limited offensive mission profile resolved through the abstracted battlefield simulation. It may affect
  local pressure or a known target, but it does not add direct driving, manual gunnery, detailed ballistics, or
  autonomous target selection.
- Make the primary tradeoff explicit: remote staging and salvage retrieval reduce player exposure and workshop
  launch signatures, but commit a valuable slow platform, may reveal a route or remote site, and can lose the UGV,
  its cargo, and staged aircraft together.
- Integrate concurrent operations so a deployed UGV, its staged aircraft, and workshop aircraft cannot be assigned
  twice or appear in two locations.
- Present route, signal, endurance, cargo, aircraft capacity, expected return, committed value, and known threats
  before commitment without exposing hidden battlefield truth.

## 3. Workshop and mission flow

1. The player selects an owned UGV and configures its carrier, salvage, or offensive support loadout.
2. Compatible aircraft and cargo are physically transferred into explicit staging slots; exact installed state is
   deterministic and saveable.
3. The tactical map offers only known and reachable remote sites or salvage routes.
4. Deployment commits the UGV, cargo, and staged aircraft to one operation and removes them from workshop
   availability.
5. At a reached staging site, compatible aerial sorties may launch from and return to the UGV without first
   returning to the hideout.
6. Salvage assignments return a bounded manifest, suffer condition and compromise outcomes, or report partial or
   total loss.
7. Recovery returns the actual surviving platform, aircraft, parts, and cargo to authoritative workshop storage.

## 4. Economy and risk rules

- UGV acquisition, repair, replacement, and recovery compete with aircraft, payload, and workshop spending; they
  are not free progression rewards.
- Remote staging reduces relevant workshop exposure only when the launch genuinely occurs away from the hideout.
  It does not erase existing exposure, concealment consequences, or discovery readiness.
- Salvage recovery avoids direct player field exposure but never guarantees profit. Capacity, route knowledge,
  delay, platform wear, interception, and the opportunity cost of committed equipment remain meaningful.
- Loss resolution is deterministic from authored mission state and seeded simulation inputs. It never depends on
  uncontrolled vehicle physics.
- The sustainable-economy audit must include UGV purchase, routine repair, total-loss, staged-aircraft-loss, and
  recovered-salvage scenarios before this milestone can be accepted.

## 5. Persistence and authority

- Immutable chassis, compatibility, carrier-module, cargo, and mission-profile definitions use ScriptableObjects.
- Mutable condition, ownership, location, installed modules, cargo, staged aircraft, mission assignment, and
  campaign effects remain runtime/save data.
- `FleetSystem` remains authoritative for aerial aircraft. A dedicated ground-vehicle authority may be introduced
  only if it does not duplicate ownership or mission state already held elsewhere.
- Save/load must resolve every UGV and staged aircraft to exactly one location and safely recover partial staging,
  launch, return, unloading, and interrupted mission transitions.

## 6. Validation

- Add automated tests for compatibility, capacity, assignment exclusivity, staging and unloading, launch and
  recovery, salvage manifests, losses, economy integration, risk integration, and save/load round trips.
- Validate one carrier cycle with multiple aerial sorties, one successful salvage recovery, one partial recovery,
  one UGV loss with staged equipment, and one limited offensive support outcome in Play Mode.
- Confirm the player can distinguish workshop aircraft, staged aircraft, deployed aircraft, returned aircraft, and
  lost aircraft at every point.
- Perform final visual QA for workshop storage, carrier slots, cargo readability, tactical-map presentation, scale,
  clipping, occlusion, material consistency, and continuity between every staged state.

## 7. Excluded

- Direct UGV driving, first-person field combat, manual turrets, detailed ballistics, infantry control, autonomous
  lethal target selection, open-world terrain, and real-world construction or weapon-integration instructions.
- Player injury simulation or a playable outdoor salvage expedition.
- A generic vehicle physics sandbox, procedural pathfinding campaign, large UGV fleets, crew systems, or logistics
  automation.
- New aerial drone categories unless separately authorized by an active milestone.

## 8. Definition of done

The milestone is complete when one persistent UGV can be prepared in the workshop, stage compatible aerial drones
at a remote site, retrieve bounded salvage, contribute through one abstract offensive profile, return or be lost
without duplicating ownership, and create a readable economy-and-risk tradeoff that passes automated, Play Mode,
save/load, and Game View validation.
