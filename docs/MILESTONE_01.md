# MILESTONE 01 — Tactile Motor Installation

## 1. Objective

Prove the highest-risk interaction in the project:

> A loose low-poly motor can be picked up, guided into a compatible socket, seated through a deliberate motion, secured using a floating screwdriver, tested, removed, and restored through save/load.

This milestone is an isolated interaction laboratory. It is not a complete drone-building system.

---

## 2. Player experience target

The interaction should feel:

- clear;
- controlled;
- physically suggestive;
- forgiving without being automatic;
- repeatable;
- satisfying through movement, sound, and visible function.

The player does not need precise dexterity.

The game should assist with exact alignment while preserving the meaningful actions:

- bringing the part to the correct location;
- pushing it into position;
- securing the fasteners;
- testing the result.

---

## 3. Required scene

Create or use an isolated scene named:

`InteractionLab`

Suggested hierarchy:

```text
InteractionLab
├── Systems
│   ├── GameBootstrap
│   ├── InteractionSystem
│   └── SaveSystem
├── Player
│   ├── Camera
│   ├── Interactor
│   └── ToolAnchor
├── Workbench
├── DroneArmFixture
│   └── MotorSocket
├── LooseMotor
├── IncompatibleMotor
├── FloatingScrewdriver
├── TestSwitch
├── DiagnosticLamp
├── AudioSourcePool
└── DebugPanel
```

The visual environment can be greybox or simple low-poly primitives.

---

## 4. Required interaction sequence

### 4.1 Highlight

When the player aims at the loose motor within range:

- the motor receives a restrained highlight;
- a contextual prompt is available;
- the player can begin a grab interaction.

### 4.2 Grab

When grabbed:

- the motor moves to a controlled held position;
- position and rotation are smoothed;
- the motor remains stable;
- the player can rotate it;
- the motor cannot clip catastrophically through the workbench;
- dropping it returns it to valid loose-object behaviour.

### 4.3 Socket guidance

When the compatible motor enters the socket capture region:

- the socket recognizes compatibility;
- the motor enters a `Guided` state;
- orientation begins blending toward the socket entry orientation;
- lateral motion is assisted;
- the part does not instantly teleport to the final pose;
- the player can pull away to cancel guidance.

An incompatible motor must not enter guided installation.

### 4.4 Seating

Once orientation is within tolerance:

- movement becomes constrained primarily along the insertion axis;
- the player pushes the motor through a short insertion distance;
- resistance or slowed movement increases near the seated position;
- a detent or contact sound marks full seating;
- the motor enters the `Seated` state.

The motor is not yet installed.

### 4.5 Securing

The seated motor exposes two fastener targets.

The player holds the drive input on a fastener. The floating screwdriver spawns already aligned to that screw.

For each fastener:

- it aligns with the fastener axis;
- player input rotates the tool;
- the screw head or fastener representation visibly rotates;
- fastening progress increases;
- the final 15% has stronger resistance;
- a final torque click marks completion;
- releasing the drive input or completing the action despawns the tool.

When both fasteners are complete:

- the motor enters `Installed`;
- the loose Rigidbody behaviour is disabled;
- the assembly records the installed part instance;
- the motor uses the deterministic installed pose.

### 4.6 Testing

The player activates the test switch.

When a compatible installed motor is present:

- the motor visibly spins for approximately 1.5 seconds;
- the fixture or motor gives a restrained vibration response;
- the diagnostic lamp changes to a success state;
- the motor enters or records `Tested`.

When no installed motor is present:

- the motor does not spin;
- the diagnostic lamp indicates failure or no connection.

### 4.7 Removal

Removal reverses the fastening process:

- hold the drive input on each fastener to spawn the screwdriver;
- loosen both fasteners;
- motor enters `Seated` but unsecured state;
- player grabs and pulls the motor out along the extraction axis;
- the socket becomes vacant;
- the motor returns to loose runtime ownership.

Removal must not create a duplicate part or leave stale assembly references.

---

## 5. Interaction states

At minimum, represent these states explicitly:

```text
Loose
Held
Guided
Seated
Securing
Installed
Tested
Removing
```

State transitions must be validated.

Invalid transitions should be rejected or logged clearly in development builds.

---

## 6. Data requirements

### 6.1 Part definition

Create an immutable part definition containing at least:

```text
ID
Display name
Part category
Compatible socket tags
Prefab reference
Base reliability
Mass
```

Use a ScriptableObject or equivalent immutable asset.

### 6.2 Runtime part instance

Runtime mutable data contains at least:

```text
Unique instance ID
Definition ID
Condition
Current state
Current owner or location
Installed socket ID
Tested flag
```

Do not modify the definition asset to store runtime state.

### 6.3 Socket definition or configuration

The motor socket contains:

```text
Socket ID
Accepted category or tags
Capture radius
Entry orientation
Insertion axis
Insertion distance
Guidance strength
Alignment tolerance
Final installed pose
Required tool
Fastener count
```

---

## 7. Initial tuning values

Use these as seeds, not final values:

