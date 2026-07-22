# UNDER STATIC — Game Specification

## 1. High concept

**Under Static** is a low-poly first-person workshop-management game about operating a concealed drone support cell near an active frontline.

The player works inside a small hidden room. They repair damaged drones, construct serviceable aircraft from mismatched spare parts, charge batteries, sort components, and plan sorties against a persistent local battlefield picture.

The enemy is materially superior. The player cannot dominate the battlefield. They can only influence individual moments:

- inspect a road before a supply movement;
- find a concealed firing position;
- observe an enemy approach;
- deliver equipment to an isolated friendly position;
- commit a scarce strike platform;
- recover a damaged reconnaissance drone;
- help one local unit survive another night.

Every launch, transmission, repeated route, active antenna, and visible recovery operation contributes to the risk that the workshop will be located.

Most play is quiet and methodical. Tools click against the workbench. Chargers hum. Rain hits the roof. A drone is slowly restored beneath a warm desk lamp.

Then an unfamiliar rotor circles outside.

The radio abruptly goes silent.

Dust falls from the ceiling.

The player realizes the enemy may have found them.

---

## 2. Elevator pitch

> A cozy but tense first-person workshop game where you build and repair contemporary drones, deploy them on abstracted battlefield support missions, and protect your hidden command post from discovery by a much larger enemy force.

The game combines:

- simple but tactile physical work;
- visible spare-parts scarcity;
- modular drone construction;
- mission prioritization;
- concealment management;
- subdued environmental storytelling;
- short, sudden horror sequences.

The player does not manually fly drones in the MVP. Their skill lies in preparation, equipment configuration, route and target planning, intelligence management, reserve management, and recognizing when continued activity is too dangerous.

---

## 3. Player fantasy

The player should feel like:

- a resourceful field technician;
- an overworked support coordinator;
- the caretaker of a fragile refuge;
- a small but consequential participant in a larger conflict;
- someone who survives through preparation rather than firepower.

The core fantasy is:

> We have three usable airframes, nine batteries, one good camera, and four units asking for help. What can I keep alive before the enemy notices us?

Strike capability is built in the workshop, never purchased as a ready-to-use aircraft. The player acquires
ordinary civilian drones, FPV kits, and mismatched donor components, strips away unsuitable assemblies, and
converts the remaining airframe into a mission-specific one-way or reusable payload carrier. A "strike drone"
is therefore an earned runtime configuration with visible workshop history, not a product category sold by the
market.

Converted aircraft should visibly retain evidence of their origin and alteration: incomplete retail shells,
vacated mounting points, exposed boards, rerouted leads, improvised cradles, mixed fasteners, straps, tape, and
field-made adapters. Payload preparation remains abstracted to mounting, retention, balance, compatibility, and
functional testing; the game does not reproduce real-world explosive preparation, fuzing, or arming procedures.

---

## 4. Tone and setting

The setting is a fictional contemporary conflict in which a smaller defending force faces a materially superior invader.

Technology is based on systems already in real operational use:

- commercial reconnaissance quadcopters;
- improvised FPV airframes;
- larger reusable multicopters;
- fixed-wing reconnaissance drones;
- fibre-controlled FPVs;
- unmanned ground vehicles;
- portable electronic-warfare equipment;
- radio and satellite communication;
- conventional infantry and artillery support.

Automation is limited to plausible functions:

- waypoint navigation;
- altitude holding;
- return-to-home;
- route following;
- camera stabilization;
- lost-link behaviour;
- basic tracking assistance.

Humans remain responsible for mission planning, identification, prioritization, and weapon release.

The tone is not cute, comedic, or triumphalist. “Cozy” means:

- warm practical lighting;
- tactile routine;
- familiar object placement;
- a room that gradually becomes organized and personal;
- shelter from distant danger;
- quiet companionship and radio procedure;
- relief when equipment returns home.

Fear comes from the possibility that the safe room is no longer safe.

---

## 5. Design pillars

### 5.1 The workshop is the primary game space

