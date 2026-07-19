# Milestone 05.5 Implementation Handoff

## Implemented

- `SortieProfileDefinition` now owns immutable category wear weights.
- `SortieMaintenanceResolver` forecasts and applies deterministic localized wear.
- `MissionRuntimeData` persists detailed maintenance records and an exactly-once flag.
- `DroneAssemblyState` separates launch-blocking faults from serviceable advisories.
- Tactical planning and reports show forecasted and actual maintenance consequences.
- A return invalidates the aircraft diagnostic and installed-part tested states.
- Mission persistence advances to schema 11.

## Tuning

The formulas and readiness thresholds are fixed by the milestone. Forecast wording, category presentation, and the subjective frequency with which a player chooses replacement over accepting wear still require play-testing.

## Boundaries

Charging, replacement, market purchasing, salvage, and fleet storage remain the only maintenance responses. No new repair currency or generic fault simulation was introduced.

## Validation

- Unity 6 script compilation completed without errors.
- Edit Mode suite: 94 passed, 0 failed.
- Play Mode suite: 63 passed, 0 failed.
- Human feel validation is still required for forecast clarity and replacement-versus-wear pacing.
