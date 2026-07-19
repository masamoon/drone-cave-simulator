# Milestone 06.1 Implementation Handoff

## Implemented

- `FieldOperationsSystem` owns the single remote site, visible-intel attention forecast, active attention, relay state, field-drone reference, salvage caches, expiry, and day cooldown.
- `FieldExcursionDirector` provides a generated first-person field vignette with explicit setup, recovery, salvage, leave, and forced-return states.
- `StorageLocationId.FieldSite`, `DroneStorageLocationKind.FieldSite`, and field-drone save records preserve aircraft identity and availability.
- Mission drafts and plans persist launch and return site identity and use the remote cache as route origin.
- Strike funds remain automatic; salvage creates recoverable caches and converts to existing scrap only when secured.
- Save persistence advances to schema 13 when field operations are configured.

## Objective validation targets

- Remote launch and return retain the same `DroneActor` identity.
- Field-cached aircraft are excluded from ready/service use until recovered.
- Salvage respects four-token capacity, partial recovery, exactly-once quantities, and day expiry.
- Only visible Current/Stale intelligence affects entry attention.
- Forced and normal returns apply their configured workshop trace once.

## Human validation still required

The temporary vignette deliberately uses authored prompts and floating objects rather than visible hands. Procedure readability, camera framing, audio urgency, cancellation comfort, and whether remote deployment is inconvenient enough without becoming tedious require Play Mode tuning.

## Validation

- Unity 6 script compilation completed without errors.
- Automated coverage verifies remote-origin routing, persistent drone identity, storage gating, salvage capacity/partial recovery/expiry, transactional return, and schema 13 rejection/round trips.
- Full suites: 94 Edit Mode and 63 Play Mode tests passed.
- The SafeHouse Play Mode smoke path constructs the risk authority, transmitter, field authority, deployment case, exit control, excursion director, and tactical terminal together.
