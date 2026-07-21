# Marketplace Expansion

## Outcome

The Safe House exchange now supports four buy-side catalogs:

- loose parts with exact condition, compatibility, grade, and stat disclosure;
- expendable strike drones delivered tested, charged, and with one loaded strike rack;
- complete, diagnosed drones that retain their authored runtime identity and readiness;
- a rotating assortment of damaged or incomplete drones whose exact faults remain hidden until workshop diagnosis.

Listings are presented as thumbnail cards. Opening a card shows the full available specification, condition disclosure, price, storage requirement, and access requirement before purchase. Fleet and sell views remain part of the same in-world terminal.

Empty scratch-build aircraft are no longer created from the fleet tablet. A bare Field FPV frame is instead sold through the exchange's Frames catalog for 100 funds and is delivered to a free drone locker. Field payload mounts and sealed payloads are continuously restocked in the Parts catalog. Each new operational day also delivers one sealed payload to serviceable-parts storage at no cost; additional payloads remain purchasable.

## Advanced-stock access

Advanced equipment uses broker reputation rather than a conventional character level:

- **Field**: available from the beginning;
- **Trusted**: unlocked at 250 reputation;
- **Professional**: unlocked at 700 reputation.

Mission fund awards grant the same amount of reputation. Buying equipment and selling assets do not change reputation, so access cannot be farmed through market transactions and is not lost when funds are spent. Newly unlocked rotating stock enters circulation on the next operational-day market refresh.

This rule ties advanced aircraft access to demonstrated battlefield support while keeping funds as a separate scarcity decision.

## Stock and ownership

The authored stock pool contains Compact, Survey, Heavy, shared-camera, and shared-antenna components; complete Scout, Survey, and Utility aircraft; ten replacement strike aircraft; and multiple deterministic damaged-aircraft variants. `AdvanceMarketCycle(seed)` selects the active unlocked assortment deterministically. Two Field-tier strike aircraft are guaranteed in every market cycle while stock remains, and purchased stock is never returned by a later rotation.

Purchases continue to transfer existing part and drone identities atomically into physical storage. Complete aircraft arrive diagnosed and ready when their installed equipment is serviceable. Strike aircraft additionally preserve their expendable role and one-charge rack across save/load; mission consumption remains limited to an armed sortie. Damaged aircraft retain hidden faults and require workshop diagnosis. A free compatible part slot or drone locker remains mandatory.

## Persistence

Schema 9 adds broker reputation, access requirements, complete-drone listing type, and rotating-stock metadata. Earlier save schemas remain readable; absent reputation initializes to zero.

## Subjective tuning

The 250/700 reputation thresholds, two-strike-aircraft allocation, 460–480 strike prices, stock counts per day, complete-drone prices, damaged-stock discounts, card density, and thumbnail readability require human play-testing. The implementation does not claim those values are final.