| Parameter | Initial value |
|---|---:|
| Interaction range | 1.8 m |
| Held-object distance | 0.65 m |
| Position smoothing | 0.08 s |
| Rotation smoothing | 0.10 s |
| Guidance radius | 0.18 m |
| Guidance blend | 65% |
| Alignment tolerance | 25 degrees |
| Insertion distance | 0.04 m |
| Fastener rotations | 2.5 turns |
| Final torque zone | Last 15% |
| Motor test duration | 1.5 s |

Expose feel values through serialized configuration rather than hard-coding them across scripts.

---

## 8. Required architecture characteristics

- Use explicit interaction states.
- Use deterministic final transforms.
- Do not use uncontrolled physics for final alignment.
- Compatibility must be data-driven.
- Definitions and mutable instances must be separate.
- Tool progress must be reusable for future fastened components.
- The socket should not contain motor-specific logic beyond configuration or narrow interfaces.
- Avoid building speculative systems for batteries, cameras, missions, or full inventory.
- Prefer a small reusable framework over one giant interaction script.
- Do not introduce an external dependency unless essential.

Suggested interfaces or concepts:

```csharp
IInteractable
IGrabbable
IInstallable
IPartSocket
IToolTarget
```

Exact API names may differ if the resulting design remains small, clear, and testable.

---

## 9. Save/load requirements

Persist:

- motor runtime instance ID;
- motor condition;
- installed socket ID;
- installed or loose state;
- fastener completion state;
- tested state.

Required scenarios:

1. Save with motor loose.
2. Save with motor installed.
3. Save with motor tested.
4. Load with the correct socket occupied.
5. Remove after loading without duplication.

Partial transient states such as `Held`, `Guided`, or active tool use must resolve safely.

Preferred rule:

- on save or load, transient states revert to the last stable state: `Loose`, `Seated`, or `Installed`.

Document the chosen behaviour.

---

## 10. Debug panel

Create a small development-only runtime panel showing:

- focused object;
- held object;
- held part instance ID;
- current motor state;
- target socket ID;
- socket occupancy;
- guidance active;
- alignment error;
- insertion progress;
- fastener 1 progress;
- fastener 2 progress;
- tested state.

The panel can be visually plain.

---

## 11. Automated tests

Add Edit Mode or Play Mode tests where practical.

Minimum test coverage:

### Compatibility

- compatible motor accepted;
- incompatible motor rejected;
- occupied socket rejects another motor.

### State transitions

- loose to held;
- held to guided;
- guided cancellation returns to held;
- guided to seated only when aligned and inserted;
- seated to installed only after both fasteners;
- installed to tested after successful test;
- installed can be removed;
- invalid transitions rejected.

### Assembly integrity

- installed part recorded once;
- removal clears socket;
- repeated installation does not duplicate ownership;
- incompatible part cannot update assembly state.

### Persistence

- loose motor survives save/load;
- installed motor survives save/load;
- fastener state survives save/load;
- tested state survives save/load;
- removal after load does not duplicate the part.

Do not attempt to automate subjective tactility.

---

## 12. Manual Play Mode acceptance criteria

The milestone is complete only when all are true:

- Pickup is smooth and stable.
- The motor can be intentionally dropped and recovered.
- Guidance begins close enough to feel contextual.
- Guidance assists without fully taking control.
- The player can cancel by pulling away.
- Insertion direction is visually understandable.
- Seating has a clear contact moment.
- The screwdriver appears aligned only during a fastener action and disappears on release or completion.
- Tool rotation is visible and linked to input.
- The final fastener has an obvious completion click.
- The installed motor remains perfectly stable.
- The motor visibly spins during the test.
- Removal works repeatedly.
- Save/load preserves stable state.
- The full loop can be repeated several times without restarting Play Mode.
- No compilation errors or recurring console exceptions remain.

---

## 13. Explicit exclusions

Do not implement during this milestone:

- complete drone frames;
- four-motor assembly;
- propellers;
- batteries;
- camera modules;
- antennas;
- mission systems;
- workshop risk;
- inventory UI;
- physical parts trays;
- visible hands;
- final environment art;
- direct drone flight;
- realistic individual screw rigid bodies;
- generic crafting recipes;
- NPCs;
- horror events.

---

## 14. Definition of done

The milestone is done when:

1. The interaction sequence works end to end.
2. Required tests pass.
3. Play Mode validation is completed.
4. Save/load works for stable states.
5. Known subjective tuning issues are listed explicitly.
6. Relevant documentation is updated.
7. No Milestone 2 systems have been added.

---

## 15. Kickoff prompt for Codex

Use this prompt from the repository root:

```text
Read AGENTS.md, docs/GAME_SPEC.md, and docs/MILESTONE_01.md before changing files.

Implement only Milestone 1: the tactile motor installation interaction in the InteractionLab scene.

Before implementation:
1. Confirm which repository instructions and documents you loaded.
2. Inspect the Unity project and Unity MCP connection.
3. Report compilation errors or missing prerequisites.
4. Propose the minimal file and scene changes.

Then implement the milestone, add the required tests, validate it in Play Mode through the Unity MCP, and resolve compilation errors and test failures.

Do not implement batteries, propellers, cameras, missions, inventory UI, final art, visible hands, direct drone flight, or any later milestone.

Finish with:
- files changed;
- tests run;
- Play Mode validation performed;
- known limitations;
- subjective tuning parameters that require human feedback.
```
