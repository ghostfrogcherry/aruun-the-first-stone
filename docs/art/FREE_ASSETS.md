# Free assets that fit

Checked 2026-11-16. Aruun's creatures and people are its own, so free packs help most with
ordinary things: tools, furniture, dungeon dressing, sound, fonts. Always re-read the licence
on the page before importing, and record the credit (the importer does this for you).

| Pack | What is useful | Format | Licence |
| --- | --- | --- | --- |
| [Voxel Dungeon Set](https://austin-io.itch.io/voxel-dungeon-set) by austin-io | 50 models: floors, walls, doors, chests, potions, furniture, weapons. Good for echo ruins. | zip, check for .vox | CC0 |
| [Mini Mike's Metro Minis](https://mikelovesrobots.github.io/mmmm) by Mike Judge | 400+ models. City themed, so use the trees, planters and small props, and study the 91 characters for proportions. | .vox, FBX | CC BY 4.0, credit required |
| [Voxel animals and items](https://sona-sar.itch.io/voxel-animals-items-pack-free-assets) by sona-sar | Foods and small props; grass, tree and box blocks. | OBJ + one .vox scene | Free for personal and commercial use |
| [itch.io CC0 voxel assets](https://itch.io/game-assets/assets-cc0/tag-voxel) | The place to browse for more: farm set, bird kit, fantasy items. | varies | CC0 by filter, confirm per pack |
| [Kenney](https://kenney.nl/assets) | UI, icons and sound packs. Its "Voxel Pack" is 2D tiles, not models. | PNG, OGG | CC0 |

Looked at and skipped: Palm Studio's Voxel Survival Pack (no redistribution, so it cannot live
in a public repo), VoxBox (paid, no stated licence), enkisoftware/voxel-models (Avoyd format).

## Bringing one in

```
python3 tools/art/import_vox.py ~/Downloads/chest.vox --id old_chest --category props \
    --credit "Voxel Dungeon Set by austin-io" --license CC0
```

This snaps the colours to the Aruun palette so the model matches everything else, registers
it for `ModelLibrary.instantiate("old_chest")`, and adds the credit to `THIRD_PARTY.md`.
Then look at it in the art gallery. If it looks too big or small, change `voxel_m` in
`content/models/models.json` (0.0625 is the standard; chunkier packs often want 0.125).

Rules of thumb: CC0 is always fine. CC BY needs the credit line kept. "No redistribution"
packs must not be committed to this repo. Nothing from other games or franchises.
