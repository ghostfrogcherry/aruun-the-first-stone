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
| First or third person | V |
| Dig (round bite) | Hold left mouse |
| Place block | Right mouse |
| Choose block | 1 to 7 |
| Blast (test destruction) | B |
| Read an inscription | E (on the glowing stone) |
| Speak a learned word | Q (at a surface) |
| Save | F5 (also on quit) |
| Free the mouse | Esc |

## Co-op

From the start menu: one player clicks **Host co-op** (UDP port 7777), friends type the host's
address and click **Join co-op**. Over the internet the host must forward UDP 7777 for now;
Steam relay comes later.

Command line works too (handy for testing two windows on one PC):

```
godot -- --host                      # or --single
godot -- --join=127.0.0.1
godot -- --host --world=mytest       # separate save folder
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
  language/     word interpreter (validated effects only)
  player/       player controller and camera
  main/         entry scene: atmosphere, HUD, menu
  art/          .vox reader, mesher, model library, rigs, art gallery
tests/        unit tests (godot --headless -s tests/run_tests.gd)
tools/        content validator, art generators and importers (see docs/art/README.md)
docs/         decisions (ADRs) and design links
```

Saves live in Godot's user folder under `worlds/<name>/`: `world.json` (seed and worldgen
version), `journal.txt` (every edit) and `chunks.sqlite` (terrain cache).

## Checks

```
python3 tools/content_validator/validate.py
godot --headless --import && godot --headless -s tests/run_tests.gd
```
