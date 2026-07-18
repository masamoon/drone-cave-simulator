# MILESTONE 05.2 — PSX Visual Pipeline and Representative Asset Set

> Historical scope note: Milestone 05.4 supersedes the old request flow referenced by the visual slice. Its PSX visual-language and representative-asset requirements remain applicable.

## Objective

Replace the most visible greybox primitives with recognizable, textured, PSX-inspired presentation while preserving every deterministic interaction transform, collider, socket, part identity, and mission result.

This is a visual-language spike, not a complete art pass. Procedural systems continue to own layout and topography; authored low-poly kits provide the objects placed into those layouts.

## Visual target

- recognizable silhouettes before surface detail;
- chunky low-poly construction with selective bevels;
- one deterministic 128×128 pixel atlas divided into controlled material swatches;
- nearest-neighbour filtering and restrained colour variation;
- painted panel lines, fasteners, labels, wear, and material separation;
- simple URP lighting without global vertex wobble or affine distortion;
- stable service-mode geometry with no visual movement at socket interfaces.

The PSX influence applies to modelling density, texture scale, palette, and shading. Precision repair views must remain stable and readable.

## Reusable presentation architecture

Add immutable `PsxVisualProfile` tuning and a runtime `PsxVisualKit` responsible only for generated presentation resources. It creates one deterministic texture atlas plus narrow materials for:

- dark frame composite;
- painted field metal;
- bare metal;
- battery/electronics labels;
- glass/lens;
- rubber;
- terrain earth;
- road;
- vegetation;
- warning/impact accents.

`PsxMeshFactory` supplies bounded reusable meshes such as chamfered boxes, low-sided cylinders, wheels, and faceted vegetation canopies. Visual meshes never become interaction colliders.

`PsxVisualFactory` attaches presentation children without replacing functional roots. Repeated calls must be idempotent.

## Representative Scout kit

Upgrade the active Scout Field drone and installed service parts:

- layered chamfered centre chassis with top access panel and identification stripe;
- arm braces and visible wiring runs;
- recognizable motor bells, caps, and cooling fins;
- propeller hub treatment;
- battery casing, strap, terminal block, and label;
- camera housing with nested lens rings;
- antenna base, cable, and coloured tip;
- compact strike-rack housing when present.

Existing sockets, fasteners, part roots, colliders, insertion axes, and authored poses remain untouched. Added detail is presentation-only and follows the same runtime part instance during storage, installation, and replay.

## Workshop terminal spike

Upgrade the tactical-map control with a textured monitor bezel, recessed screen, side controls, warning stripe, and low-resolution status treatment. IMGUI remains the functional interface.

## Reconstruction prop palette

The deterministic 2D map continues to select positions. Replace raw reconstruction primitives with reusable textured kit builders:

- three deterministic tree/bush silhouettes;
- textured terraced terrain and raised road;
- recognizable artillery carriage with shield, breech, barrel, wheels, and trails;
- recognizable observed utility vehicle with cabin, windscreen, wheels, and cargo body;
- improved reconstruction quadcopter;
- restrained distant-figure and unconfirmed-area treatments.

Prop selection and scale variation remain deterministic from map cells. No detailed ballistics, graphic casualties, or battlefield simulation is added.

## Budgets

- Scout complete presentation: target below 8,000 rendered triangles;
- individual service parts: target below 1,000 rendered triangles each;
- artillery/vehicle: target below 4,000 rendered triangles each;
- foliage instance: target below 300 rendered triangles;
- atlas: 128×128 RGBA, point filtered, mipmapped;
- no more than two textured materials on an individual presentation object where practical.

## Validation

Edit Mode tests cover atlas size/filtering/determinism, swatch distinction, mesh bounds, triangle budgets, idempotent enhancement, and absence of enabled colliders on visual children.

Play Mode tests cover Safe House kit creation, recognizable Scout detail, textured tactical terminal, service-mode interaction regression, reconstruction terrain material, deterministic foliage variants, artillery subcomponents, observed vehicle subcomponents, and replay entry/exit restoration.

Run all existing Edit and Play Mode tests. Capture service-mode and reconstruction screenshots for human approval.

## Exclusions

- complete Survey and Utility presentation kits;
- full workshop replacement;
- final art, authored animation, or audio pass;
- external packages;
- global PSX post-processing;
- unstable vertex snapping in service mode;
- procedural generation of arbitrary 3D meshes from prompts;
- new gameplay, economy, mission, or risk rules.

Human approval is required for pixel density, texture contrast, silhouette recognition, damage readability, camera-distance appearance, and whether the result feels PSX-inspired rather than unfinished.
