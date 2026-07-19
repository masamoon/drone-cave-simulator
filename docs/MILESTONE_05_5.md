# Milestone 05.5 — Return-to-Workbench Consequences

## Goal

Make every reusable sortie feed the physical workshop loop. A returning aircraft should require a meaningful inspection, charge, replacement, salvage, or fleet choice without turning maintenance into random punishment.

## Binding scope

- Recon wear weights: Camera 4, Antenna 3, Motor 2, Propeller 1, Battery 1.
- Grenade Drop wear weights: Motor 4, Propeller 3, Battery 2, Antenna 2, Strike Rack 2, Camera 1.
- Frame wear is `clamp(BaseWear + utilization × 0.03, 0, 0.20)`.
- Localized component wear is `clamp(BaseWear × 2 + utilization × 0.06, 0.04, 0.12)`.
- Below 65% utilization, one seeded weighted component receives component wear. At or above 65%, two distinct seeded weighted components receive a 70/30 split.
- Battery drain remains proportional to route utilization. Mission outcome never adds hidden damage.
- Kamikaze aircraft do not receive return maintenance.
- Conditions below 75% create advisories. Frame or required-part conditions below 45% block readiness.
- A serviceable advised aircraft may launch after a passing diagnostic.
- Return maintenance invalidates the previous diagnostic and tested state.

The launch forecast exposes battery use, frame wear, severity, and likely categories, but not the exact seeded component. The report exposes the actual before/after consequences through serialized `SortieMaintenanceRecord` entries. `maintenanceApplied` is the exactly-once gate.

## Persistence and acceptance

Mission-only saves use schema 11 and reject schema 10 or older before mutation. Active sorties must not contain prematurely applied maintenance; returning and resolved sorties must never apply it twice.

This milestone does not add repair materials, named fault tags, or speculative damage systems.
