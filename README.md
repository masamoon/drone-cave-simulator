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

The repository now contains a Unity `6000.4.8f1` Universal Render Pipeline project implemented through Milestone 5.4. Open `Assets/Scenes/SafeHouse.unity` for the current playable workshop: maintain modular drones in a focused PC-building-game style service view, store whole chassis in a three-slot physical locker, trade persistent parts and salvage aircraft through the constrained market, and launch abstract daily sorties from the tactical map. Recon, precision-strike, and armed-search requests resolve while the workshop remains interactive and return or deliberately consume the same aircraft with physical resource use and wear. Sortie rewards, expendable strike aircraft, end-of-day operations, deterministic market refresh, and overnight battery turnaround close the current daily continuity loop. Resolved reports can generate deterministic in-engine after-action reconstructions from the same 2D topography shown on the map. A deterministic pixel atlas and presentation-only low-poly mesh kit provide PSX-level detail, while service-mode component tooltips move fault diagnosis from large text summaries onto the physical drone. The three lab scenes remain regression fixtures.

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
- while holding a part, `E` on a valid storage rack — store it
- while holding a damaged part, `E` twice on salvage — confirm salvage
- `E` on the orange drone control — move a repaired and diagnosed drone to or from the ready shelf
- `E` on an occupied drone-locker control — swap that chassis with the service-bay drone
- `E` on the parts/salvage exchange terminal — open Parts, Salvage Drones, Fleet, and Sell views
- `E` on the tactical map — review daily Recon, Precision Strike, and Armed Search requests
- in the tactical map — accept a request, choose workshop-adjacent or remote-team deployment, assign the ready-shelf drone, and launch
- after launch — continue normal workshop work while the sortie timer and radio status run
- reopen the tactical map after recovery — inspect and acknowledge the deterministic report or end operations
- on a resolved report, `VIEW RECONSTRUCTION` — generate the seeded topography as a temporary in-engine replay
- in a reconstruction, `END RECONSTRUCTION` / `RETURN TO WORKSHOP` — restore the previous workshop camera and controls
- `E` on the cyan service control — enter focused drone service mode
- in service mode, middle-mouse drag — orbit the drone
- in service mode, mouse wheel — zoom
- in service mode, hover a component, socket, screw, or frame — inspect its name and diagnostic condition
- in service mode, `RUN DIAGNOSTIC` — disclose installed component and frame condition through hover cards
- in service mode, left click / hold — drag a stored part, tighten the pointed screw, or operate the highlighted component procedure
- in service mode, right click / hold — loosen the pointed screw
- in service mode, drag a stored part out of the sidebar — transition that same instance into a visible controlled 3D drag
- in service mode, guide the 3D part into a compatible socket — complete a short insertion gesture and seat it deterministically
- in service mode, drag an eligible damaged part to `SALVAGE` — convert it to scrap
- in service mode, `Escape` — cancel the active drag first, otherwise return to first-person workshop mode
- `EXIT SERVICE` — return to first-person workshop mode

See `docs/MILESTONE_04_3_IMPLEMENTATION.md` for the market handoff, `docs/MILESTONE_05_IMPLEMENTATION.md` for missions and daily continuity, `docs/MILESTONE_05_1_IMPLEMENTATION.md` for procedural reconstructions, `docs/MILESTONE_05_2_IMPLEMENTATION.md` for the PSX visual pipeline, and `docs/MILESTONE_05_4.md` plus `docs/MILESTONE_05_4_IMPLEMENTATION.md` for visual diagnosis and component-tooltip validation.
