# Milestone 06 — Workshop Exposure and Lost Link

## Goal

Make transmitter use and repeated workshop-origin routes create a legible strategic cost. Radio silence must lower future exposure while sacrificing coverage, intervention, or immediate confirmation.

## Exposure authority

`WorkshopRiskSystem` is the sole mutable authority. Its immutable profile defines Quiet 0–14.99, Possible Attention 15–34.99, Pattern Suspected 35–54.99, Active Search 55–74.99, and Likely Located 75–100.

Initial sources are workshop launch +8, powered active-sortie transmission +0.25/second, motor diagnostic +3, familiar-route +5, and repeated-route +10. Routes are sampled every 0.10 km and compared by Jaccard similarity to the previous four workshop launches. Exposure never decays. At 100 it caps, sets `discoveryPending`, and leaves operations available for Milestone 07.

Normal UI exposes only qualitative state. Developer UI exposes exact value, source totals, last similarity, link timer, and discovery readiness.

## Radio contract

The physical transmitter starts powered and is required for a workshop launch. Power loss creates a five-second reversible grace period in which internal progress continues while telemetry and displayed discoveries freeze.

After grace expires:

- Recon completes its current leg, returns directly, skips later waypoints, and resolves Observation Only when it recovered useful intelligence or Aborted otherwise.
- A pre-release Grenade Drop recalls, refunds its reserved charge once after recovery, and applies maintenance for distance flown.
- A post-release Grenade Drop completes autonomously and confirms from onboard data after recovery.
- A Kamikaze Strike completes its authorized route but enters Awaiting Confirmation. Battlefield effects, rewards, and report resolution occur exactly once after transmitter power returns.

Powered Recon and pre-release Grenade Drop sorties expose Recall. Kamikaze sorties cannot be recalled.

## Persistence and boundaries

Schema 12 persists workshop exposure, source totals, transmitter and route history, actual route, link/recall/release state, refunds, and pending confirmation. Schema 11 or older is rejected before mutation.

Lights, chargers, rotor audio, discovery events, and Milestone 07 discovery incidents are excluded.