The workshop is not a menu hub. It is where most play occurs.

The player physically:

- picks parts from shelves and trays;
- places drones on a repair mat;
- removes damaged components;
- installs replacements;
- connects batteries;
- tests motors and cameras;
- labels completed builds;
- carries ready equipment to deployment storage;
- walks between the bench, radio, tactical map, and concealment controls.

Important resources should have a visible physical presence.

A quantity such as “four motors” should be represented by four motors in a parts tray, with UI used as clarification rather than the primary representation.

Loose recoverable parts in the workshop can be transferred into physical parts storage. The safe-house exchange keeps field-grade basic build components continuously available so a new drone can always be assembled when the player has sufficient funds; higher-grade, damaged, and complete-airframe offers may remain limited or rotating stock.

---

### 5.2 Low complexity, high tactility

Repair and construction are intentionally simple.

The interaction vocabulary is small:

- point;
- highlight;
- grab;
- move;
- rotate;
- guide;
- insert;
- secure;
- remove;
- inspect;
- test;
- cancel.

The player should understand a fault visually and repair it through familiar actions.

Example:

`damaged motor → remove two fasteners → pull motor free → insert compatible replacement → secure fasteners → run motor test`

A repair should usually take between 10 and 40 seconds.

Tactility comes primarily from:

- controlled object lag;
- magnetic guidance;
- insertion resistance;
- discrete detents;
- tool movement;
- layered sound;
- small camera reactions;
- visible mechanical consequences;
- a clear functional test.

Do not rely on high mechanical difficulty.

---

### 5.3 Authored interaction, not uncontrolled physics

Loose parts can behave physically on large surfaces, but precision work uses guided deterministic interactions.

A compatible component near a valid socket:

1. enters a guidance region;
2. begins aligning toward the correct orientation;
3. becomes constrained to an insertion direction;
4. is pushed or rotated through a meaningful gesture;
5. reaches a seated state;
6. requires the correct tool or lock action;
7. snaps only at the final deterministic transform.

Tiny screws do not need to exist as free rigid bodies. Fasteners can be represented by visible screw heads, rotational progress, sound, and completion state.

The goal is tactile credibility, not literal simulation.

---

### 5.4 Scarcity creates improvisation

Parts are not perfectly standardized or plentiful.

The player manages:

- frames;
- motors;
- propellers;
- batteries;
- cameras;
- antennas;
- control modules;
- payload mounts;
- fasteners;
- adhesives and temporary repair materials.

Parts have condition and capabilities.

A functional drone may be imperfect:

- good camera, weak endurance;
- strong signal, noisy motor;
- damaged frame suitable for one more sortie;
- heavy battery reducing payload;
- mismatched motors lowering reliability;
- repaired antenna with reduced range.

The player should often choose between:

- using the best component now;
- saving it for a critical mission;
- cannibalizing another drone;
- performing a temporary repair;
- sending a marginal build;
- declining the mission.

---

### 5.5 Influence rather than domination

The player does not command conventional forces directly.

The persistent battlefield presents opportunities such as:

- inspect a road;
- observe a tree line;
- confirm a suspected firing position;
- check a route before resupply;
- monitor an approach;
- support an evacuation;
- maintain observation for artillery;
- determine whether an enemy drone team is present.

Each discovered contact has:

- location;
- type and durability;
- current or stale intelligence;
- distance from the workshop;
- operational value;
- risk and suitability for the staged aircraft;
- consequence of acting late or with the wrong loadout.

The player cannot observe or attack every contact immediately. Mobile infantry intelligence decays when a new day begins, forcing a choice between acting on nearby information now and pursuing more valuable distant targets.

Success means changing important local outcomes while preserving enough capability to continue.

---

### 5.6 The workshop becomes precious

Each hideout begins sparse and unfamiliar.

Over several shifts the player:

- organizes trays;
- positions tools;
- adds chargers;
- improves lighting;
- routes cables;
- establishes alternate antenna positions;
- creates a more efficient ready shelf;
- accumulates repaired equipment and personal details.

