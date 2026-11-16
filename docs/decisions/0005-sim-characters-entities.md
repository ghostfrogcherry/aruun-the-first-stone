# 0005: The sim, characters and entities

Status: accepted (first pass)

## Decision

- Gameplay state lives in one server-owned node, `World/Sim` (`scripts/sim/sim.gd`), with
  `World/Sim/Entities` for creatures, plants, drops and structures. The world node keeps owning
  terrain and calls a small set of hooks (`dig_power`, `can_edit`, `spend_block`, `on_dug`,
  `pay_for_word`, `schedule_landing`, `save_all`).
- Clients send requests (`request_attack`, `request_craft`, `request_use`, ...) and the server
  decides with pure rule classes (`CombatRules`, `Crafting`, `Farming`, `Conditions`, `Collapse`,
  `CreatureMotion`). These have no scene dependencies, so they are unit tested.
- The world is the server's; a character belongs to its player but is stored by the host in
  `characters/<guid>.json`. The guid comes from `user://player.json` on the player's machine.
- Survival is a data-driven Conditions system: each condition is a 0 to 100 value with rates
  switched by flags (night, sheltered, near_heat, sprinting, earth_gravity, ...) and bands that
  apply modifiers. A ruleset is JSON; Hardcore extends Light. Gravity debt and Earth's weight are
  ordinary conditions.
- Entities are replicated with four RPCs (spawn, despawn, moves, event). Creatures move
  kinematically on the voxel grid on the server at 20 Hz; clients only interpolate.
- Crops store when they were planted; their stage is computed from the clock, so nothing ticks
  per plant and unloaded farms keep growing.
- A loose piece becomes a `clear` op with a `drop`, then a `put` op when it lands. Both are
  journalled, so a rejoining player replays the same collapse.
- Gravity: nothing hard-codes it. The player's strength is fixed (jump speed, stamina), and the
  sim reads the body's local gravity to raise the `earth_gravity` flag inside Occlusion sites.

## Not done yet

- Creature art exists for more species than have behaviour (reedhopper, glassback, duskmoth,
  echo sentinel).
- Creatures use Aruun gravity everywhere, including inside Occlusion sites.
- No carry weight, armour, ranged weapons or the other six disciplines.
- Character saves trust the host; there is no protection against a host editing them.
