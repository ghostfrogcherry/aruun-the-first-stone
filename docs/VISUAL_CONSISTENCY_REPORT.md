# Visual consistency report

2026-11-18 · checks the Unity build (main at `202dc9b`) against Bruh's *Visual Language Guide
v0.1*, summarised in [`WORLD_BIBLE.md`](WORLD_BIBLE.md) section 11.

This is an audit only. Nothing has been changed. Each fix below is a small, separate step
that keeps the game playable, and none of it needs new lore.

Sources checked: `content/blocks/blocks.json`, `content/art/palette.json`,
`content/structures/`, `unity/Assets/Aruun/Game/DayNight.cs`, `PostFX.cs`,
`Play/PlayHud.cs`, `Core/Ruins/EchoRuin.cs`, and the screenshots in the project files
(`screenshots/graphics/aruun-noon.jpg`, `aruun-m0/demo-town.png`).

## Already fits

- **A living world.** Grassland, forest, marsh, water and flowers around the spawn; red soil
  and rock on the highlands and volcanoes. It does not read as a desert planet.
- **Scale.** Olympus Mons and the other volcanoes are on the map at real positions and show
  on the horizon.
- **Language as discovery.** Words go Unseen → Seen → Guessed → Known by reading the same
  word in two places, then speaking it. That is exactly the guide's "inscription becomes
  legible" idea.
- **Ancient mechanisms answer words.** The Echo Ruin's gate, stair and heart each respond to
  one word, so the ruin already behaves like one machine.
- **Teal is reserved.** The block and palette files already say teal/violet glow is for
  magick only; normal materials never glow.
- **Warm light for inhabited places.** Lamps are amber (`lamp_glow #ffd27a`).
- **Bronze and old stone exist** as materials (`bronze`, `ancient_stone`, `bronze_fitting`).

## Needs refinement

In priority order (most visible and cheapest first).

1. **Inscriptions glow all the time.** `inscribed_stone` is solid teal `#3fb8a8` and
   emissive; `echo_seal` is emissive too. The guide says light only when a word is active.
   *Fix:* make inscribed stone a dark carved stone (Deep Teal `#315E68` or Ancient Stone with
   recessed channels) that lights in Resonance `#62B8B1` only when read, spoken or
   answering, then fades. The Echo Heart may keep a faint glow because it is a live machine.
2. **Greens are too bright and yellow.** `tall_grass #76a84a` and `grass #5e8f3c`, pushed by
   post-process saturation 1.12, read as lime (see `aruun-noon.jpg`). The guide's greens are
   cooler and calmer: Living Green `#527C62`, Pale Moss `#A5B68A`, Deep Canopy `#263B35`.
   *Fix:* retune the grass, tall grass and leaf colours toward those, and lower saturation
   to about 1.0. Keep crimson and violet trees as accents.
3. **Sky is Earth blue.** `DayNight.cs` uses a blue day sky (`0.26, 0.52, 0.88`). The
   Occlusion concept art shows a pink/salmon sky. *Open question for Bruh* (World Bible §14,
   question 7) before changing it.
4. **Violet is used for ordinary things.** `violet_bloom` ground cover, `violet_leaves` trees,
   and the palette's `glow_violet #b06cff` is a neon purple. The guide keeps violet for rare
   phenomena and Occlusions (Veil Violet `#8B78A9`). *Suggestion:* keep violet plants (the
   Bloom Fields are canon) but shift them to a dusty, non-glowing violet, and reserve the
   glowing violet for Occlusions only.
5. **The Echo Ruin is lit by amber lamps.** It is an abandoned old-builder shrine, but it uses
   the same lamp blocks as a town. *Fix:* light it with daylight shafts, the occasional
   active channel, and the player's own light; save amber lamps for places people live.
6. **Spawn lamp column.** A tall lamp column marks the Confluence. It is a dev marker, not
   part of the world; it should go when the town is built.
7. **Trees all share one silhouette.** Green, crimson and violet trees are the same round-top
   shape. The canon Skyroot Tree is 30–80 m. *Fix:* 3–4 distinct shapes per biome so forests
   read from far away.
8. **HUD is default Unity boxes.** Grey translucent boxes and plain labels. The guide wants
   dark neutral panels, warm accents, geometric motifs and teal only for active ancient
   systems. Low priority until the demo.
9. **Settlement buildings don't match the reference art yet.** The demo-town houses are domes
   and thatch. Bruh's references show timber and stone houses with blue tiled roofs,
   repaired walls and banners. There is no slate or blue roof tile block (`clay_tile` is
   red). *Fix:* add a Deep Teal slate tile and a "cultivated stone" block when the town is
   rebuilt.

## Missing visual systems

- **Occlusions:** nothing visual exists yet (boundary distortion, Earth objects intruding,
  floating chunks, light change across the seam, warning signs).
- **A glyph set for Avarra.** Words are shown as text. The guide needs one carved symbol per
  word, built from shared geometric parts, used identically on walls, tools and in the UI.
  Vuh, Ahm and Sem need glyphs first.
- **Active vs inactive states** for inscriptions and mechanisms (channels lighting in
  sequence).
- **Inscribed equipment look:** resonance chamber, metal channels, crystal; vibration effect
  for the Sonic Pickaxe.
- **Layered history in buildings:** mixed stonework and repair blocks for settlements.
- **The Grand College** as a distant landmark.
- **Title screen** with the subtle fracture in the sky.

## Conflicts with lore or language

- `content/language/lexicon.json` still calls Vuh, Ahm and Sem "(placeholder)". Bruh has
  named them established vocabulary. Remove the label (and confirm Dohm).
- `content/models/models.json` describes the Echo Sentinel as "teal Avarra glow marks it as
  magick". Fine only if it glows while awake and goes dark when it falls.
- ADR 0005 says creatures keep Aruun gravity inside Occlusions; the Occlusion rules say
  creatures may respond to local gravity. Decide when Occlusions are built.
- The concept art (class poster, architecture board) uses strong blue-cyan glow everywhere.
  Treat the written guide as the authority over that art.

## Suggested order of work

1. Inscribed stone and seals dark until active (small block and shader change).
2. Retune greens and saturation.
3. Avarra glyphs for Vuh, Ahm, Sem, carved on the Echo Ruin.
4. Echo Ruin lighting without town lamps.
5. Answer the sky colour question, then adjust `DayNight.cs`.
6. Tree silhouettes, then settlement blocks with the town rebuild (M3).
7. Occlusion visuals with the first scripted Occlusion.
8. HUD restyle before the friends demo.
