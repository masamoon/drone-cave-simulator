# Milestone 02 implementation notes

## Scope

This implementation is limited to the reusable component-installation laboratory approved in `MILESTONE_02.md`. It preserves the Milestone 1 motor loop and adds isolated propeller, battery, camera, and antenna stations. It does not add a complete drone, inventory, derived drone statistics, missions, charging, signal simulation, camera feeds, or direct flight.

`InteractionLabFactory` constructs five runtime stations and ten loose parts: one compatible and one deliberately incompatible example per station. The incompatible examples for the four new categories have the correct category but the wrong compatibility tag; the original incompatible motor retains its wrong category and tag.

## Shared framework

`InstallablePart` now owns definition-driven identity, runtime state, physics, highlighting, recovery, and persistence restoration. `MotorPart` remains as a narrow compatibility type for the motor test fixture.

`PartSocket` owns category/tag compatibility, authored insertion guidance, deterministic seating, occupancy, assembly registration, removal, and procedure state. It contains no component-name branches. `InstallationProfile` ScriptableObject assets select one of three securing procedures:

- `TwistLock` for the 60-degree propeller and 90-degree antenna locks;
- `Latch` for the battery tray;
- `Fasteners` for the motor and two-fastener camera bracket.

All procedures use the existing explicit state flow:

`Loose → Held → Guided → Seated → Securing → Installed`

Removal reverses the procedure through `Removing → Seated → Held → Loose`. Installed parts are kinematic, parented to a deterministic authored pose, and recorded once in `DroneAssemblyState`.

## Immutable data

Compatible and incompatible `PartDefinition` assets are checked in under `Assets/UnderStatic/Resources/PartDefinitions`. Propeller, battery, camera, and antenna `InstallationProfile` assets are under `Assets/UnderStatic/Resources/InstallationProfiles`.

These assets contain design data only. Per-instance state and procedure progress remain in `PartRuntimeData` and `SocketRuntimeState`.

## Persistence

The save file is now `under-static-milestone-02.json` under `Application.persistentDataPath`.

Version 2 persists collections of existing runtime parts and sockets, rebuilding occupancy by unique instance ID without instantiating replacements. Loose world poses, stable interaction state, owner, socket ID, condition, tested flag, insertion progress, lock progress, latch state, and fastener progress are retained as applicable.

Transient states normalize as follows:

- `Held` and `Guided` resolve to `Loose`;
- `Securing` resolves to `Seated` while retaining partial procedure progress;
- `Removing` resolves to the last stable installed or tested checkpoint;
- stable states persist directly.

The loader also accepts the Milestone 1 single-part JSON shape and migrates it into the existing motor/socket instances. Unknown instances, definitions, or sockets fail safely.

## Controls

- `WASD` — move;
- mouse — look;
- `E` — pick up/drop, open or close the battery latch, extract an unlocked part, or operate the motor test switch;
- hold left mouse while carrying — rotate a held part;
- hold left mouse while focusing a seated/installed twist-lock part — lock or unlock it;
- `C` — activate or return the floating screwdriver on a focused fastener socket;
- hold left mouse while the screwdriver is aligned — turn the active fastener;
- `1` — save all parts and sockets;
- `2` — load all parts and sockets.

## Automated validation

Validated with Unity `6000.4.8f1`:

- project import and C# compilation complete without project compilation errors;
- all 16 Edit Mode tests pass;
- all 3 Play Mode tests pass;
- the original motor install, test, save/load, and removal regression loop passes;
- the Milestone 2 Play Mode loop builds five sockets and ten parts, completes all five securing procedures, verifies five assembly records, saves the collection, reloads it, and verifies occupancy and identity are preserved;
- no recurring runtime exceptions occurred in the passing runs.

The computer-control service could not attach to the already-open Unity window. Automated Play Mode validation is complete, but the interactions have not received a subjective mouse/keyboard feel pass.

## Human tuning targets

The checked-in installation profiles contain the binding document's initial seeds:

| Component | Capture | Alignment | Insertion | Lock/secure | Final resistance |
|---|---:|---:|---:|---:|---:|
| Propeller | `0.16 m` | `25°` | `0.025 m` | `60°` | final `15°` |
| Battery | `0.22 m` | `18°` | `0.12 m` | explicit latch | final `20%` |
| Camera | `0.16 m` | `20°` | `0.035 m` | 2 fasteners | final `15%` |
| Antenna | `0.12 m` | `15°` | `0.018 m` | `90°` | final `20°` |

Subjective feedback is still required for capture generosity, held-object smoothing, twist speed, perceived resistance, latch readability, screwdriver travel, audio balance, and station spacing.
