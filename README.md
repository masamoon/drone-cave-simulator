# Under Static — Codex Handoff

This package contains the minimum design and implementation documentation needed to begin the first Unity prototype for **Under Static**.

## Included files

```text
AGENTS.md
docs/GAME_SPEC.md
docs/MILESTONE_01.md
README.md
.gitignore
```

## Intended use

1. Create or open a Unity 6 URP project.
2. Place these files in the repository root, not inside `Assets/`.
3. Keep the Unity project in the same Git repository.
4. Confirm the project opens and enters Play Mode without errors.
5. Install and configure one Unity MCP bridge.
6. Open the repository root in Codex.
7. Give Codex the kickoff prompt at the end of `docs/MILESTONE_01.md`.

Suggested repository layout:

```text
UnderStatic/
├── AGENTS.md
├── README.md
├── .gitignore
├── docs/
│   ├── GAME_SPEC.md
│   └── MILESTONE_01.md
├── Assets/
├── Packages/
└── ProjectSettings/
```

If your Unity project currently lives inside a subdirectory, keep `AGENTS.md` and `docs/` at the folder opened in Codex, and state the Unity project path inside `AGENTS.md`.

## Before Codex writes code

Verify manually:

- Unity 6 project opens successfully.
- Universal Render Pipeline is configured.
- Unity Input System is installed and active.
- Git is initialized.
- Unity MCP can create or inspect a GameObject.
- Codex has access to the repository root.
- The current repository state is committed.

## Recommended first branch

```bash
git checkout -b prototype/motor-interaction
```

## Recommended first commit

```bash
git add AGENTS.md README.md .gitignore docs
git commit -m "Add Under Static design and milestone documentation"
```

## Important workflow rule

Do not ask Codex to build the complete game.

Use one bounded milestone at a time:

1. Commit the current stable state.
2. Update or add one milestone document.
3. Ask Codex to implement only that milestone.
4. Test the interaction personally.
5. Give concrete feel feedback.
6. Iterate until acceptable.
7. Commit before beginning the next milestone.

Useful tactile feedback is specific:

- “Guidance begins too far from the socket.”
- “The held motor feels rigidly glued to the camera.”
- “Insertion has no noticeable resistance.”
- “The screwdriver reaches the screw too abruptly.”
- “The final torque click occurs before the visible rotation stops.”
- “Removing the motor takes too long.”
- “The motor test needs a stronger vibration and clearer sound.”

Avoid vague feedback such as “make it more satisfying.”

## Project premise

Under Static is a low-poly first-person workshop-management game about physically maintaining contemporary drones, assigning them to abstracted support missions, and protecting a hidden command post from discovery.

The first milestone intentionally implements only one motor, one socket, one floating screwdriver, and one test fixture.

## Prototype status

The repository now contains a Unity `6000.4.8f1` Universal Render Pipeline project implemented through Milestone 6.1. Open `Assets/Scenes/SafeHouse.unity` for the current playable workshop: maintain modular drones, store whole chassis, trade persistent parts and salvage aircraft, and launch abstract daily sorties from the tactical map. Returning reusable sorties now create deterministic localized maintenance, while workshop launches, powered transmissions, motor tests, and repeated routes build persistent qualitative exposure. The physical transmitter can be silenced at the cost of lost-link recall or delayed strike confirmation. One authored remote cache supports short first-person setup, aircraft recovery, and limited-capacity salvage excursions while preserving the same drone identity across the complete workshop–field cycle. Active sorties expose an optional degraded first-person feed during final approach; resolved reports remain the authoritative outcome record. The three lab scenes remain regression fixtures.

Controls use the checked-in Input System action asset:

- `WASD` — move
- mouse — look
- `E` — pick up, drop, pull out, operate the battery latch, or operate the test switch
- hold left mouse while carrying — rotate a part
- hold left mouse while focusing a twist-lock part — lock or unlock it
- `C` — activate or return the floating screwdriver for a focused fastener socket
- hold left mouse while the screwdriver is aligned — turn the active fastener
- `1` — save all part and socket state
- `2` — load all part and socket state
- `E` on an occupied drone-locker control — swap that chassis with the service-bay drone
- `Tab` — open or close the fleet tablet; review thumbnail cards for the service bay, ready shelf, and locker, and move drones between those physical locations
- `E` on the parts/salvage exchange terminal — open Parts, Salvage Drones, Fleet, and Sell views
- `E` on the tactical map — review daily Recon, Precision Strike, and Armed Search requests
- in the tactical map — accept a request, choose workshop-adjacent or remote-team deployment, assign the ready-shelf drone, and launch
- after launch — continue normal workshop work while the sortie timer and radio status run
- `E` on the radio transmitter — power the workshop link on or off
- tactical map during Recon or a pre-release Grenade Drop — recall while the link is powered
- tactical map remote controls — plan deployment, retrieve a cached aircraft, or recover a salvage cache
- reopen the tactical map after recovery — inspect and acknowledge the deterministic report or end operations
- during an active sortie's final approach — reopen the tactical map and select `OPEN DEGRADED LIVE FEED`
- in the live feed, `LEAVE LIVE FEED` / `RETURN TO WORKSHOP` — restore the previous workshop camera and controls
- `E` while aiming anywhere around the service-bay drone — enter focused drone service mode
- `E` on the workbench battery charger — open its focused service view; drag a spent battery into the keyed charging dock and lift it out after charging
- in service mode, middle-mouse drag — orbit the drone
- in service mode, mouse wheel — zoom
- in service mode, left click / hold — drag a stored part, tighten the pointed screw, or operate the highlighted component procedure
- in service mode, right click / hold — loosen the pointed screw
- in service mode, drag a stored part out of the sidebar — transition that same instance into a visible controlled 3D drag
- in service mode, guide the 3D part into a compatible socket — complete a short insertion gesture and seat it deterministically
- in service mode, drag an eligible damaged part to `SALVAGE` — convert it to scrap
- in service mode, `Escape` — cancel the active drag first, otherwise return to first-person workshop mode
- `EXIT SERVICE` — return to first-person workshop mode

See the milestone documents in `docs/`. The current handoffs are `MILESTONE_05_5_IMPLEMENTATION.md`, `MILESTONE_06_IMPLEMENTATION.md`, and `MILESTONE_06_1_IMPLEMENTATION.md`.
