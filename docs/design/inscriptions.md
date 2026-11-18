# Inscriptions: Avarra cut into equipment

Status: first vertical slice (the Sonic Pickaxe, plus a Resonant Hammer as the second item).
Canon source: `docs/WORLD_BIBLE.md` sections 7 and 9 (draft PR #22).

## The idea in one paragraph

An Avarra word describes how reality behaves. Cut into an item, it changes how that item meets
the world. You must have found the word first (read it in two places), you cut it at an
**inscription table**, and the same glyph you read on the ruin wall is the one that appears on
the item. Not every word is a permanent upgrade: some are always on, some cost stamina when
you use them, some work only in the right situation, and some do nothing until another word
joins them.

## Words in this slice

| Concept (brief) | Avarra | Status | On items |
| --- | --- | --- | --- |
| SONIC | **Ulun**, "Ring" | proposed by Claude, approved for now (2026-11-18) | activated: resonant mining (pick) |
| FORCE | **Vuh**, "Away" | canon | compositional: only with Ulun on a hammer |
| FORTIFY | **Tahr**, "Hold fast" | proposed, approved for now | passive: wears half as fast |
| RECLAIM | **Oshen**, "Return" | proposed, approved for now | conditional: mends while you rest |

Ulun, Tahr and Oshen are cut into things, never spoken. Renaming one is a one-line change to
`display` in `content/language/lexicon.json`; saves use the stable ids (`word_sonic`...).

## The four kinds (`content/language/inscriptions.json`)

| Kind | Meaning | Example |
| --- | --- | --- |
| passive | always on | Tahr on any tool: half the wear |
| activated | right click with the item; costs stamina, has a cooldown | Ulun on a pick: rings for 10 s |
| conditional | works by itself when its condition holds | Oshen: mends by a fire or Tether |
| compositional | does nothing alone | Vuh on a hammer, waiting for Ulun |

**Compositions** are fixed pairs, looked up, never free text:

- Ulun + Vuh on a hammer = **Resonant strike**: right click, a shockwave throws back and hurts
  everything within 4.5 m.
- Tahr + Oshen = **Self-mending**: mends all the time.

The first word on an item is free; a second needs a word fragment (from the Echo Sentinel or
the Echo Heart) to bind it. Grinding a word off at the table is free.

## How a player finds and uses Ulun

1. In the echo ruin's hall of the step, Ulun is carved on the east wall (opposite Ahm). You
   can read it (E) but don't know what it means.
2. In the sentinel's court, by the east wall, stands the old builders' inscription table. Ulun
   is cut into its face. Reading it a second time gives a guess: "Ring"?
3. After the heart wakes, the quest "The Ringing Stone" points at the table.
4. E on the worktop opens the table. Pick the flint pick, press Cut beside Ulun. The word takes,
   and now you know it.
5. Right click with the pick: it rings for 10 s (18 stamina). While it rings it digs stone 2.5x
   faster and a broken block shatters up to 40 joined voxels of the same stone within 1.5 m.

## How it looks (Visual Language Guide)

- Glyphs are built from 5 x 5 cells; the same cells are used on ruin walls, on the held item and
  in the UI.
- Quiet glyphs are dark cut cells (Deep Teal `#315E68`). They light (Resonance `#62B8B1`) only
  while something is answering, cell by cell like channels filling: when you read one, at the
  table, and on the item while it rings.
- Resonant mining: grit in the colour of the struck stone, a small ripple, a low flash. The pick
  shivers slightly. No particle explosions.
- Shockwave: a ring along the ground and a brief flash.

## Where the code is

- Rules (pure, tested): `unity/Assets/Aruun/Core/Inscription/` (`InscriptionRules`, `Inscriber`).
- Host (server side, so co-op is ready): `Core/Play/HostInscriptions.cs`. Clients only ask:
  `Inscribe`, `Erase`, `UseHeld`.
- Saved: each item's words ride on its bag line (`3 flint_pick 1 240 @word_sonic`), found
  compositions as `combo` lines in the character save.
- Game: `Game/Play/InscriptionView.cs` (glyphs, held view, effects), the table panel in
  `PlayHud.cs`.
- Tests: `unity/Tests/Aruun.Core.Tests/InscriptionTests.cs`.
- Dev keys: I goes to the table; U teaches every word; K gives a kit with a pick and hammer.

## Open

- Avarra names for the other concept words (FRACTURE, PHASE, LIGHT, SEVER).
- Whether the Grand College gets its own table (any `inscription_table` block works as one).
- Armour, boots and lanterns from the brief need equipment slots first.
