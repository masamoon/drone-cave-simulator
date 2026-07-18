# MILESTONE 05 — Daily Sorties and Abstract Battlefield Missions

> Historical scope note: Milestone 05.4 supersedes the authored daily-request, mission-definition, and deployment-site flow in this document. This file remains a record of the earlier implementation target.

## 1. Objective

Connect workshop preparation to a playable daily operation loop:

`Review requests → choose a drone and loadout → stage on ready shelf → deploy → continue workshop work → receive report → recover the same drone → decide whether to sortie again or end the day`

The first mission slice supports three authored request archetypes:

- Recon — observe a location or route and return;
- Precision Strike — attack a known stationary target such as an artillery piece;
- Armed Search — locate, positively identify, and engage an uncertain hostile target.

Missions remain abstract simulations shown through the tactical map, radio updates, and reports. There is no direct flight, combat scene, ballistics simulation, or graphic casualty presentation.

Milestone 4.3's market and economy are prerequisites. Mission outcomes consume the existing fleet and economy ownership model rather than creating parallel stock, funds, or rewards.

## 2. Daily cadence and sortie limit

Each operational day begins with three authored requests, initially one of each archetype. The player may complete more than one sortie in a day, but only one mission may be active at a time in this milestone.

There is no arbitrary fixed “one sortie per day” counter. The practical limit comes from physical state:

- only the drone on the ready shelf can deploy;
- the drone must have a current passing diagnostic;
- its battery must be sufficiently charged for the selected request;
- mission-specific capabilities must be installed;
- returned drones lose charge and condition;
- strike ordnance is consumed;
- another prepared drone may perform a later sortie.

Returned depletion still limits same-day reuse. Beginning the next authored day performs an abstract overnight
battery turnaround for owned aircraft; it does not repair condition or pass diagnostics. A better prepared fleet
therefore supports more work within a day without a hidden action-point rule.

The player may end operations voluntarily whenever no mission is active. Unresolved offers expire when the next authored day begins.

## 3. Mission definitions and runtime state

Immutable `MissionDefinition` assets contain:

- stable ID and display name;
- mission archetype;
- briefing and operational value;
- estimated duration;
- minimum capabilities and battery reserve;
- weighted stat profile;
- uncertainty and expected wear;
- authored radio updates and report language.

Mutable `MissionRuntimeData` uses explicit states:

`Available → Accepted → Assigned → Active → Returning → Resolved`

Runtime data contains the assigned drone identity, deployment site, saved random seed, elapsed time, result, wear, consumed ordnance, and readable score breakdown. Definitions never store campaign progress.

## 4. Mission archetypes

### 4.1 Recon

Recon values observation, endurance, control, and reliability. It does not require a strike rack.

The initial `Road Watch` request asks the player to inspect a road before friendly movement. Reports distinguish coverage, image quality, signal stability, route time, and whether the request produced useful intelligence.

### 4.2 Precision Strike

Precision Strike targets a known stationary military object. The initial `Counter-Battery Window` request targets an identified artillery position.

It requires:

- a mission-ready drone;
- an installed strike rack with at least one ordnance charge;
- sufficient payload, control, and reliability;
- a camera capable of confirming the known target.

Payload, control, reliability, and observation determine the result. A weak result may abort, miss, damage but not disable the target, or complete the strike. The report focuses on equipment effect and aircraft recovery, not spectacle.

### 4.3 Armed Search

Armed Search combines reconnaissance and engagement against an uncertain hostile position. The initial `Broken Treeline` request concerns a reported hostile infantry team near a tree line.

It requires the strike rack plus stronger observation and control thresholds than Precision Strike. Positive identification is a hard gate: inadequate identification produces an abort or observation-only result, never an invented engagement. This preserves the game’s contemporary support-cell tone and prevents the mission from becoming an abstract kill counter.

Armed Search has the highest uncertainty, transmission time, expected wear, and future exposure contribution.

## 5. Physical strike capability

Add one optional shared strike-rack socket to the current quadcopter layout. It is not part of ordinary flight completeness, but armed missions require it.

`PartDefinition` gains mission capabilities and `PartRuntimeData` gains persistent consumable charges. The initial Safe House receives one loose Field strike rack with one charge in serviceable-parts storage.

Installing and removing the rack uses the existing deterministic service interaction. Launching an armed mission consumes one charge exactly once. An empty rack remains a persistent part but cannot satisfy another armed assignment. Reloading, crafting, purchasing ordnance, and explosive visuals remain outside this milestone.

## 6. Deployment sites

Two immutable `DeploymentSiteDefinition` assets are available:

