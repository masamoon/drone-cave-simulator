# MILESTONE 09 — Discovery and Game Over

## 1. Goal

Convert accumulated workshop exposure into one readable authored crisis with a complete terminal failure flow:

`Warning signs → discovery incident → final prevention action → safe resolution or game over`

The loss should feel like the workshop has been discovered, not like an abstract meter reached zero.

## 2. Binding scope

- Use `WorkshopRiskSystem` as the sole authority for exposure and discovery readiness.
- Trigger one authored incident when the existing discovery condition is reached; crossing the numeric threshold
  alone does not immediately display a game-over screen.
- Telegraph the incident through escalating diegetic audio, radio, lighting, and equipment feedback.
- Give the player one clear, time-bounded prevention procedure centered on shutting down the transmitter and
  darkening the relevant powered equipment.
- Suspend launches, service actions, market use, and other conflicting interactions while the incident is active.
- Resolve successful prevention back into a valid uneasy workshop state without resetting accumulated campaign
  ownership or inventing passive exposure decay.
- Resolve failure into an explicit terminal runtime state, then present a game-over screen with the cause and
  options to start a new run or return to the main menu.
- Prevent ordinary save/load from bypassing, duplicating, or soft-locking the incident. Document whether an active
  incident resumes or restores to its last safe authored checkpoint.
- Preserve deterministic cleanup of active sorties, held parts, service mode, field excursions, and modal UI when
  entering either incident resolution.

## 3. Initial failure condition

The first supported game-over cause is discovery of the current hideout after the player fails the authored
prevention procedure. Bankruptcy, aircraft depletion, missed profits, and individual sortie failure are not game
over conditions.

## 4. Player-facing requirements

- The warning is understandable without exposing an exact risk number.
- The required controls remain readable under pressure.
- Failure cannot occur before the player has control and a fair opportunity to respond.
- Success and failure produce clearly different audiovisual resolution.
- Game-over input cannot accidentally activate workshop interactions behind the screen.

## 5. Validation

- Add automated tests for trigger gating, single activation, prevention success, terminal failure, interaction
  suspension, and persistence policy.
- Validate both branches repeatedly in Play Mode, including while a sortie, service interaction, or modal terminal
  was active before the incident.
- Perform final Game View visual QA for warning, response, successful resolution, and game-over states, inspecting
  framing, clipping, occlusion, scale, readability, material consistency, and state continuity.
- Complete an audio pass with no missing, looping, or prematurely cut incident cues.

## 6. Excluded

- Combat, enemy NPCs, player injury, playable evacuation, multiple discovery scenarios, and a final victory ending.
- Generic wave defense or a conventional health bar.
- Hideout relocation and multiple hideouts.
- Hideout upgrades and economy retuning beyond integration fixes.

## 7. Definition of done

The milestone is complete when exposure can produce exactly one fair authored incident, the player can clearly
prevent or fail it, failure reaches a stable game-over state, save/load cannot exploit or corrupt the sequence,
automated tests pass, and both branches pass Play Mode and visual QA.
