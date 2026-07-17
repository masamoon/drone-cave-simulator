# Milestone 03 implementation notes

## Scope delivered

`DroneAssemblyLab` is a separate regression-safe scene from the Milestone 2 station lab. It creates one complete low-poly workshop quadcopter with four motor sockets, four propeller locks, a battery tray, camera bracket, and antenna connector. Eleven mounted parts share one `DroneAssemblyState`; a serviceable motor and charged battery are loose replacements.

The initial drone is complete but unready. Its rear-left motor has `18%` condition and its installed battery has `0%` charge. All other required components are serviceable. The status panel exposes the complete assembly, derived statistics, maintenance faults, persistence status, and diagnostic result.

The battery-service readability pass adds color-coded charged and depleted trays, visible battery rails, connector, and latch, plus charge and condition details in focused-part prompts and the status panel. Propellers now have larger silhouettes, visible motor shafts, and a lower authored position that visually connects each rotor assembly to its motor. Frame arms now run directly from the center plate to all four motors, and camera, antenna, propeller, and battery materials are visually distinct. The floating screwdriver keeps its authored fastener alignment while its shaft and asymmetric driver bit spin around the local drive axis.

## Mounting and replacement

All mounting and removal continues through Milestone 2's explicit interaction states and deterministic socket poses. The rear-left propeller is configured as a removal blocker for its motor. Once a twist-lock component reaches its terminal detent, continued held input no longer reverses direction; release starts the next lock or unlock gesture. Pressing `E` on an occupied unlocked socket now extracts its seated component, removing dependence on overlapping part/socket collider order.

The validated repair sequence is:

1. unlock, extract, and set aside the rear-left propeller;
2. loosen and extract the damaged rear-left motor;
3. guide and fasten the serviceable motor;
4. guide and relock the propeller;
5. open the battery latch and extract the depleted battery;
6. guide and latch the charged battery;
7. run the complete-drone diagnostic.

## Scratch build and teardown slice

`DroneBuildLab` preserves the repair scenario and adds a separate empty-frame exercise. It starts with exactly eleven loose serviceable parts in labeled motor, propeller, and electronics kits; every drone socket is vacant. The scene uses its own `under-static-milestone-03-scratch-build.json` save so it cannot overwrite the replacement scenario.

The player builds four fastened motors, four twist-locked propellers, one latched charged battery, one fastened camera, and one keyed antenna. A propeller socket rejects guidance until its matching motor is fully installed. The mounted propeller then blocks motor removal, enforcing the reverse teardown order. Motor shafts are children of the motor parts, so they leave the frame with the motors instead of remaining as floating geometry.

The status panel identifies the scene as `SCRATCH BUILD / TEARDOWN`, reports live mounted count and missing categories, and retains the focused part's condition or charge. Complete assembly produces `11/11` and a ready diagnostic. Reversing every authored procedure returns the same eleven instances to loose ownership with `0/11` occupied.

## Runtime data and readiness

`PartRuntimeData` now persists normalized battery charge alongside condition. Immutable part definitions expose power draw and capability seeds without storing runtime state.

`DroneAssemblyState` retains installed identity and runtime references, required category counts, and a derived snapshot containing completeness, condition, reliability, endurance, observation quality, control reliability, maintenance summary, and mission readiness. Readiness requires all eleven required components, no part below `45%` condition, and a battery above the depleted threshold.

## Audio

`AudioFeedbackSystem` synthesizes and caches short layered clips at runtime for pickup, drop, guidance capture/cancel, contact, twist detents, ratchet movement, torque clicks, latch movement, extraction, and diagnostic success/failure. Small pitch variation prevents exact repetition. No third-party package or external audio asset was added.

## Persistence

Milestone 3 saves use `under-static-milestone-03.json` and schema version 3. Existing collection restore behavior now includes condition and charge automatically through `PartRuntimeData`. Milestone 1 and 2 compatibility paths remain.

## Validation

Validated with Unity `6000.4.8f1`:

- 21/21 Edit Mode tests passed;
- 10/10 Play Mode tests passed;
- the new lab built 13 physical parts and 11 sockets;
- initial assembly reported `11/11`, `ready=false`, and `0%` endurance;
- the complete replacement sequence was performed in live Play Mode through normal `E`, `C`, and held-left-mouse input;
- final assembly reported `11/11`, `ready=true`, `96%` endurance, and all required systems serviceable;
- the focused battery-only slice was replayed through normal player input: unlatch, extract, place in the depleted tray, pick up the charged pack, guide, seat, latch, and diagnose;
- battery replacement alone moved endurance from `0%` to `96%` and reduced maintenance to the intentionally unresolved damaged-motor fault;
- final battery-service screenshots are stored at `Assets/Screenshots/battery-swap-before-final.png` and `Assets/Screenshots/battery-swap-after-final.png`;
- the scratch-build scene was completed and fully stripped through normal player input, reporting `0/11 → 11/11 ready=true → 0/11` with all eleven original instances loose at the end;
- scratch-build screenshots are stored at `Assets/Screenshots/scratch-build-empty.png`, `Assets/Screenshots/scratch-build-complete.png`, and `Assets/Screenshots/scratch-teardown-final-ui.png`;
- front and side-access rear motor fasteners were driven through normal `C` and held-left-mouse input; both tool root and drive-axis error remained `0.0000°` while the bit visibly rotated and the motors reached `Installed`;
- the screwdriver drive screenshot is stored at `Assets/Screenshots/screwdriver-axial-drive.png`;
- Milestone 1 motor and Milestone 2 component regression tests remained green.

## Known limitations and tuning targets

- Charging is intentionally absent; depleted batteries must be replaced.
- Removed components remain loose workbench objects; storage ownership arrives in Milestone 4.
- Derived statistics are normalized workshop-readiness signals, not mission balance values yet.
- Low-poly shapes and colors are functional greybox art.
- Human tuning is still required for screwdriver duration, twist speed, guidance capture, extraction distance, audio mix, detent density, status-panel readability, and full-frame camera access around rear components.
- Rear motor, propeller, antenna, and diagnostic interactions are reachable from the sides; their final walk-around clearance and camera framing require human comfort tuning.
