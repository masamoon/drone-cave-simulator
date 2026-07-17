# Milestone 05.2 Implementation — PSX Visual Pipeline

## Outcome

Milestone 5.2 adds a deterministic PSX-inspired presentation layer without changing workshop interaction, mission resolution, persistence, or replay generation. The Safe House now presents a recognizable Scout Field drone and service parts, a textured tactical terminal, and textured reconstruction props generated from the existing seeded 2D topography.

## Architecture

- `PsxVisualProfile` is the immutable ScriptableObject containing atlas and surface tuning.
- `PsxVisualKit` generates one deterministic 128×128 RGBA atlas at runtime. The atlas uses point filtering, mipmaps, and fixed material swatches.
- `PsxMeshFactory` creates bounded chamfered boxes, low-sided cylinders, wheels, and faceted canopies.
- `PsxVisualFactory` attaches idempotent presentation-only children to existing functional roots.
- Visual children do not add colliders or rigidbodies. Existing socket roots, authored poses, insertion axes, fasteners, and runtime identities remain authoritative.

The atlas is generated from exact authored pixel rules rather than an external image-generation pass. This keeps swatch placement, UV addressing, filtering, and test fingerprints deterministic.

## Representative assets

The Safe House Scout kit now includes:

- a layered centre shell, access panel, identification stripe, arm bracing, and wiring;
- motor bells, caps, and cooling fins;
- battery casing detail, strap, terminal, and label;
- camera bezel and lens glass;
- antenna base and coloured tip;
- propeller hubs and strike-rack presentation where applicable.

The tactical terminal now has a textured bezel, recessed screen, and physical side buttons while IMGUI continues to own its functional interface.

After-action reconstructions now use the same atlas for terraced terrain, roads, foliage, targets, the replay drone, and warning accents. Precision-strike results build an artillery silhouette with a shield, breech, barrel, wheels, carriage, and trails. Recon results build a utility vehicle with a cab, windscreen, cargo body, and wheels.

## Validation

- Focused Edit Mode: 6/6 passed.
- Focused Play Mode: 4/4 passed.
- Full Edit Mode regression: 80/80 passed.
- Full Play Mode regression: 43/43 passed.
- Safe House Play Mode inspection confirmed that presentation children follow the functional Scout and parts without replacing their colliders or rigidbodies.
- Reconstruction inspection confirmed a readable artillery silhouette, textured terraced terrain and road, deterministic foliage, replay controls, and workshop camera restoration.

Reference captures:

- `Assets/Screenshots/milestone-05-2-scout-framed.png`
- `Assets/Screenshots/milestone-05-2-reconstruction.png`

## Known limitations and approval points

- Only the Scout family, tactical terminal, and representative reconstruction props received this art spike. Survey, Utility, and the broader workshop remain outside Milestone 5.2.
- The atlas is intentionally small and shared; major assets will eventually benefit from authored per-kit texture allocation while retaining the same palette and filtering rules.
- Lighting remains the current prototype lighting, so dark composite and painted-metal separation needs subjective review on the target Windows display.
- Human approval is still required for pixel density, surface contrast, motor and battery recognition at service distance, artillery/vehicle silhouette clarity, and whether the overall result feels deliberately PSX-inspired.

Primary visual tunables are atlas noise, wear strength, surface colours, individual presentation dimensions, camera distance, and reconstruction lighting.