The workshop gradually becomes a temporary home.

The possibility of abandoning it gives progression emotional weight.

---

### 5.7 Quiet routine, sudden horror

The default rhythm should be calm.

Warning signs accumulate slowly:

- interference on a local channel;
- a distant rotor that does not leave;
- a friendly observer reporting an aircraft near the rear;
- shelling landing closer than before;
- a repeated loss of signal;
- a vehicle stopping outside;
- an unexplained light through a boarded window;
- someone failing to answer the radio.

Discovery sequences are short, authored, and infrequent.

The horror comes from transformed context. Familiar objects become liabilities:

- an active charger emits light;
- the antenna must be disconnected;
- a drone on the test stand cannot be allowed to spin;
- a radio must be silenced;
- valuable parts may need to be left behind;
- the player may have to hide, evacuate, or choose what to carry.

---

## 6. Core gameplay loop

A normal session represents an operational shift lasting approximately 25–45 minutes.

### 6.1 Assess

The player reviews:

- the persistent battlefield map and known contacts;
- returned equipment;
- damaged components;
- charged and depleted batteries;
- supply deliveries;
- weather and visibility;
- known enemy activity;
- workshop suspicion and recent warning signs.

### 6.2 Prepare

The player:

- diagnoses damaged drones;
- repairs essential components;
- assembles mission-specific configurations;
- allocates batteries;
- chooses whether to use high-quality or improvised parts;
- performs functional tests;
- moves completed drones to the ready area.

Active sorties may reveal new contacts while preparation is underway.

### 6.3 Deploy

At the tactical map, the player chooses:

- which completed drone to stage;
- Recon, Kamikaze Strike, or Grenade Drop;
- a reconnaissance route or discovered target;
- whether the intelligence is current enough to justify committing the aircraft or ordnance.

All routes originate at the visible workshop marker. The sortie then runs as an abstract operation while its progress remains visible on the same map.

### 6.4 Manage concurrent work

While missions resolve, the player returns to physical work.

They may:

- repair another drone;
- prepare a reserve battery;
- monitor an intermittent feed;
- respond to radio messages;
- redirect or abort an operation;
- authorize a follow-on mission;
- reduce workshop emissions;
- react to signs of discovery.

The game intentionally divides attention.

### 6.5 Recover

Returned equipment may be:

- intact;
- dirty;
- partially damaged;
- missing a component;
- carrying a degraded battery;
- unreliable;
- lost entirely.

The player decides whether to:

- repair immediately;
- strip for parts;
- reserve for low-risk work;
- convert to a one-way build;
- store for later.

### 6.6 Reduce exposure or continue

Additional launches may help friendly units but increase workshop risk.

The player chooses when to:

- continue operations;
- enter partial radio silence;
- stop launching from one site;
- switch to a secondary deployment point;
- conceal equipment;
- end the shift early;
- prepare to relocate.

---

## 7. Campaign structure

A first commercial campaign should contain approximately 8–12 operational shifts across several hideouts.

Campaign loop:

1. Receive the local operational situation.
2. Establish or inherit a hidden workshop.
3. Organize equipment and supplies.
4. Complete several shifts.
5. Accumulate enemy attention.
6. Decide whether to remain or relocate.
7. Pack limited equipment.
8. Leave behind or destroy what cannot be carried.
9. establish a new workshop;
10. continue with changed resources and battlefield conditions.

Each hideout is a temporary home.

The campaign should avoid a grand strategic map. The player experiences the wider conflict through:

- changing requests;
- radio reports;
- equipment shortages;
- friendly units disappearing or surviving;
- new enemy methods;
- movement between shelters.

---

## 8. Core systems

### 8.1 Interaction system

Responsibilities:

- first-person focus;
- highlighting;
- prompts;
- grabbing;
- holding and rotating;
- interaction cancellation;
- tool selection;
- guided socket transitions;
- feedback dispatch.

Primary states:

`Idle → Highlighted → Held → Guided → Seated → Securing → Installed → Tested`

