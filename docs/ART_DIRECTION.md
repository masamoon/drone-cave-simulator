# Under Static — Binding Art Direction

## Status and authority

This document is the default visual contract for every player-facing asset in **Under Static**. Read it before
generating, modelling, texturing, importing, or procedurally presenting art. An active milestone may narrow the
asset scope, but it does not silently replace these rules. Any intentional exception must be stated in that
milestone or approved explicitly.

The target is **grounded contemporary low-poly with a restrained PSX influence**. The result should read as a
deliberate game art style, not unfinished greybox geometry, photorealism reduced by compression, or a generic
science-fiction kit.

## Visual pillars

1. **Recognizable silhouette first.** A drone, battery, camera, radio, charger, vehicle, or workshop fixture must
   be identifiable at normal gameplay distance before labels or texture detail are visible.
2. **Functional construction.** Visible parts explain how the object is held together and used: plates meet
   standoffs, screws enter surfaces, wires terminate at plugs, straps cross what they retain, and upgrades replace
   believable blanking plates or modules.
3. **Chunky, authored simplification.** Use broad planes, low-sided cylinders, selective single-segment bevels,
   and a few exaggerated functional details. Spend triangles on silhouette and interaction readability, not
   hidden surfaces or tiny curvature.
4. **Grounded improvisation.** Field equipment may show mixed finishes, exposed boards, rerouted leads, tape,
   straps, adapters, incomplete retail shells, and mismatched fasteners. It should remain credible contemporary
   equipment with fictional branding.
5. **Stable tactility.** The art supports deterministic sockets and authored gestures. Presentation geometry must
   never make a seated part appear misaligned, floating, or physically impossible.

## PSX influence

The PSX reference applies to:

- low polygon density and visibly faceted forms;
- compact pixel textures and controlled colour ramps;
- nearest-neighbour texture filtering;
- broad material blocks instead of physically perfect surfaces;
- restrained, chunky wear and labels;
- strong silhouettes under simple lighting.

Do not add global vertex wobble, affine texture distortion, unstable snapping, or camera-space jitter. Precision
service views, sockets, fasteners, and tools must remain visually stable.

## Geometry budgets

Budgets are measured as rendered triangles after export and import.

| Asset class | Budget |
| --- | ---: |
| Individual service component, attachment, or small workshop prop | Hard maximum below 1,000 triangles |
| Workshop furniture or bounded environment module | Target below 1,000 triangles; exceptions require approval |
| Complete assembled drone presentation | Target below 8,000 triangles |
| Mission vehicle or artillery presentation | Target below 4,000 triangles |
| Foliage instance | Target below 300 triangles |

Additional constraints:

- prefer one textured material, with no more than two where practical;
- use six- to twelve-sided cylinders according to screen size;
- use one-segment bevels only on edges that improve silhouette or material readability;
- omit invisible internal faces and decorative geometry that does not survive normal gameplay distance;
- do not use subdivision surfaces for final game exports;
- retain a readable low-poly facet structure rather than smoothing every edge.

The current Blender-authored small assets and drone components use the below-1,000-triangle rule as a hard test.
Future generators must report triangle counts and fail validation when the relevant budget is exceeded.

## Texture rules

- Authored asset-family textures are **128×128 RGBA** unless an explicit milestone approves another size.
- The runtime-generated physical tactical map in the Safe House is an approved exception: **512×512 RGBA**,
  bilinear filtered with mipmaps. The terminal preview remains 128×128 and point filtered.
- Use point/nearest-neighbour filtering and anisotropic level `0` in Unity.
- Use padded UV islands so atlas swatches do not bleed at service distance.
- Prefer a deterministic compact atlas or palette texture for each coherent asset family.
- Use painted colour blocks, identification bands, warning panels, labels, dirt, edge wear, and material separation
  selectively. Do not use photographic source images as finished textures.
- Preserve deliberate pixel scale. Avoid high-frequency noise that turns into shimmer or visual static.
- Repeating architectural textures must avoid isolated high-chroma diagonal marks. A tiny coloured scratch can tile
  into wall-length rays, as happened with the original red concrete artifact.
- Keep text symbolic or fictional where 128×128 resolution cannot support legible typography.

## Palette and materials

The safe house uses:

- warm practical lamps against cold exterior fill;
- aged timber, worn concrete, muted plastics, dark composite, painted steel, and bare metal;
- low-to-moderate saturation with small amber, yellow, red, blue, or cyan functional accents;
- matte and rough surfaces with restrained highlights;
- dark corners that preserve atmosphere without hiding interaction targets.

Mission recreations use the same material language with cooler, flatter field lighting and reduced saturation.
Glass, lenses, screens, indicator lamps, and bare fasteners may be more reflective, but they should not introduce a
glossy modern-rendering style that conflicts with the rest of the asset.

## Drone and component direction

- Drone categories describe physical airframes, not rigid mission roles. Payload and installed equipment determine
  whether an aircraft is used for reconnaissance, relay, delivery, reusable attack, or a one-way mission.
