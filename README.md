# Aruun

A co-op voxel survival RPG set on Aruun, the Living Red World: Mars when it was alive, green and full of water. Small 0.25 m voxels, real-time
play in first or third person, a world you can dig, build and break.

**Current milestone: M0, The First Stone.** A small seeded region of meadows, forests, lakes and
red mesas with one handbuilt ruin,
digging and building, destruction with falling debris, one ancient word to learn and speak,
saving, and drop-in co-op.

> Status: written but **not yet run in the engine**. The first job on a real machine is to
> install the pinned tools below, open the project and fix whatever the first run turns up.

## Setup

1. Install **Godot 4.x stable** (the project targets 4.4).
2. Install **Voxel Tools** by Zylann from its GitHub releases page, matching your Godot version:
   - If the release has a **GDExtension** build, unzip it so the files end up in
     `addons/zylann.voxel/`, then open the project with normal Godot.
   - Otherwise download the release's **prebuilt Godot editor** (Voxel Tools compiled in)
     and open the project with that.
3. Write the exact versions you used into `docs/decisions/0001-engine-stack.md`.
4. Open `project.godot` and press Play.

## Controls

| Action | Key |
| --- | --- |
| Move / sprint / jump | WASD / Shift / Space |
| Dodge | Ctrl |
| First or third person | V |
| Dig (round bite), or attack | Left mouse. With a weapon, or a creature ahead, it attacks: tap for a light blow, hold for a heavy one |
| Place a block, eat, plant, set down | Right mouse |
| Guard | Hold right mouse with a weapon, a tool or bare hands |
| Lock on to a creature | Middle mouse |
| Choose a hotbar slot | 1 to 8, or the mouse wheel |
| Pick up, harvest, drink, read an inscription, help a friend up | E |
| Speak the chosen word / next word | Q / R |
| Bag and crafting | Tab |
| Controls on screen | F1 |
| Blast (test destruction) | B |
| Save | F5 (also on quit) |
| Free the mouse | Esc |
| Testing: starter kit and all words / switch day and night / call a nightstalker | F9 / F10 / F11 |

## Surviving

You are a human castaway on Aruun. Everything below runs on the host and is the same for every
player in co-op.

- **Conditions** (hunger, thirst, cold, thin air, dust, fatigue, gravity debt) are data in
  `content/survival/rulesets.json`. The default **Light** rules only reward looking after
  yourself; **Hardcore** (`--ruleset=hardcore`) adds real penalties, tool wear and a downed state.
- **Gathering and crafting**: dug blocks go into your bag and building spends them. Wild reeds,
  glasswort, redroot and flint grow from the world seed. Recipes are in `content/items/items.json`;
  some need a campfire or a Tether nearby.
- **Farming**: plant seeds in soil with right click. Crops grow with the clock, faster near a Tether.
- **Combat**: light and heavy attacks, guarding, dodging and stamina. Redstriders graze by day,
  nightstalkers hunt at night. A heavy Fallhammer blow breaks the ground.
- **Words**: Push, Pull and Weigh throw, drag and pin creatures. Every word adds gravity debt,
  which weighs you down until it is repaid.
- **Destruction**: a piece cut loose falls, hurts what is under it and becomes terrain again.
- **Gravity**: Aruun pulls at 0.38 g. Inside an Occlusion site Earth's gravity returns, and your
  fixed human strength buys less: slower, shorter jumps, harder falls, costlier words.
- **Home**: set down a Tether stone to choose where you wake after falling.

## Co-op

From the start menu: one player clicks **Host co-op** (UDP port 7777), friends type the host's
address and click **Join co-op**. Over the internet the host must forward UDP 7777 for now;
Steam relay comes later.

Command line works too (handy for testing two windows on one PC):

```
godot -- --host                      # or --single
godot -- --join=127.0.0.1
godot -- --host --world=mytest       # separate save folder
godot -- --join=127.0.0.1 --guid=friend --name=Friend   # a second character on the same PC
godot -- --single --ruleset=hardcore
```

## Where things are

```
content/      blocks, words and structures as JSON; art as .vox models (data, not code)
scripts/
  core/         content loading, world constants
  worldgen/     seeded terrain + landmark stamping
  voxel/        block library from content
  multiplayer/  session setup, the server-owned world and its edit journal
  gameplay/     edit rules, loose-piece detection, debris
  sim/          the server's game: characters, clock, combat, saving (child of the world)
  entities/     creatures, plants, drops and structures, and how they are drawn
  survival/     conditions and character state (pure rules)
  items/ farming/ combat/ destruction/   inventory, crafting, crops, melee and collapse rules
  data/         loads items, recipes, rulesets, plants and creatures
  ui/           survival HUD, bag and crafting screen
  language/     word interpreter (validated effects only)
  player/       player controller and camera
  main/         entry scene: atmosphere, HUD, menu
  art/          .vox reader, mesher, model library, rigs, art gallery
tests/        unit tests (godot --headless -s tests/run_tests.gd)
tools/        content validator, art generators and importers (see docs/art/README.md)
docs/         decisions (ADRs) and design links
```

Saves live in Godot's user folder under `worlds/<name>/`: `world.json` (seed and worldgen
version), `journal.txt` (every edit), `chunks.sqlite` (terrain cache), `survival.json` (clock,
plants, structures, drops) and `characters/<id>.json` (one per player, kept by the host).

## Checks

```
python3 tools/content_validator/validate.py
godot --headless --import && godot --headless -s tests/run_tests.gd
godot --headless -s tests/integration/smoke.gd -- --single --world=smoke_test   # scripted play-through
godot --headless -s tests/integration/coop.gd -- --host --world=coop_test       # with the next line, in two terminals
godot --headless -s tests/integration/coop.gd -- --join=127.0.0.1 --guid=friend
```