Interaction state must be serializable or safely resolvable on save.

---

### 8.2 Part system

Initial part categories:

- Frame
- Motor
- Propeller
- Battery
- Camera
- Antenna
- Control module
- Payload mount

Each immutable part definition includes:

- ID;
- display name;
- category;
- compatible socket types;
- mass;
- power draw;
- capability modifiers;
- base reliability;
- prefab reference;
- salvage yield;
- installation feedback profile.

Each runtime part instance includes:

- unique instance ID;
- condition;
- repair state;
- current location or owner;
- installed socket ID;
- temporary repair modifiers;
- contamination or damage tags.

---

### 8.3 Socket system

Each socket defines:

- socket ID;
- accepted part categories or tags;
- capture radius;
- entry direction;
- guidance strength;
- required orientation tolerance;
- insertion distance;
- final installation pose;
- required securing tool;
- required fastener count;
- removal procedure.

Socket compatibility must be data-driven.

---

### 8.4 Tool system

Initial tools:

- screwdriver;
- small wrench;
- cleaning cloth;
- diagnostic switch or tester.

The MVP only requires the floating screwdriver.

A floating tool should:

- spawn only for its relevant tool action and despawn on release or completion;
- align to the target;
- visibly respond to player input;
- respect the active fastener axis;
- produce progress, resistance, and detents;
- remain absent outside its relevant tool action.

No hands are rendered.

---

### 8.5 Drone assembly system

A drone assembly consists of:

- frame;
- installed parts by socket;
- derived statistics;
- completeness state;
- tested state;
- mission eligibility;
- persistent runtime identity.

Initial derived statistics:

- endurance;
- observation quality;
- control reliability;
- mission reliability;
- available payload;
- overall condition.

A build does not need to be optimal to be mission-capable.

The compact FPV electronics stack is assembled as distinct serviceable parts inside a carbon-plate frame held
apart by visible metal standoffs. The lower four-in-one ESC is seated and secured first; the flight controller
then seats on authored soft mounts and becomes installed only after the player plugs in its short keyed wiring
harness. The connector moves only the final few centimetres into its board socket; it is not represented by a
hinged cover or latch. Removing the ESC requires unplugging and removing the flight controller first. Boards,
mounting rings, fasteners, motor pads, solder pads, ports, and wiring remain readable at the grounded low-poly
target.

The first scratch-built strike configuration adds one authored underslung payload mount. The mount is a
single inventory-owned component, while its captive fasteners, two retention straps, and short control harness
belong to the mount and are not separate inventory entries. Installation is complete only after the player
seats the mount, tightens all four fasteners, secures both straps, and connects the harness in that order. The
aircraft cannot pass diagnostics, be staged, or launch a strike while any of those steps is incomplete. Removal
reverses the dependent steps: disconnect the harness, release the straps, then loosen the mount fasteners.
Payload contents and release or initiation mechanisms remain abstract; the workshop interaction is limited to
the externally visible cradle, retention, wiring, balance, and readiness check.

---

### 8.6 Inventory and storage

Resources are represented physically where practical.

Storage locations include:

- divided parts trays;
- ready shelf;
- damaged equipment shelf;
- charging rack;
- salvage bin;
- deployment cases.

The player should learn the room spatially.

UI may show exact quantity and compatibility when focused, but should not replace the physical representation.

---

### 8.7 Battery system

Battery states:

- charged;
- assigned;
- installed;
- depleted;
- charging;
- degraded;
- unsafe.

Core actions:

- remove battery;
- inspect;
- place into charger;
- connect charger;
- move to ready rack;
- install into drone;
- secure;
- read basic status.

