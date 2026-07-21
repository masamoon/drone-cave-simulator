# MILESTONE 13 — Sleep and Day Transition

## 1. Goal

Make the end of an operational day a physical return to the hideout rather than another terminal command:

`Finish operations → walk to the cot → choose to sleep → fade through the night → wake into the next day`

Sleeping should reinforce the workshop as a lived-in refuge, provide a moment of decompression, and clearly
communicate the consequences of time advancing.

## 2. Binding flow

1. The cot is a readable interactable in the Safe House living corner.
2. While a sortie, field excursion, discovery incident, service procedure, or other blocking operation is active,
   the cot explains why sleep is unavailable without advancing state.
3. When sleep is available, interacting with the cot opens a concise confirmation showing the day that will end
   and the major overnight consequences.
4. Confirmation commits the transition once, suspends player and terminal interaction, and performs an authored
   lie-down or settle-in camera movement without rendering hands or a body.
5. The room fades down while workshop ambience gives way to a short night transition.
6. Existing overnight systems advance atomically: operational day, battlefield movement and pressure, market
   refresh, battery turnaround, stock delivery, salvage expiry, and other already-authored daily changes.
7. The scene fades up from the cot at the start of the new day and presents a concise wake summary before returning
   control.

The cot becomes the only player-facing way to begin the next operational day. The tactical terminal may show that
operations can end and direct the player to rest, but it no longer exposes `BEGIN NEXT DAY` or advances time.

## 3. Transition requirements

- Day advancement is an exactly-once transaction; repeated input, scene interruption, or load cannot duplicate
  rewards, stock, free payloads, battery turnaround, market rotation, salvage expiry, or battlefield pulses.
- Autosave occurs at a documented safe boundary before sleep and again after the new day is fully initialized.
- If transition persistence is supported mid-fade, load resolves deterministically to either the pre-sleep state or
  the fully initialized new day, never a partial mixture.
- Camera movement respects room geometry and cannot clip through the cot, wall, ceiling, or nearby props.
- Audio and lighting transitions remain brief enough not to punish repeated days and can be skipped only after the
  authoritative state boundary is safe.
- The wake summary reports meaningful changes without becoming a second tactical-map or market interface.

## 4. Atmosphere and presentation

- The cot, blanket, pillow, nearby personal storage, and localized light follow the Milestone 11 visual language.
- The transition uses restrained camera motion, fabric contact, room tone, distant conflict, darkness, and morning
  ambience rather than a cinematic character animation.
- Variations may reflect risk or battlefield pressure only when driven by existing visible state and must not add a
  new random event system.

## 5. Validation

- Add automated coverage for availability blockers, confirmation cancellation, exactly-once day advancement,
  autosave boundaries, migration, and all existing daily consequences.
- Validate sleeping after a quiet day, after resolved sorties, with stale intelligence, with expiring salvage, and
  at every supported risk state below an active discovery incident.
- Confirm the old tactical-terminal day-advance path cannot be used.
- Capture pre-sleep, confirmation, fade, wake, and summary Game View states.
- Perform final visual QA for camera framing, clipping, occlusion, cot scale, material consistency, prompt
  readability, lighting continuity, fade continuity, and restoration of player control.

## 6. Excluded

- Visible player body or hands, dreams, sleep-management statistics, hunger, fatigue, survival meters, NPC bedroom
  routines, random night attacks, multiple sleeping locations, and real-time waiting.
- New daily economy, battlefield, or risk rules beyond integrating their existing transitions.
- A general cutscene framework not required by the sleep sequence.

## 7. Definition of done

The milestone is complete when the player must use the cot to advance the day, every existing overnight consequence
occurs exactly once, save/load cannot create a partial transition, the old terminal path is removed, automated
tests pass, and all representative sleep and wake states pass Play Mode and Game View QA.
