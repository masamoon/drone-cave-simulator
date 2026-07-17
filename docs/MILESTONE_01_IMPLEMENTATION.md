# Milestone 01 implementation notes

## Scope

This implementation is limited to the isolated motor interaction laboratory described by `MILESTONE_01.md`. It does not add complete drone frames, batteries, propellers, cameras, mission systems, inventory, workshop risk, visible hands, or direct flight.

The checked-in `InteractionLab` scene is intentionally small. `InteractionLabFactory` constructs the required runtime hierarchy, greybox geometry, immutable motor definitions, first-person player, compatible and incompatible motors, socket, two fasteners, floating screwdriver, test switch, diagnostic lamp, audio feedback, persistence system, and development panel when Play Mode begins.

## State and ownership

The motor uses explicit states:

`Loose → Held → Guided → Seated → Securing → Installed → Tested`

Removal uses:

`Installed/Tested → Removing → Seated → Held → Loose`

Invalid transitions are rejected and logged. The socket owns guidance and deterministic final alignment. `DroneAssemblyState` records a part only when all fasteners are complete and clears that record when both fasteners have been loosened. A seated but unsecured motor occupies the socket without being recorded as installed.

Part definitions are immutable `PartDefinition` ScriptableObject assets. Per-instance condition, state, location, socket ID, and tested state live in `PartRuntimeData` and are never written back to those assets.

## Save/load stabilization rule

The save file is `under-static-milestone-01.json` under `Application.persistentDataPath`.

Transient states are normalized as follows:

- `Held` or `Guided` saves as `Loose`.
- `Securing` saves as `Seated`, preserving partial fastener progress.
- `Removing` returns to its last stable installed/tested state, with both fasteners secured.
- `Loose`, `Seated`, `Installed`, and `Tested` persist directly.

Loading clears the socket and assembly registry before restoring the existing runtime part instance. It never creates a replacement part, preventing duplicate ownership after a load/removal cycle.

## Automated coverage

`Assets/UnderStatic/Tests/EditMode/MotorInteractionTests.cs` covers:

- compatible, incompatible, and occupied socket behaviour;
- valid and invalid state transitions;
- guidance cancellation and seating tolerances;
- installed ownership recorded only once;
- complete removal and socket clearing;
- testing state;
- loose, installed, tested, and partial-fastener persistence;
- removal after load without duplication.

## Validation status

Validated with Unity `6000.4.8f1`:

- project import and C# compilation complete with no project compilation errors;
- Unity Input System updated to `1.17.0` for Unity 6.4 compatibility;
- all 11 Edit Mode tests pass;
- both Play Mode tests pass;
- the Play Mode suite loads `InteractionLab`, verifies its required runtime hierarchy, completes deterministic seating and both fasteners, runs the visible 1.5-second motor test, saves and reloads the tested state, loosens both fasteners, removes the same runtime part instance, and verifies socket/assembly cleanup;
- no recurring runtime exceptions or project error logs occurred during the passing test runs.

The computer-use UI service could not connect and no Unity MCP bridge was exposed, so subjective mouse/keyboard feel validation remains a human play-test. Automated Play Mode validation is complete.

## Human tuning targets

These serialized seeds require subjective play feedback:

- interaction range: `1.8 m`;
- held distance: `0.65 m`;
- position smoothing: `0.08 s`;
- rotation smoothing: `0.10 s`;
- capture radius: `0.18 m`;
- guidance strength: `65%`;
- alignment tolerance: `25°`;
- insertion distance: `0.04 m`;
- fastener travel: `2.5 turns`;
- final torque zone: last `15%`;
- motor test duration: `1.5 s`;
- tool travel speed, vibration amplitude, feedback volume, and procedural tone pitch.