Compact FPV batteries are soft rectangular LiPo bricks rather than proprietary slide cartridges. They rest on
anti-slip pads on the structural carbon top plate and visibly carry thick red/black main leads with a keyed
XT-style connector. Its battery and airframe halves meet face-to-face and remain visibly tethered to their
respective pigtails when unplugged; they must not overlap as generic blocks. A thinner balance-wire bundle ends
in a compact white balance plug. Two separate hook-and-loop straps pass
around both the battery and top plate. Their side wraps must visibly connect the tightened top bands to the
frame; when loosened, their tails lie flat against the plate instead of floating or hinging open. No battery
hatch, proprietary rail, or hard-plastic latch is used.

The scratch-build inventory contains three charged packs that share the same electrical and physical fit:

- compact: shortest endurance, lowest mass, and the greatest payload and control margin;
- balanced: moderate endurance and mass with no pronounced handling penalty;
- long-range: greatest endurance, highest mass, and reduced payload, control, and reliability margin.

The focused-part status readout exposes pack mass and signed endurance, payload, and control modifiers before
installation. Battery choice is therefore a legible build decision rather than a hidden optimal upgrade.

For the vertical slice, battery interaction follows after the motor framework is proven.

---

### 8.8 Mission system

Missions are abstract simulations rather than piloted sequences. During the final approach, the player may
optionally open a degraded first-person feed generated from the planned route and persistent battlefield.
The feed is observational: it provides no direct flight control and never replaces deterministic resolution.

Immutable sortie profiles define:

- sortie type;
- required capabilities;
- whether the airframe or a charge is committed;
- distance and scoring weights;
- target suitability;
- wear and duration tuning.

Mutable mission state owns the saved planning draft, one active sortie, resolved history, committed resources, rewards, and fleet transitions. It does not own battlefield truth.

Mission result considers:

- assigned drone statistics;
- part condition;
- planned route or selected contact;
- distance from the workshop;
- intelligence age;
- sortie-to-target suitability;
- random variation within readable bounds.

The player should understand why a mission was likely to succeed or fail.

---

### 8.9 Persistent battlefield system

One deterministic 4 km × 4 km map persists for an entire run. The workshop is visible near its southwest corner. Hidden ground truth is kept separate from player-visible intelligence.

Reconnaissance routes reveal contacts inside a sensor corridor as the aircraft reaches them. Artillery and the enemy base remain stationary. Infantry relocates between days, leaving a selectable stale last-known position that may produce a no-contact strike. Reconnaissance can reacquire it. Destroyed contacts remain crossed out, and rewards are granted once from contact state.

Sorties may originate at the workshop or at one authored remote cache. Remote deployment and recovery are short first-person procedures, not a second explorable world, and reusable aircraft remain unavailable while cached in the field.

---

### 8.10 Workshop risk system

Workshop discovery risk is not a simple health bar, although a debug meter may exist.

Inputs include:

- transmission duration;
- transmitter power;
- number of launches;
- repeated flight paths;
- active jammer use;
- nighttime light leakage;
- audible testing at dangerous times;
- nearby enemy reconnaissance;
- recent partial detection events.

The system produces escalating states:

1. **Quiet**
2. **Possible attention**
3. **Pattern suspected**
4. **Active search**
5. **Likely located**
6. **Discovery event**

Exposure does not passively decay. Silencing the workshop transmitter stops its active-sortie contribution but invokes an authored lost-link procedure: reconnaissance coverage is truncated, an unreleased grenade payload is recalled and refunded after recovery, and a kamikaze result remains unconfirmed until the link returns.

Remote sites own a separate persistent attention value. Entering, repeating visits, operating a relay, launching, recovering equipment, and securing salvage increase site attention. A forced retreat can make a site Hot and create traceable workshop exposure without becoming an infantry-combat or player-injury system.

The player receives diegetic warning signs before severe escalation.

---

### 8.11 Discovery events

Discovery events are authored scenarios, not generic waves.

Possible actions:

- shut down the transmitter;
- disconnect an antenna;
- stop a motor test;
- extinguish visible lights;
- hide sensitive equipment;
- remain silent;
- choose evacuation cases;
- leave through a concealed route.

The first vertical slice uses a simple event:

- unknown rotor overhead;
- warning over radio;
- transmitter must be shut down before suspicion reaches the failure threshold.

