# Aruun

A co-op voxel survival RPG set on Aruun, the Living Red World: Mars when it was alive, green
and full of water. Small 0.25 m voxels, real-time play in first or third person, a world you
can dig, build and break, and an ancient language you learn word by word.

**Engine: Unity.** The game lives in [`unity/`](unity/README.md). Start there for setup,
controls, building and tests.

The first prototype was built in Godot. It is retired; its last version is commit `91ea5ff`
on `main` (`git checkout 91ea5ff` to look at it). Its rules are being ported to Unity, and
everything engine-neutral (content, art, tools, design docs) carries on unchanged.

## Where things are

| Folder | What it is |
| --- | --- |
| `unity/` | The game. Rules in `Assets/Aruun/Core` (plain C#, tested with .NET), Unity side in `Assets/Aruun/Game`. |
| `content/` | All game data: blocks, items, recipes, creatures, words, survival rules, `.vox` models and structures. Shared by every tool. |
| `tools/art/` | Python tools that build and import `.vox` art and the palette. |
| `tools/content_validator/` | Checks the content files. |
| `docs/art/` | Art guide for contributors (no code needed). |
| `docs/WORLD_BIBLE.md` | The canon: what Aruun is, what is decided and what is still open. Read before adding content. |
| `docs/design/` | Game design: mobs, behaviours. |
| `docs/decisions/` | Architecture decisions (ADRs). |

## Checks

```
dotnet test unity/Tests/Aruun.Core.Tests          # rules, worldgen, mesher, saves, language
python3 tools/content_validator/validate.py      # content files
```

Both run on every pull request.
