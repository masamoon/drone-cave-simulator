# AGENTS.md

## Project identity

**Under Static** is a low-poly, first-person workshop-management game about building and repairing contemporary drones, assigning them to abstracted battlefield support missions, and keeping a concealed workshop undiscovered by a materially superior enemy.

Read these files before making project changes:

1. `docs/GAME_SPEC.md`
2. `docs/MILESTONE_01.md`
3. Any newer milestone document explicitly named in the user prompt

The active milestone document is the binding implementation scope. The larger game specification provides context, not permission to implement future systems.

---

## Engine and project assumptions

- Engine: Unity 6
- Rendering: Universal Render Pipeline
- Language: C#
- Input: Unity Input System
- Perspective: First person
- Art target: Grounded low-poly 3D
- Player hands: Not rendered
- Tools: Floating, visibly animated tools
- Source control: Git
- Supported initial platform: Windows PC
- Do not change the Unity editor version or render pipeline without explicit approval.
- Do not add third-party packages without documenting the need and receiving approval unless the active milestone explicitly requires them.

---

## Product rules

Protect these design decisions:

- The workshop is the primary play space.
- Tactility comes from guided physical actions, sound, resistance, detents, and functional feedback.
- Precision assembly is deterministic and authored, not dependent on uncontrolled physics.
- Loose parts may use simplified Rigidbody behaviour.
- Small parts must never become irretrievable beneath furniture.
- The player performs the meaningful gesture; the game handles exact final alignment.
- No visible hands are required.
- No direct FPV piloting is planned for the MVP.
- Battlefield missions are abstracted simulations presented through maps, reports, and limited feeds.
- The player is outnumbered and influences local outcomes rather than clearing the battlefield.
- Concealment and the sudden horror of discovery are core pillars.
- Contemporary, fielded drone capabilities only; no speculative robot armies or science-fiction systems.

---

## Architecture rules

Prefer small, testable systems with explicit state transitions.

Keep these concepts separate:

- Immutable design definitions
- Mutable runtime condition
- World representation
- Installed assembly state
- Inventory ownership
- Mission-derived statistics
- Visual and audio feedback
- Persistence

Use ScriptableObjects for immutable definitions such as part types, socket compatibility, mission templates, and feedback profiles.

Do not store mutable condition, current ownership, installation state, or campaign progress directly in ScriptableObject assets.

Core interaction states should be explicit. Avoid behaviour that depends on incidental collider order, frame timing, or undocumented MonoBehaviour execution order.

Preferred top-level systems:

- `GameBootstrap`
- `InteractionSystem`
- `WorkbenchSystem`
- `PartSystem`
- `DroneAssemblySystem`
- `ToolSystem`
- `InventorySystem`
- `MissionSystem`
- `WorkshopRiskSystem`
- `SaveSystem`
- `AudioFeedbackSystem`

Only create systems required by the active milestone.

---

## Coding standards

- Use namespaces under `UnderStatic`.
- Keep public APIs narrow.
- Prefer composition over inheritance.
- Use serialized private fields rather than public mutable fields.
- Avoid scene-wide singleton lookups and repeated `FindObjectOfType` calls.
- Avoid hidden static mutable state.
- Validate required references in `Awake` or `OnValidate`.
- Use descriptive names tied to game concepts.
- Add comments for non-obvious intent, not line-by-line narration.
- Keep tunable feel parameters exposed in the Inspector or in dedicated data assets.
- Do not hard-code input keys.
- Do not create placeholder abstractions for speculative future features.

---

## Interaction implementation rules

Tactile assembly should normally follow:

`Idle → Highlighted → Held → Guided → Seated → Securing → Installed → Tested`

Not every object requires every state, but transitions must remain explicit.

For socketed parts:

- Compatibility must be data-driven.
- Guidance begins only inside a configurable capture volume.
- Guidance should assist orientation without instantly teleporting the part.
- Final seating follows an authored insertion axis.
- Final installed transforms are deterministic.
- Installed parts are attached through explicit assembly state, not merely parenting.
- Removal reverses the relevant installation sequence.
- Cancellation must leave the system in a valid state.
- Save/load must resolve partial interactions safely.

---

## Validation requirements

For each implementation task:

1. Inspect the existing project before changing it.
2. State the minimal intended file and scene changes.
3. Implement only the active milestone.
4. Resolve compilation errors.
5. Add Edit Mode or Play Mode tests for state and data logic where practical.
6. Validate the actual interaction in Play Mode.
7. Report:
   - Files changed
   - Tests run
   - Play Mode checks performed
   - Known limitations
   - Tunable parameters requiring subjective feedback
8. Update documentation only where the implementation changed a settled decision.

Do not claim an interaction feels good merely because it compiles. Report objective validation and identify what requires human play-testing.

---

## Prohibited scope unless explicitly requested

Do not implement:

- Direct drone flight
- Visible player arms or hands
- Multiplayer
- Open-world terrain
- Infantry combat simulation
- Detailed ballistics
- Full drone aerodynamics
- Procedural campaign generation
- Complex NPC schedules
- Workshop decoration systems
- Final art production
- A generic physics sandbox
- Free-simulated individual screws
- Additional drone categories beyond the active milestone
- Future milestones while completing the current one
