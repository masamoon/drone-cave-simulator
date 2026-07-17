# MILESTONE 02 — Reusable Component Installation

## 1. Objective

Generalize the proven Milestone 1 motor interaction into a small reusable component-installation framework, then prove it with four additional contemporary drone components:

- propeller;
- battery;
- camera;
- antenna.

Each component must use the shared part, socket, state, ownership, feedback, and persistence contracts while retaining a distinct authored installation gesture.

This milestone proves framework reuse. It does not create a complete drone, inventory system, repair economy, derived drone statistics, or mission gameplay.

---

## 2. Preconditions

Before implementation:

- Milestone 1 must continue to compile and pass all automated tests.
- The complete Milestone 1 motor install, test, save/load, and removal loop must remain functional.
- Unity 6 URP and Input System configuration must not be replaced.
- No third-party dependency may be added.

The implementation may refactor Milestone 1 code where required for reuse, but must preserve its behavior and serialized save compatibility where practical.

---

## 3. Player experience target

The four interactions should share a readable vocabulary without feeling identical:

- the propeller is placed over a shaft, pressed down, and twist-locked;
- the battery slides through a guided tray and closes a deterministic latch;
- the camera seats in a bracket and is secured by two screwdriver fasteners;
- the antenna aligns to a keyed connector, seats, and receives a short locking turn.

All interactions remain:

- deterministic at final alignment;
- forgiving near the correct socket;
- reversible without duplication;
- visually distinct;
- short enough to repeat during workshop play;
- free of simulated loose screws or tiny uncontrolled rigid bodies.

---

## 4. Required scene

Expand `InteractionLab` with four isolated fixture stations while retaining the Milestone 1 motor station.

Suggested runtime hierarchy:

```text
InteractionLab
├── Systems
│   ├── GameBootstrap
│   ├── InteractionSystem
│   ├── AssemblySystem
│   ├── SaveSystem
│   └── AudioFeedbackSystem
├── Player
│   ├── Camera
│   ├── Interactor
│   └── ToolAnchor
├── Workbench
├── MotorStation
├── PropellerStation
│   ├── PropellerSocket
│   ├── LoosePropeller
│   └── IncompatiblePropeller
├── BatteryStation
│   ├── BatteryTraySocket
│   ├── LooseBattery
│   └── IncompatibleBattery
├── CameraStation
│   ├── CameraBracketSocket
│   ├── LooseCamera
│   └── IncompatibleCamera
├── AntennaStation
│   ├── AntennaConnectorSocket
│   ├── LooseAntenna
│   └── IncompatibleAntenna
├── FloatingScrewdriver
├── DiagnosticPanel
└── DebugPanel
```

Stations may use low-poly primitives and muted greybox materials. Final art is excluded.

---

## 5. Shared interaction framework

### 5.1 Generic part runtime

Replace motor-only runtime assumptions with a generic installable-part contract containing:

```text
Unique instance ID
Definition ID
Condition
Current interaction state
Last stable state
Current owner or location
Installed socket ID
Tested flag where applicable
```

Motor-specific visible behavior may remain in a narrow motor component, but ownership, transitions, compatibility, and persistence must not require a motor type.

### 5.2 Generic socket

A reusable socket must own:

```text
Socket ID
Accepted categories
Accepted compatibility tags
Capture volume
Entry pose
Insertion axis
Insertion distance
Guidance strength
Alignment tolerance
Final installed pose
Installation procedure
Required tool where applicable
Fastener targets where applicable
Occupied runtime part reference
```

The socket must not branch on concrete component names such as `Battery`, `Camera`, or `Antenna`.

### 5.3 Installation procedures

Component-specific actions should be represented by small authored procedures or profiles rather than one large conditional interaction script.

Required procedure types:

- axial insertion;
- axial insertion plus twist lock;
- axial insertion plus deterministic latch;
- screwdriver fasteners.

Procedures share compatibility, state, assembly, cancellation, removal, feedback, and persistence infrastructure.

### 5.4 Compatibility

Compatibility remains definition-driven.

- A correct category with the wrong compatibility tag must be rejected.
- An incorrect category must be rejected.
- An occupied socket must reject another part.
- Rejected parts remain valid loose or held objects.
- Compatibility checks must not mutate assembly state.

---

## 6. Propeller interaction

### 6.1 Installation

The propeller socket is a short motor-shaft fixture.

1. Pick up the compatible propeller.
2. Enter guidance near the shaft.
3. Align the keyed hub.
4. Press the propeller down approximately `0.025 m`.
5. Hold the rotate gesture and turn approximately `60°` in the locking direction.
6. Increasing resistance applies during the final `15°`.
7. A detent marks `Installed`.

The final pose is deterministic. The propeller must not spin freely as a Rigidbody while installed.

### 6.2 Removal

1. Grab or activate the installed propeller.
2. Turn it approximately `60°` in the unlocking direction.
3. Pull along the extraction axis.
4. Return it to `Loose` ownership.

