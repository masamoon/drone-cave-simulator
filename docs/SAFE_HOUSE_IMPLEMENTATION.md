# Safe-house environment slice

## Scope

`SafeHouse` is the first representative game-space scene. It wraps the complete Milestone 3 service drone in a compact concealed workshop while preserving all existing mounting, replacement, diagnostics, audio feedback, and persistence behavior.

The room contains authored spatial landmarks for the workbench, tactical map, radio/comms desk, ready shelf, parts/returns storage, light and radio cutoff panel, utility generator, and a small living corner. A boarded window, concealed reinforced exit, exposed ceiling beams, warm task lighting, cold exterior leakage, rain, and generator ambience establish the intended quiet refuge tone.

The scene starts with the service-repair scenario: eleven mounted components, one damaged rear-left motor, one depleted installed battery, and serviceable replacements on the bench. It uses `under-static-safe-house.json` so its save does not overwrite either Milestone 3 lab.

## Boundaries

- The workbench and complete drone remain fully interactive.
- While a battery is guided at the tray entrance, `E` commits a short authored slide to the seated position; a second `E` closes the latch.
- Opening an installed battery latch now enters an explicit extraction-ready state; the prompt changes to `LATCH OPEN · E: pull battery from tray`, and the next interaction grabs the battery instead of closing the latch again.
- The battery latch is a high-contrast orange, end-hinged lever that swings upward by `105°` when open, making its mechanical state readable from the player camera.
- The serviceable replacement motor now sits in its own green `REPLACEMENT MOTOR · 97%` tray at the front-left of the workbench.
- Safe-house ambient, task, and bounce lighting were raised while retaining the warm work-lamp/cool exterior contrast.
- Authored fastener heads rise as they loosen and hide when motor extraction begins, preventing screws from floating after the motor leaves the socket.
- The surrounding room and furniture use collision and can be walked around.
- Map, radio, and concealment panel remain readable physical stations but intentionally non-interactive placeholders.
- Milestone 4 makes the ready shelf, parts rack, returns rack, and salvage bin interactive through a separate physical inventory layer; see `MILESTONE_04_IMPLEMENTATION.md`.
- No charging, mission selection, exposure simulation, discovery event, or outdoor play is added in this slice.
- Geometry and materials are grounded low-poly greybox art, not final environment production.

## Validation targets

- the room builds only in the `SafeHouse` scene;
- the Milestone 3 repair assembly remains `11/11` with the two intended service faults;
- the player starts inside the clear central aisle and can reach each station;
- walls, furniture, ceiling, and exit retain collision;
- rain and generator loops start with the room;
- all prior Edit Mode and Play Mode tests remain green.

## Validation completed

- `21/21` Edit Mode tests passed.
- `13/13` Play Mode tests passed, including the safe-house hierarchy, service-drone state, separate save, active ambience, guided battery slide/latch flow, and fastener visibility during motor extraction.
- The room was traversed in Play Mode with normal WASD input from the entrance aisle to the bench, map side, storage side, and concealed exit.
- The player remained grounded and inside the authored collision shell throughout the walk.
- The service drone remained `11/11` mounted with the intended damaged motor and depleted battery faults.
- A clean safe-house Play Mode boot produced zero console errors.
- Final views are stored at `Assets/Screenshots/safe-house-service-bay-final.png` and `Assets/Screenshots/safe-house-entry-final.png`.
