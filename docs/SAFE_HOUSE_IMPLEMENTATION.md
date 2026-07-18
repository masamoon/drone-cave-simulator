# Safe-house environment slice

## Scope

`SafeHouse` is the first representative game-space scene. It wraps the complete Milestone 3 service drone in a compact concealed workshop while preserving all existing mounting, replacement, diagnostics, audio feedback, and persistence behavior.

The room contains authored spatial landmarks for the workbench, tactical map, radio/comms desk, ready shelf, general shelving, light and radio cutoff panel, utility generator, and a small living corner. A boarded window, concealed reinforced exit, exposed ceiling beams, warm task lighting, cold exterior leakage, rain, and generator ambience establish the intended quiet refuge tone.

The scene starts with the service-repair scenario: eleven mounted components, one damaged rear-left motor, one depleted installed battery, and serviceable replacements in the UI inventory. It uses `under-static-safe-house.json` so its save does not overwrite either Milestone 3 lab.

## Boundaries

- The workbench and complete drone remain fully interactive.
- While a battery is guided at the tray entrance, `E` commits a short authored slide to the seated position; a second `E` closes the latch.
- Opening an installed battery latch now enters an explicit extraction-ready state; the prompt changes to `LATCH OPEN · E: pull battery from tray`, and the next interaction grabs the battery instead of closing the latch again.
- The battery latch is a high-contrast orange, end-hinged lever that swings upward by `105°` when open. Its direct interaction collider is deliberately larger than the visible handle so it remains easy to acquire after seating a replacement.
- The decorative replacement-motor tray is absent from the Safe House. Stored loose parts and returns use hidden runtime slots and are accessed through the tablet/service inventory.
- The obsolete charged/depleted battery presentation trays are absent from the Safe House. A single-bay smart charging dock accepts loose batteries through the same guided service drag as drone servicing, seats them against a keyed electrical connector, and begins charging without a fictional retention latch. Its indicator changes state, and partial charge plus dock occupancy persist through save/load.
- Drone service no longer depends on a small labelled button. An invisible non-blocking interaction envelope surrounds the service-bay aircraft, allowing the player to enter service mode while aiming from any side of the drone; the `SERVICE` world lettering has been removed.
- Safe-house ambient, task, and bounce lighting were raised while retaining the warm work-lamp/cool exterior contrast.
- Authored fastener heads rise as they loosen and hide when motor extraction begins, preventing screws from floating after the motor leaves the socket.
- The surrounding room and furniture use collision and can be walked around.
- Map, radio, and concealment panel remain readable physical stations but intentionally non-interactive placeholders.
- The old first-person parts rack, returns rack, and two-step salvage-bin targets are removed. Service-mode salvage remains the deliberate destructive gesture.
- Floating world-space labels are omitted from the Safe House; interaction prompts and functional screens carry the necessary information.
- The legacy red diagnostic block and orange ready-shelf block are absent. Diagnostics run inside service mode, while service/ready/locker movement is handled by the tablet.
- No exposure simulation, discovery event, or outdoor play is added in this slice.
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