No wrench, loose nut, or simulated thread is required.

---

## 7. Battery interaction

### 7.1 Installation

The battery uses a shallow tray fixture.

1. Pick up the compatible battery.
2. Guidance aligns the long battery body with the tray rails.
3. Lateral movement is constrained after alignment.
4. Push the battery approximately `0.12 m` along the tray.
5. Resistance increases near the connector.
6. Full insertion produces a connector contact sound and `Seated` state.
7. A short explicit interact gesture closes the visible latch and enters `Installed`.

The latch is authored animation or transform movement, not a physics hinge. An empty tray's latch may be
closed and reopened, but battery guidance and insertion begin only while the latch is open. Closing the
latch installs a battery only after that battery has reached the `Seated` state.
The authored seated pose rests the battery on the tray surface between the rails; it must not intersect the
tray base or drone body.

### 7.2 Removal

1. Activate the latch to open it.
2. Enter `Removing`.
3. Grab and pull the battery along the tray axis.
4. Clear socket and assembly ownership when extraction completes.

Battery charge, heat, degradation, and charging are excluded from this milestone. Only generic condition persists.

---

## 8. Camera interaction

### 8.1 Installation

The camera uses a keyed bracket with two screwdriver fasteners.

1. Pick up the compatible camera.
2. Guidance aligns the lens housing and bracket rails.
3. Push approximately `0.035 m` to `Seated`.
4. Hold the drive input on a fastener to spawn the floating screwdriver at that screw.
5. Complete two visible fasteners using the reusable fastener procedure.
6. Enter `Installed` only when both fasteners are complete.

The screwdriver is inactive outside a fastener action. It spawns aligned to the selected screw and despawns
when the player releases the drive input or completes the action.

### 8.2 Removal

1. Hold the drive input on each fastener to spawn the screwdriver.
2. Loosen both fasteners.
3. Return to unsecured `Seated`.
4. Grab and extract the camera along the authored axis.

Camera feeds, image quality statistics, stabilization, and damage visuals are excluded.

---

## 9. Antenna interaction

### 9.1 Installation

The antenna uses a keyed connector fixture.

1. Pick up the compatible antenna.
2. Guidance aligns the connector axis and keyed orientation.
3. Push approximately `0.018 m` to contact.
4. Hold the rotate gesture and turn approximately `90°`.
5. The last `20°` receives stronger resistance.
6. A short click marks `Installed`.

The interaction represents a simplified contemporary locking connector. It must not simulate fine threads.

### 9.2 Removal

1. Turn approximately `90°` in the unlocking direction.
2. Pull along the connector axis.
3. Clear the socket and return the antenna to loose ownership.

Signal strength, radio bands, transmission risk, and mission modifiers are excluded.

---

## 10. Data requirements

### 10.1 Part definitions

Add immutable definitions for compatible and incompatible examples of:

- propeller;
- battery;
- camera;
- antenna.

Definitions contain only design data:

```text
ID
Display name
Category
Compatibility tags
Prefab reference where available
Base reliability
Mass
```

### 10.2 Installation profile

Create immutable installation configuration containing:

```text
Procedure type
Capture radius
Guidance strength
Alignment tolerance
Insertion distance
Insertion resistance curve or scalar
Required locking rotation
Final resistance zone
Fastener count
Required tool ID
Feedback identifiers
```

Mutable progress must not be stored in the profile asset.

### 10.3 Runtime procedure state

Persist only the mutable values required by the active procedure:

```text
Insertion progress
Lock rotation progress
Latch closed state
Fastener progress array
Last stable interaction state
```

---

## 11. Initial tuning values

| Parameter | Propeller | Battery | Camera | Antenna |
|---|---:|---:|---:|---:|
| Capture radius | 0.16 m | 0.22 m | 0.16 m | 0.12 m |
| Alignment tolerance | 25° | 18° | 20° | 15° |
| Insertion distance | 0.025 m | 0.12 m | 0.035 m | 0.018 m |
| Guidance blend | 65% | 70% | 65% | 72% |
| Lock rotation | 60° | latch | fasteners | 90° |
| Final resistance zone | 15° | last 20% | last 15% | 20° |
| Fasteners | 0 | 0 | 2 | 0 |

These values are serialized seeds requiring human feel feedback.

---

## 12. Persistence

Upgrade persistence from one motor/socket pair to a collection of runtime parts and socket records.

Persist per part:

- instance ID;
- definition ID;
- condition;
- stable state;
- owner/location;
- installed socket ID;
- tested flag where applicable;
- stable world pose when loose.

Persist per socket:

- socket ID;
- occupied part instance ID;
- insertion/lock/latch/fastener stable progress required by its procedure.

Required behavior:

