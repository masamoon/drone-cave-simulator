# MILESTONE 11 — Visual Identity and Readability

## 1. Goal

Establish a distinctive visual identity for *Under Static* and apply it to one representative, fully playable
workshop slice:

`Recognizable silhouette → readable material and function → clear interaction state → cohesive atmosphere`

The result should no longer read as generic greybox or generic retro low-poly. It must remain easy to understand
while moving, servicing a drone, and responding under pressure.

## 2. Visual pillars

The binding visual language is:

1. **Field-repaired contemporary hardware** — exposed structure, mixed but plausible components, restrained tape,
   labels, rerouted leads, worn coatings, and visible service history.
2. **Warm refuge against cold surveillance** — warm localized work light and aged interior materials contrasted
   with cold exterior leakage, screens, radio light, and threat cues.
3. **Chunky silhouette, selective information** — large low-poly planes and recognizable profiles carry the form;
   texture detail, markings, and accents appear only where they explain function, ownership, wear, or danger.

The PSX influence remains in polygon density, texture scale, palette discipline, and restrained shading. Global
vertex wobble, unstable precision geometry, excessive pixel noise, and nostalgic effects that obscure interaction
targets remain excluded.

## 3. Binding scope

- Produce a compact visual-style guide covering palette, value structure, material families, geometry density,
  texture scale, edge treatment, lighting, typography, iconography, wear, decals, and state accents.
- Define a consistent functional accent language for interactable, selected, serviceable, warning, and dangerous
  states without covering objects in emissive outlines.
- Refine the representative Safe House view: room shell, workbench, service bay, cot corner, tactical station,
  exchange terminal, storage, charging, lighting, and concealment controls.
- Refine one representative aircraft and its visible motors, propellers, battery, camera, antenna, payload mount,
  connectors, fasteners, and floating screwdriver.
- Bring terminal chrome, prompts, status panels, labels, and icons into the same visual language while preserving
  information density and legibility.
- Reuse the existing deterministic PSX atlas, mesh, and visual-factory architecture where suitable; extend it only
  when the style guide requires a reusable presentation capability.
- Keep visual children separate from interaction roots, colliders, socket transforms, runtime identity, and
  gameplay state.

## 4. Readability requirements

- Important silhouettes remain identifiable in warm light, cold light, and partial darkness.
- Focus, compatibility, damage, incomplete fastening, tested readiness, market quality, and risk warnings are
  distinguishable without relying on color alone.
- Text and icon contrast remain readable at the supported resolution and ordinary first-person viewing distance.
- Material separation makes carbon, painted metal, bare metal, rubber, glass, plastic, wood, fabric, and electronics
  recognizable without high-resolution textures.
- Wear adds history without hiding ports, fasteners, sockets, labels, or contact edges.
- No visual refinement changes deterministic installed poses or makes service targets harder to acquire.

## 5. Validation

- Capture a fixed set of representative Game View frames before and after the pass: workshop entrance, service
  bay, focused component, terminal, cot corner, ready storage, and warning lighting.
- Run the full service, market, storage, charging, sortie-staging, and concealment interactions after visual changes.
- Perform Game View QA for framing, clipping, occlusion, scale, material consistency, interaction-target
  readability, lighting continuity, and state-to-state continuity.
- Test readability with color-vision simulation or grayscale value checks for all functional state accents.
- Record remaining subjective decisions requiring human feedback instead of claiming that compile success proves
  the visual target.

## 6. Excluded

- Final art for every asset, high-resolution realism, visible hands, character art, motion-captured animation,
  freeform decoration, and a full environment rebuild.
- Direct changes to economy, missions, battlefield truth, or interaction rules beyond visual integration fixes.
- The battlefield-map and final-approach visual overhaul, which belongs to Milestone 12.

## 7. Definition of done

The milestone is complete when the style guide is documented, the representative workshop slice and aircraft use
it consistently, all functional states remain readable, existing interactions and tests remain valid, and every
required representative frame passes final Game View QA.