- Workshop-adjacent launch — fastest response and simplest recovery, but longer workshop-origin transmission exposure for the later risk system;
- Remote team launch — slower handoff and slightly higher handling wear, but lower workshop-origin exposure.

Milestone 5 records authored exposure contributions for later consumption but does not implement `WorkshopRiskSystem` yet.

## 7. Tactical map and concurrent work

The Safe House tactical map becomes interactive. Its functional prototype interface shows:

- current day and unresolved requests;
- archetype, briefing, urgency, duration, and operational value;
- selected deployment site;
- the ready-shelf drone and mission-specific eligibility;
- expected strengths, weaknesses, component value, and wear;
- accept, assign, launch, acknowledge report, and end-operations actions.

After launch, the map closes and normal workshop interaction resumes. The mission timer continues independently. Authored radio updates announce progress without taking control away from the player. The map may be reopened to inspect the active mission.

## 8. Deterministic resolution and return

Assignment saves a random seed. Resolution combines the assigned drone’s recomputed `DroneStatsSnapshot`, installed capabilities, deployment-site modifiers, mission weights, and the saved roll.

Reports expose the relevant calculation in readable categories rather than one opaque percentage:

- airframe and component readiness;
- observation or identification quality;
- endurance reserve;
- control and signal stability;
- payload suitability;
- reliability;
- deployment-site effect;
- uncertainty roll;
- resulting operational outcome.

The same `DroneActor` returns. The result applies deterministic battery depletion, minor frame and component wear, and mission-specific strike-charge consumption. Severe results may create heavy wear or failed components, but this milestone does not permanently delete a frame or create missing battlefield parts.

The returned drone goes to the service bay when available; otherwise recovery waits in an explicit returning state until a valid destination exists.

## 9. Operational-day coordination

`OperationalDaySystem` owns only day cadence, offered mission IDs, completed sortie count, and whether the player ended operations. It does not own fleet, part, mission-result, or future risk/economy data.

The first day seeds:

- `Road Watch` — Recon;
- `Counter-Battery Window` — Precision Strike against stationary artillery;
- `Broken Treeline` — Armed Search against a reported infantry position.

New-day offer generation is deterministic from the day seed. Authored missions may repeat in later prototype days, but their runtime identities remain distinct.

## 10. Persistence

Schema version 8 contains the version-7 mission data plus:

- operational day index and seed;
- offered mission runtime records;
- assignment and active timing state;
- saved resolution seed;
- report and acknowledgement state;
- pending return state;
- strike-rack consumable charges.
- expendable strike-airframe role and loss state;
- one-time mission funds and salvage rewards.

Versions 1–7 remain readable. Loading an active mission resumes its timer and preserves its assigned drone. Loading during `Returning` safely completes or waits for a valid service destination. Derived scores are recomputed and validated from current saved runtime parts rather than persisted as authoritative statistics.

## 11. Validation

Edit Mode coverage:

- explicit mission-state transitions and invalid-transition rejection;
- ready-shelf and unique-assignment gating;
- recon eligibility without a strike rack;
- armed eligibility requiring a charged strike rack;
- positive-identification gate for Armed Search;
- deterministic results from identical definitions, drone state, site, and seed;
- distinct stat weighting across all three archetypes;
- one-time ordnance consumption;
- return charge and wear without identity replacement;
- multiple sequential sorties with different ready drones;
- voluntary day end and deterministic next-day offers;
- schema-6 round trip and schema-1–5 compatibility.

Play Mode coverage:

- open the tactical map through normal `E` input;
- repair, diagnose, and stage the Scout;
- assign and launch Road Watch;
- interact with storage while Road Watch remains active;
- receive and acknowledge a deterministic report;
- recover the same worn drone identity;
- install the strike rack and complete one armed request;
- reject a second armed launch after its charge is consumed;
- save/load during an active mission and during return;
- all Milestone-4.3 market and earlier regression tests remain green.

## 12. Human acceptance and exclusions

Human feedback is required for mission-board readability, report clarity, timer pacing, radio-update frequency, whether stat trade-offs are understandable, whether armed-request language fits the tone, and whether one-at-a-time missions still permit satisfying workshop multitasking.

Excluded:

- direct piloting or combat presentation;
- moving-target ballistics;
- civilian simulation or casualty scoring;
- loss of aircraft not explicitly designated expendable;
- multiple simultaneous active missions;
- player-managed charging and ordnance reloading;
- dynamic generation of replacement market identities;
- workshop exposure and discovery events;
- procedural campaign generation.

Milestone 4.3 must pass its automated and human approval gates before this milestone begins. Milestone 6 risk work must not begin until this mission loop passes automated validation and receives human pacing/readability approval.