---

### 8.12 Save system

Save data includes:

- part instances and condition;
- installed assembly state;
- inventory locations;
- drone derived state;
- mission status;
- workshop risk;
- current shift;
- settled player layout choices where supported.

Partial precision interactions should not be restored in unstable states.

On save, a held or partially guided component should either:

- serialize to a safe explicit state; or
- be returned deterministically to its last valid loose, seated, or installed state.

---

## 9. Visual direction

The binding production constraints for generated and authored assets live in `docs/ART_DIRECTION.md`. This section
defines the high-level product direction; the dedicated art document records the settled triangle, texture,
attachment, Blender-source, naming, and visual-QA rules that future asset work must follow.

### Style

- grounded contemporary low-poly;
- readable silhouettes;
- large planar surfaces;
- restrained geometry;
- small bevels on important hard edges;
- muted materials;
- minimal texture detail;
- labels, tape, dirt, and wear used selectively.

### Interior palette

- warm work lights;
- aged wood;
- muted plastics;
- painted and bare metal;
- dark corners;
- cold light leaking from outside.

### Tools and interactions

- no visible hands;
- floating tools remain physically grounded;
- movement should be fast but not teleporting;
- tools align visibly before acting;
- active parts receive restrained highlights;
- guidance should be visible through motion and shape alignment rather than holographic science-fiction effects.

### Damage language

- bent;
- cracked;
- taped;
- dirty;
- scorched;
- loose;
- misaligned;
- missing.

Avoid exaggerated cartoon damage.

---

## 10. Audio direction

Sound is central to tactility and atmosphere.

Interaction layers include:

- plastic contact;
- metal contact;
- scrape;
- connector insertion;
- screw rotation;
- ratchet ticks;
- final torque click;
- electric initialization;
- motor spin;
- charger fan;
- switch detents.

Assembly sounds must describe the mechanism rather than reuse a generic confirmation click. Battery retention uses a short hook-and-loop pull and buckle contact, board harnesses use a plastic connector insertion or release snap, and fasteners distinguish rotation ticks, final torque, and loosening breakaway. Short cues should originate at the manipulated component and use a small pool of variations to avoid obvious repetition. The MVP uses clearly labeled CC0 field recordings for these physical layers, with a data-authored profile selecting, trimming, varying, and combining the source material; procedural synthesis remains a fallback rather than the primary assembly sound.

Workshop ambience includes:

- rain;
- wind;
- generator hum;
- charger fans;
- quiet radio traffic;
- distant impacts;
- building creaks;
- cloth and footsteps;
- intermittent aircraft noise.

Horror often begins as an audio anomaly before a visual event.

---

## 11. MVP scope

The smallest meaningful MVP contains:

### Environment

- one greyboxed low-poly workshop;
- one workbench;
- one tactical map station;
- one ready shelf;
- basic parts storage;
- simple concealment controls.

### Parts and construction

- one drone frame;
- four motor sockets;
- motors;
- propellers;
- one battery socket with three compatible battery sizes;
- one camera;
- one underslung strike payload mount with captive straps, fasteners, and control harness interactions;
- floating screwdriver;
- installation, removal, and testing;
- persistent assembly state.

### Management

- visible spare-parts inventory;
- damaged and intact part condition;
- one simple salvage or cannibalization action;
- one persistent battlefield map;
- player-planned reconnaissance and strike sorties;
- persistent current, stale, disproven, and destroyed contact intelligence;
- returned equipment wear.

### Concealment

- simple workshop risk model;
- transmission and launch contributions;
- one warning sequence;
- one discovery-prevention interaction.

### Content

- one deterministic local battlefield containing infantry, artillery, and an enemy base.

---

## 12. Vertical slice: Persistent Sorties

The player repairs and stages a reconnaissance drone, then creates the operation on the persistent tactical map.

Sequence:

1. A reconnaissance drone is available but has a damaged rear-left motor.
2. The player picks up the drone or places it on the repair fixture.
3. The player removes the damaged motor using the floating screwdriver.
4. The player selects a compatible replacement from the parts tray.
5. The motor is guided, inserted, and secured.
6. The player installs a charged battery.
7. The player performs a motor test.
8. The player places the drone on the ready shelf.
9. At the map, the player draws a reconnaissance route within the aircraft's range.
10. The mission resolves while the player continues working and contacts appear progressively.
11. The reconnaissance drone returns with depleted battery and minor wear.
12. The player stages a strike-capable drone and selects a discovered contact.
13. The strike report updates that contact on the same persistent map.
14. An unidentified aircraft is heard overhead.
15. The radio operator warns that local transmissions may be exposed.
16. The player shuts down the transmitter and darkens the relevant equipment.
17. The aircraft eventually leaves, returning the room to uneasy quiet.

The slice proves:

- tactile repair;
- modular construction;
- physical inventory;
- hands-on route and target planning;
- persistent intelligence;
- persistent equipment wear;
- workshop risk;
- quiet-to-horror tonal transition.

---

## 13. Explicit exclusions

The MVP and first vertical slice do not include:

- direct FPV control;
- any manually flown drone;
- visible hands;
- realistic free screws;
- full drone aerodynamics;
- infantry tactical control;
- detailed projectile simulation;
- open-world outdoor movement;
- multiplayer;
- procedural campaign generation;
- large NPC populations;
- a workshop decorating game;
- autonomous target-selection systems;
- science-fiction drone technology;
- final-quality character animation;
- full battlefield rendering.

---

## 14. Development strategy

Milestones 1–7 established the current foundation: tactile component service, complete drone assemblies,
physical ownership and fleet storage, the market, persistent sorties, return consequences, workshop exposure,
field operations, and the experimental frontline loop. Their documents remain implementation history and
regression contracts rather than the active forward roadmap.

### Milestone 8 — Sustainable Workshop Economy

Tune salvage, market stock, part availability, prices, rewards, and losses until repairing, stripping, buying,
selling, and holding equipment are all legible decisions. A bad sortie must hurt without creating an
unrecoverable economic dead end. See `MILESTONE_08.md`.

### Milestone 9 — Discovery and Game Over

Turn the existing workshop-risk endpoint into one authored discovery incident and a complete game-over flow.
The player receives readable warnings and a final prevention opportunity before failure. See `MILESTONE_09.md`.

### Milestone 10 — Functional Hideout Upgrades

Let the player spend earned resources on a small set of physically visible workshop improvements that change
storage, turnaround, or concealment capability. This is functional progression, not freeform decoration. See
`MILESTONE_10.md`.

### Milestone 11 — Visual Identity and Readability

Turn the existing grounded low-poly and PSX-inspired foundation into a deliberate, recognizable visual language
for the workshop, drones, tools, functional props, and interface. Readability of interaction state and material
function takes priority over decorative detail. See `MILESTONE_11.md`.

### Milestone 12 — Battlefield Presentation

Upgrade the tactical map and degraded final-approach feed so terrain, frontline pressure, intelligence age,
routes, contacts, and outcomes are readable and visually compelling without implying direct flight or hidden
knowledge. See `MILESTONE_12.md`.

### Milestone 13 — Sleep and Day Transition

Replace terminal-driven day advancement with an authored return to the living corner. The player ends the day by
sleeping in the cot, sees a concise transition, and wakes into the deterministically advanced workshop state. See
`MILESTONE_13.md`.

The detailed document for the active milestone is binding. Earlier milestone documents provide regression
coverage but do not authorize additional expansion. Do not proceed until the active milestone is stable,
validated in Play Mode, and subjectively acceptable.

---

## 15. Product success test

The concept is working when players describe it as:

> The game where you run a tiny hidden drone workshop, physically keep damaged machines alive, and feel terrified when something starts circling above the roof.

It is not working if players primarily describe it as:

- an FPV simulator;
- a generic crafting game;
- a standard RTS;
- a physics sandbox;
- a menu-driven mission manager.