- Civilian donor drones must retain recognizable retail origins after conversion: partial shells, vacated mounting
  points, exposed boards, rerouted leads, and improvised adapters.
- FPV airframes use recognizable carbon plate construction, four arms, motor bells, an exposed electronics stack,
  a forward camera, antennas, battery leads, and visible standoffs.
- Components must remain individually readable in service view. Battery, camera, receiver, VTX, flight controller,
  ESC, motor, propeller, antenna, rack, and payload cannot collapse into one ambiguous centre block.
- Payload visuals remain sealed fictional abstractions. Model external cradle, retention, balance, wiring, and
  readiness only; never reproduce explosive preparation, fuzing, or arming mechanisms.

## Mechanical contact and attachment rules

Floating detail is a failed asset.

- Screw heads must visibly contact or slightly intersect their mounting surface. They may not hover above camera
  housings, frame plates, standoffs, motors, racks, or covers.
- Standoffs meet both plates they support. Brackets meet the part and frame they connect.
- Wires terminate inside a plug, grommet, board pad, or housing; both ends must be visually accounted for.
- Connected plugs meet their sockets. Disconnected plugs must have a believable loose resting pose.
- Batteries rest on a pad, bench, shelf, or frame and use visible straps or leads as appropriate. A cabled charger
  keeps batteries beside the station and exposes one additional front plug pair per capacity upgrade.
- Straps cross and touch the retained object. Racks include visible rails, saddles, bridges, or fasteners that explain
  how their load is carried.
- No decorative plate, rail, screw, cable, or label may float simply because it looks correct from one camera angle.

Validate attachment from top, side, front, and low oblique views. Use measured renderer bounds when perspective
makes contact ambiguous.

## Environment direction

- The workshop remains the visual centre of the game: cramped, practical, repeatedly used, and personally
  organized rather than decorated as a generic bunker.
- Furniture proportions must support first-person movement and the authored service camera.
- Props should communicate a function or story: storage, charging, repair, concealment, communications, rest, or
  power. Avoid decorative clutter that competes with interaction targets.
- Upgradeable fixtures show their future capacity physically through blanking plates, unused mounting points, or
  modular extensions. Do not fake capacity with inaccessible geometry.
- Large repeated surfaces need a final tiling inspection to catch seams, rays, obvious repetition, and pixel-scale
  mismatch.

## Interaction and presentation separation

Authored art is presentation, not the source of assembly truth.

- Keep visual meshes separate from deterministic socket, collider, inventory, installed-state, and persistence
  logic.
- Visual children normally have no enabled interaction colliders.
- Functional roots own identity and runtime state; authored models follow those roots.
- Installed transforms remain deterministic. Art must be fitted to the authored pose rather than moving the socket
  casually to disguise a visual mismatch.
- Dynamic state visuals—connected cables, secured straps, indicators, open guards, removed shells—must follow the
  explicit interaction state and preserve continuity between states.

## Blender source and export contract

- Keep editable `.blend` sources and deterministic `build_*.py` generators under `ArtSource/<AssetFamily>/`.
- Export Unity-ready FBX files under `Assets/UnderStatic/Resources/Art/<AssetFamily>/Models/` and 128×128 textures
  under the matching `Textures/` directory.
- Work in metres, apply scale before export, keep origins and transforms intentional, and regenerate UVs through the
  source script when geometry changes.
- Use stable prefixes: `SH_POC_` for safe-house assets, `DR_` for drone-kit assets, and `MR_` for mission-recreation
  assets. Texture names end in `_128`.
- Preserve meaningful child names for visual QA and tests, such as `Screw`, `Plug`, `Cable`, `Strap`, `Port`,
  `LockedBlank`, or `Guard`.
- Do not commit Blender backup files, Python bytecode, or cache directories.

## Required validation for every visual change

1. Rebuild from the checked-in Blender generator where applicable.
2. Record imported triangle count and verify the relevant budget.
3. Verify texture size, point filtering, anisotropic level, and material count.
4. Run relevant Edit Mode and Play Mode tests.
5. Inspect the actual player-facing state in Unity Game View—not only the Blender preview.
6. Capture representative final states and inspect:
   - framing and gameplay-distance recognition;
   - clipping and occlusion;
   - scale and proportions;
   - material and pixel-density consistency;
   - interaction-target readability;
   - state-to-state continuity;
   - contact at every screw, plug, wire, strap, bracket, and resting surface.
7. Correct every obvious defect before marking the asset complete. A screenshot with a visible floating element,
   broken connection, accidental shelf-like protrusion, texture ray, or unreadable silhouette is a failed result.

## Approval-sensitive tuning

Human visual approval remains necessary for:

- whether silhouettes are recognizable at normal play distance;
- pixel density and texture contrast;
- the balance between warmth, darkness, and interaction readability;
- damage and removed-part readability;
- charger, furniture, and drone proportions;
- whether an asset feels deliberately PSX-inspired rather than unfinished.

When subjective feedback changes one of these decisions, update this document so the next asset generator inherits
the settled direction.
