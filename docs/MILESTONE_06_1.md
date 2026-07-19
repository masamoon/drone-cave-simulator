# Milestone 06.1 — Manned Field Excursions

## Goal

Let the player move risk away from the hidden workshop through short authored deployment and salvage procedures. Field operations trade workshop exposure for travel friction, carrying limits, persistent site attention, and the need to recover reusable aircraft.

## Field authority and format

`FieldOperationsSystem` owns remote sites, attention, planned excursions, cached aircraft references, salvage caches, and persistence. Battlefield truth remains in `BattlefieldSystem`, aircraft identity and location in `FleetSystem`, and recovered scrap in `InventorySystem`.

`FieldExcursionDirector` temporarily suspends workshop controls and UI, creates one compact low-poly vignette in the current scene, and restores the workshop afterward. There is no outdoor world, combat, NPC simulation, or direct drone flight. Manual save is unavailable during the interaction; an autosave occurs before departure and after return.

## Remote launch cache

One remote cache exists at normalized map position (0.30, 0.18). Remote routes use that origin and return site. Launch requires a staged drone, available site, and completion of the case-open, drone-seat, relay-connect, and launch procedure.

Remote launches add no workshop launch or workshop-route exposure. Relay activity adds site attention and executes the preplanned route without workshop control. Recall and retasking are unavailable while the workshop transmitter is off. Reusable aircraft become field-cached fleet actors and cannot be serviced until a recovery excursion returns the same runtime actor to the service bay or first free locker. Recovery requires workshop storage capacity. Kamikaze aircraft need no recovery.

## Salvage and attention

Strike salvage becomes a persistent field cache rather than immediate inventory: artillery yields 3 scrap and a destroyed base yields 5. A trip secures at most 4 tokens. Caches survive the current and following operational day and expire when the second subsequent day begins.

Site attention states are Safe 0–24.99, Exposed 25–49.99, Danger 50–74.99, Search 75–99.99, and Forced Retreat at 100. Contributions are entry +10, repeat visits +5 each capped +20, each visible Current/Stale hostile within 0.5 km +10, presence +1/second, case +5, relay +10, launch +15, active relay +0.15/second, returned drone +15, and salvage +5/token.

At 100, setup or collection stops, a two-second exit grace begins, secured items remain secured, the site becomes Hot for the rest of the day, and the hasty return adds +12 workshop exposure. Normal trace is +0 below 50, +3 from 50–74.99, and +7 from 75–99.99. A new day reduces site attention by 20 but never workshop exposure.

## Persistence and boundaries

Schema 13 persists site attention and visits, relay and Hot state, field-cached actors, salvage quantities and expiry, and launch/return sites. Schema 12 or older is rejected before mutation.

No player injury, lethal routine outcome, exact component loot, second remote site, open terrain, direct flight, or Milestone 07 discovery event is included.