- transient `Held` and `Guided` parts resolve to `Loose`;
- active securing resolves to the last valid `Seated` or `Installed` checkpoint;
- active removal resolves to the last stable `Installed` or `Seated` checkpoint;
- loading rebuilds occupancy by unique instance ID;
- loading never instantiates a duplicate for an existing runtime part;
- missing definitions or sockets are rejected with a clear development error;
- Milestone 1 saves either load through a versioned migration or fail safely with an explicit unsupported-version message.

---

## 13. Debug panel

Extend the development panel to show the focused or selected station:

- focused object;
- held part name and instance ID;
- part category and state;
- target socket ID and procedure type;
- compatibility result;
- socket occupancy;
- guidance active;
- alignment error;
- insertion progress;
- lock rotation progress;
- latch state;
- each fastener progress;
- current owner/location;
- last persistence status.

The panel may remain visually plain.

---

## 14. Automated tests

### 14.1 Regression

- all Milestone 1 Edit Mode and Play Mode tests continue to pass;
- motor install, test, persistence, and removal remain unchanged from the player perspective.

### 14.2 Generic compatibility and ownership

- accepted category and tag succeed;
- wrong category fails;
- wrong tag fails;
- occupied socket rejects another compatible part;
- rejected parts do not mutate assembly ownership;
- repeated installation records one instance only;
- removal clears occupancy and ownership exactly once.

### 14.3 Procedure tests

- propeller cannot install before insertion and required rotation;
- propeller unlock and extraction restore loose state;
- empty battery latch can close and reopen, while battery insertion requires it to be open;
- a battery cannot enter `Installed` through the latch before full insertion;
- battery unlatch and extraction clear ownership;
- camera cannot install until both fasteners complete;
- camera fastener progress survives persistence;
- antenna cannot install before insertion and required rotation;
- antenna unlock and extraction restore loose state;
- cancellation from guidance returns to held without stale socket references.

### 14.4 Persistence tests

- all four new compatible parts survive loose save/load;
- all four installed states survive save/load;
- each procedure's stable progress survives save/load;
- multiple occupied sockets restore to the correct unique instances;
- removal after load does not duplicate any part;
- transient states normalize according to Section 12.

### 14.5 Play Mode tests

- the expanded lab creates all required stations and parts;
- each compatible part completes its full install/remove loop;
- incompatible examples are rejected at each station;
- save/load with all five compatible parts installed restores every socket;
- no recurring console exceptions occur.

---

## 15. Manual Play Mode acceptance criteria

- Milestone 1 motor behavior remains functional.
- Each component is visually distinguishable at workshop distance.
- Guidance begins only near the correct fixture.
- Wrong parts never begin guidance.
- Propeller and antenna locking rotations are visible and reversible.
- Battery insertion follows the tray without lateral jitter.
- Battery latch motion clearly changes installed state.
- Camera fastener interaction reuses the floating screwdriver cleanly.
- Installed parts remain perfectly stable.
- Every removal can be repeated without restarting Play Mode.
- Saving and loading several installed parts preserves identity and occupancy.
- Loose parts recover if dropped from the workbench.
- No compilation errors or recurring console exceptions remain.

Subjective feel must be reported separately from objective test completion.

---

## 16. Explicit exclusions

Do not implement during this milestone:

- a complete four-motor drone frame;
- derived drone statistics;
- battery charging, charge level, heat, or safety simulation;
- propeller aerodynamics or motor thrust;
- camera feeds or observation quality;
- antenna signal or radio simulation;
- inventory UI or storage trays;
- salvage, repair, or cannibalization;
- missions or deployment;
- workshop exposure or discovery events;
- visible hands;
- additional tools;
- final environment or component art;
- later milestones.

---

## 17. Definition of done

Milestone 2 is complete when:

1. The Milestone 1 loop still passes regression tests.
2. Shared part, socket, procedure, ownership, feedback, and persistence systems no longer depend on a motor type.
3. Propeller, battery, camera, and antenna interactions work end to end.
4. Compatible and incompatible examples behave correctly.
5. Multi-part save/load preserves stable states and unique identity.
6. Required Edit Mode and Play Mode tests pass.
7. Objective Play Mode validation is recorded.
8. Subjective tuning items are listed for human feedback.
9. No Milestone 3 systems are added.

---

## 18. Kickoff prompt

```text
Read AGENTS.md, docs/GAME_SPEC.md, docs/MILESTONE_01.md,
docs/MILESTONE_01_IMPLEMENTATION.md, and docs/MILESTONE_02.md before changing files.

Implement only Milestone 2: generalize the proven motor interaction framework and add
the isolated propeller, battery, camera, and antenna interaction stations.

Preserve all Milestone 1 behavior and tests. Do not implement full drone statistics,
inventory, charging, camera feeds, signal simulation, missions, workshop risk, final art,
visible hands, direct flight, or later milestones.

Finish with files changed, tests run, Play Mode validation, known limitations, and
subjective tuning parameters requiring human feedback.
```
