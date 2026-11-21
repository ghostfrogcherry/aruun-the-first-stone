# Aruun World Bible

Version 0.1 · 2026-11-18 · owner: Bruh (ghostfrogcherry)

This is the one reference for what Aruun *is*. Before adding a place, word, creature, item,
building or system, check it against this file. If something here is wrong, fix this file
first, then the content.

## How to read this

Every statement carries one tag:

| Tag | Means | Who can change it |
| --- | --- | --- |
| **[CANON]** | Established. From Bruh's own sources (the *Questbound Aruun Alpha v0.1* bible, the painted map, the flora and landscape posters) or an explicit decision Bruh made. | Only Bruh. |
| **[PROPOSED]** | A design idea that fits the canon but has not been approved. Includes ideas from ChatGPT briefs, concept art, and earlier Claude threads. | Bruh approves, edits or drops it. |
| **[BUILD]** | What the Unity game does today. A fact about the code, not about the world. | Code changes. |
| **[OPEN]** | A question only Bruh can answer. Do not fill these in with invented history. | Bruh answers; then it becomes CANON. |

Rules for anyone adding content (people or Claude):

1. Never overwrite CANON. If new work needs to contradict it, add an OPEN question instead.
2. Never quietly promote a PROPOSED idea. Ideas become CANON only when Bruh says so.
3. Don't fill OPEN questions with invented history, even as flavour text in the game.
4. Keep names from the canon spelled exactly as listed here (see the Glossary).

Sources, in order of authority:

1. Bruh's direct decisions in the project chat (dated below).
2. *Questbound Aruun Alpha v0.1* (Bruh's docx, 2026-11-16) and the map *World of the Vaari*.
   A summary lives in the lore compendium: https://claude.ai/artifact/V4eC5Vd3KiFa9H3HVt6NWt
3. The *Flora of Mars* field guide poster and the *A Living Mars* landscape posters.
4. Bruh's briefs of 2026-11-17: *Protecting Aruun's identity*, *Visual Language Guide v0.1*,
   *The Occlusion system*, and the *equipment inscription* brief.
5. Concept art (art/architecture, class poster, Occlusion image). Art shows mood; it does not
   create canon on its own.

---

## 1. Identity: what Aruun must stay

**[CANON]** Aruun is Mars when it was alive: green, wet, breathable, full of life. It is
*the Living Red World*. Red soil and rock show through, but the game must never read as a
barren red desert. Bruh: "Aruun should feel like a world that had an entire history before
the player spawned into it."

**[CANON]** The setting's maxim: *"Magick manipulates conditions. It does not replace
them."* Magick is known, studied and teachable. It has sources, costs and limits, and
weaknesses never disappear.

**[CANON]** The ancient language is not conventional magic. Its words describe properties of
reality; understanding them lets you change how things behave.

**[CANON]** The player is not the chosen one. Bruh: "They're someone who finally learns how
to understand it." The world has cultures, old conflicts, forgotten technology and mysteries
that don't revolve around the player.

**[CANON]** "The world remembers" is a recurring theme: ancient structures respond, recover
lost functions or replay fragments of the past. The player is uncovering the memory of a
planet, not just digging up buildings.

**[CANON]** What the game is (2026-11-17): an Elder Scrolls-like RPG in voxels is the main
game; survival is a secondary layer; a separate survival-only mode also exists. Inspirations:
Zelda, JRPGs, Elder Scrolls-likes, Valheim, Hytale, Seedship.

**[CANON]** Avoid drifting into generic fantasy survival with blue glowing runes. See
"Things Aruun avoids" in section 11.

### Design pillars **[CANON]** (alpha doc)

- Sandbox first.
- Hard magick with sources, costs and limits.
- Weaknesses never disappear.
- World before plot.
- Deep-time mystery.
- Culture is not biology (ancestry, culture, calling and magick are separate choices).
- North star: players understand enough to become dangerously creative, with one deeper layer
  always left to find.

---

## 2. The planet and cosmology

### Planet facts **[CANON]**

| Fact | Value | Source |
| --- | --- | --- |
| Name | Aruun, the Vaari name for Mars (roughly "red ground" or "living earth"; etymology still working) | Alpha doc |
| Gravity | 0.38 g (3.73 m/s²) | Alpha doc, Bruh 2026-11-16 |
| Day | 24 h 39 m | Flora poster |
| Air | Breathable: 78% N₂, 19% O₂, trace CO₂ and Ar | Flora poster |
| Mean temperature | 14 °C | Flora poster |
| Moons | Phobos "the Swift Watcher", Deimos "the Distant Sentinel" | Map |
| Calendar | Counted in Cycles of the Twin Moons; the map was charted in the 312th Cycle | Map |
| Geology | Real Martian geology: Tharsis volcanoes, Valles Marineris, northern seas, southern ice | Alpha doc, map |

**[CANON]** Ignore the moons drawn on the painted map when building the game world (Bruh).

**[OPEN]** The Occlusion concept image (2026-11-18) shows a huge planet in a pink sky. Is that
an intended sky object (and which one), or artistic licence? Canon moons are small.

**[OPEN]** What colour is Aruun's daytime sky? Concept art is pink/salmon; the build is
Earth blue (see the visual report).

### The Great Occlusion **[CANON]**

- Earth and Aruun are hidden from each other on purpose. From Earth, Mars looks dead; from
  Aruun, Earth looks dead.
- Avarran working name: **Keth-Avar**, "the veil between living worlds". It feeds a false
  version of each world to eyes, instruments and probes; ordinary travel reaches a
  "shadow-state", not the real world.
- Crossing to the true world takes rare knowledge, perception or alignment. The few humans
  on Aruun are High Mystics, Red Pilgrims, scholars or their descendants.
- That Earth is inhabited is not common knowledge on Aruun.

**[OPEN]** Who made the Occlusion and why (prison, quarantine, camouflage, protection,
treaty)? Are many inhabited worlds hidden from each other?

**[PROPOSED]** (from the lore compendium, optional) Red Horizon's dying Mars, Tareth, could be
the Occlusion's "shadow-state": the false dead Mars that Earth's instruments receive. Bruh
allowed Tareth as the only older world that may connect. Not adopted.

### Occlusions (local breaks) **[CANON]**

These are Bruh's rules from 2026-11-16 and 2026-11-17.

- An **Occlusion** (also called an Occlusion site or Confluence site in older notes) is a place
  where the Great Occlusion fails and two worlds briefly occupy the same space, with the laws
  of one world intruding on the other.
- **Gravity.** Aruun is 0.38 g. Inside an Occlusion, Earth gravity (1 g) applies. Crossing the
  boundary is a noticeable change. Jumps, falling, projectiles, loose objects and (potentially)
  creatures respond to the local gravity.
- **Why they're harder.** The player is an Earth human: unusually strong in 0.38 g (about a
  1.6 m jump). Inside an Occlusion that edge vanishes (about 0.6 m). Strength-based systems use
  fixed human strength and local gravity.
- **Warning signs.** Before one opens: nearby objects behave strangely, distant sounds distort,
  the air shows spatial distortion. No "INCOMING ATTACK" pop-ups. Experienced players learn to
  read the signs.
- **Look.** Not every Occlusion is a big purple portal. It should feel like reality failing to
  keep two places apart: the landscape distorts at the boundary, Earth environments and
  objects intrude, lighting and atmosphere change across the seam, gravity visibly affects
  loose objects and plants, and the boundary has one recognisable signature.
- **Reference image (2026-11-18).** A violet tear with lightning splitting the sky. Aruun's
  side is lush (waterfalls, ruins, a castle-college, pink sky); the other side is a modern Earth
  city intruding (skyscrapers, a broken highway bridge, rusted cars). Voxel chunks of ground
  float around the seam.
- **Progression.**

  | Stage | Occlusion behaviour |
  | --- | --- |
  | Early game | Scripted encounters introduce the phenomenon. |
  | First boss | Establishes the danger and significance of Occlusions. |
  | Second boss | Marks the change in how they behave. |
  | After the second boss | Occlusions become random world events, even in places that seemed safe. |

- **Title screen.** The living landscape, with a subtle dimensional fracture in the distant sky.

**[CANON]** Player premise: a human from Earth, stranded on Aruun when an Occlusion broke. The
demo opens with the player waking inside the First Stone circle as the breach closes.

**[OPEN]** Who or what are the enemies that come with Occlusions? Earth things, things from
between, or Aruun creatures driven mad by the gravity change?

**[OPEN]** Who are the first and second bosses? (The Echo Sentinel is a ruin guardian, not an
Occlusion boss, unless Bruh wants it to be.)

**[OPEN]** Naming. "The Great Occlusion" is the veil; "an Occlusion" is a local break. Is that
the wording Bruh wants, or should local breaks have their own word?

**[BUILD]** `WorldConfig.Gravity` (0.38 g) and `WorldConfig.EarthGravity` exist, and fall
damage and creature tuning already account for Earth gravity, but there are no Occlusion sites
in the world yet. ADR 0005 says creatures keep Aruun gravity inside Occlusions; the new canon
says they may respond to local gravity, so ADR 0005 needs revisiting when Occlusions are built.

---

## 3. Geography

### Regions **[CANON]**

Six cultural regions around a desert heartland, a world ocean to the north, bloom-covered ice
to the south. Bruh wants each region to have its own architecture, materials, customs,
creatures and relationship with the ancient language.

| Region | Where | Identity (alpha doc) |
| --- | --- | --- |
| **Teshari**, River Basins | north-central | Irrigation, trade, astronomy, scholarship |
| **Kharuun**, Volcanic Highlands | west | Metalworking, fortification, ancestor traditions |
| **Oravai**, Desert Caravan Lands | centre | Seasonal water, long caravan routes |
| **Sael**, Cliff-Cities and Canyon Realms | east | Vertical cities, climbing, gliding, life along Valles Marineris |
| **Nerethi**, Coastal and Island Realms | north, north-east | Maritime traditions |
| **Ilyari**, Polar Cultures | south | Cold, extreme seasons, auroras, long migrations |

**[PROPOSED]** Per-region architecture, materials, customs and language relationship. Not yet
written; this is the next lore task after the open questions below are answered.

### The three volcanoes **[CANON]**

| Volcano | Title | Map position (cells) |
| --- | --- | --- |
| Olympus Mons | Crown of Flame | x 66, y 84, height 150 m in game |
| Pavonis Mons | The Embering | x 38, y 133, 70 m |
| Arsia Mons | The Red Sentinel | x 50, y 152, 80 m |

The Tharsis Volcanic Province is still active (landscape poster). Bruh wants the volcanoes to
have histories, dangers, mysteries and real gameplay weight, not just scenery.

**[OPEN]** What are the volcanoes' histories and mysteries?

### Named places in the game map **[CANON]**

These are the 20 places in `content/world/map.json`. Their directions and distances from each
other never change, whatever the seed (so NPC directions stay true).

| Place | Title | Kind | Region |
| --- | --- | --- | --- |
| **Teshari Confluence** | (starting settlement, spawn) | settlement | Teshari |
| Teshbal | | major city and port ("Cliffside City" in art) | Teshari |
| Maerel | | settlement | Teshari |
| Kethis Crossing | | settlement | Teshari |
| Eeral Reach | | settlement | Teshari |
| Voral Reach | | settlement | Teshari |
| Jorai's Mouth | | port | Teshari |
| Suvan Delta | | settlement | Teshari |
| Elduun Marshes | | settlement | Sael in the game data (the lore compendium lists it under Teshari) |
| Khar Uln | Lavahold | major city, regional capital | Kharuun |
| Waystation of Jal Mer | | waystation | Oravai |
| Waystation of Oru | | waystation | Oravai |
| Ruins of Avarra | City of Echoes | ruin | Oravai |
| Sael Prime | The High Below | major city | Sael |
| Keth Canyon Hub | Thousand Bridges | settlement | Sael |
| Veyrin Hold | Eastern Watch | major city | Sael |
| Ilyan's Perch | | settlement | Sael |
| Nerethal | (seat of the Vaari Cartographic Conclave) | major city | Nerethi |
| Veyrith Port | | port | Nerethi |
| Ilyen's Gate | Starwake Portal | sacred site | Ilyari |

More names on the painted map, not yet placed in the game: Aruun's Heartlands, Dune Sea of
Seerakh, The Shifting Wastes (Oravai); Vast Ocean of Nereth "The Endless Expanse", Nerethian
Archipelago, Orvash Atoll, Ilessa Isles, Talvash Isles, Shal-Nereth Shoals, Lumaa Atoll
(Nerethi); Ilyari Glacial Fields, Bloomsea of Lyr (seasonal), Frostspine Range, South Polar
Bloom "The Great Renewal" (Ilyari); Ascraeus range (Kharuun, label hard to read).

Map cartouche: *"Charted in the 312th Cycle of the Twin Moons by the Vaari Cartographic
Conclave of Nerethal. For knowledge. For memory. For Aruun."*

**[CANON]** The painted map is the fixed centre of every world. Beyond its edges the land is
procedural from the seed. Major cities, towns and villages are hand-built at fixed positions;
doors lead into separate detailed town and interior instances.

**[OPEN]** Similar names: Ilyari (polar region), Ilyen's Gate (south pole) and Ilyan's Perch
(far east) are easy to confuse. "Keth" appears in Keth-Avar, Keth Canyon Hub, Kethis Crossing
and the Kethra people. Deliberate? If so, what does *Keth* mean?

**[OPEN]** Elduun Marshes: Teshari (compendium) or Sael (game map)?

**[OPEN]** In-game naming: should text say "Olympus Mons" (Earth name), "Crown of Flame"
(Vaari title), or both depending on who speaks?

### Biomes

**[BUILD]** The game has 12 biome types (`content/world/map.json`): deep ocean, shallow sea,
coast and delta, forest, marsh, grassland, desert, volcanic, highlands, canyonlands, glacial,
bloom fields.

**[CANON]** Named ecosystems from the posters: Tharsis Savannah, Polar Bloom Fields, Marineris
Rainforests, Highland Tundra, Cobalt Shorelines, Crimson Desert. Named landscapes: Nirgal
Valley, Vallis Marineris waterfalls, the Crimson Wood, the Deuteronis Sea, Arcadia Dunes,
Tharsis Volcanic Province.

**[PROPOSED]** Show the canon names in game instead of the generic biome keys, for example
grassland near Tharsis = "Tharsis Savannah", canyonlands = "Marineris Rainforests" on the
canyon floor, desert = "Crimson Desert", glacial + bloom = "Polar Bloom Fields".

**[PROPOSED, from the class poster]** "The Red Expanse", "The Verdant Basin", "The Shattered
Reach", "The Old Kingdom". These names are not in the canon; adopt or drop.

---

## 4. The Teshari Confluence (the start)

**[CANON]** The starting settlement, in the Teshari river basins. Spawn point of every world.
The road south leads toward the Ruins of Avarra; the Echo Ruin lies on it, 90 m south.

**[CANON]** Bruh: the starting town is not an isolated tutorial village; it is part of an
established world with connections to other settlements. Develop its identity before
expanding the town.

**[CANON]** (2026-11-16) "Confluence" sites are where the Occlusion fails: mixed Earth/Mars
zones and thin-place dungeons.

**[OPEN]** What makes the Teshari Confluence a Confluence? Options to choose from (not canon):
a meeting of rivers; a meeting of cultures and trade roads; a place where the Occlusion is
thin; several of these at once. The 2026-11-16 decision suggests the Occlusion meaning is at
least part of it.

**[PROPOSED]** People who could live there: traders and craftspeople, scholars from the
College, explorers returning from far regions, archaeologists studying the old structures,
people who know fragments of the ancient language.

**[BUILD]** Spawn is at the Confluence (map cell 159,102 = world 0,0). A dev lamp column marks
it. The planned M3 town there is described as a "Vaari town".

---

## 5. Peoples, cultures and institutions

### Peoples **[CANON]**

Ancestry, culture, calling and magick are four separate choices. Any people can belong to
any regional culture.

| People | Description |
| --- | --- |
| **Vaari** | Tall, light-framed generalists; the main sapient people. Art shows long-necked figures in layered robes. |
| **Shaaru** | Winged, canyon-descended. Sustained flight needs airflow, height or momentum. |
| **Nhal** | Semi-aquatic marshfolk, broad bodies, need moisture. |
| **Kethra** | Six-limbed: four to walk, two hands. No extra actions. |
| **Oth** | Subterranean: low-light senses, vibration sense, spatial memory. Bright surfaces are hard. |
| **Humans** | Rare Earth-born outsiders. Dense bones and muscle make them robust in low gravity; some sense Avarran fragments. |
| **Vael** | Rare colonial sapient: one person is a living colony that slowly adapts its body. |

**[OPEN]** The landscape art calls Teshbal a "human settlement", but the alpha doc says only a
handful of humans live on Aruun. Which is right?

**[OPEN]** Are the Kethra (and the Vaari) of the six-limbed Hexarthra lineage? The market art
seems to give Vaari small extra limbs.

### The Avarran Lyceum and the Grand College

**[CANON]** (alpha doc) The **Avarran Lyceum** is the great academy: university, research
institute, archive and political power. It treats magick as measurable, studies Avarra and
ancient ruins, keeps Earth's existence quiet, protects human High Mystics, and probably holds
sealed vaults of the oldest Avarran material. Factions around it:

| Faction | Position | Weakness or drive |
| --- | --- | --- |
| Wardens of the Lyceum | Defend the institution; controlled knowledge is safer than chaos | Secrecy, paternalism |
| The Ashen Spear | Want the Lyceum broken or stripped of its monopoly | Mixed motives: anti-elitism, anti-human zeal, religion, fear |
| The Veilborn | Pursue the origin of magick, Avarra and the Occlusion | Will follow truth even if it breaks every institution |

**[CANON]** (2026-11-17 briefs and architecture art) The **Grand College**: a monumental
castle-college that dominates its mountain, with observatories, courtyards, bridges between
towers, libraries, laboratories, archives and lecture halls; ancient structures built into
newer construction; sections that go underground or into the mountain. Recognisable from its
silhouette alone and visible long before you reach it. It has facilities for studying words.

**[OPEN]** Is the Grand College the Avarran Lyceum (same institution, common name vs formal
name), or a separate institution? **Recommendation:** make them one place, "the Grand College"
in everyday speech and "the Avarran Lyceum" as its formal name, so the existing factions and
the `lyceum_warden` character model still fit.

**[OPEN]** Where is it on the map? Candidates: near Teshbal (Teshari = scholarship and
astronomy), Nerethal, or the Ruins of Avarra.

**[CANON]** The **Vaari Cartographic Conclave** has its seat in Nerethal and charted the map.

**[OPEN]** Faction leaders, symbols, headquarters; the secret beneath the Lyceum; religions;
nations and borders; technology level; daily life.

### Culture details **[CANON]**

- Grinleaf / oumessa (placeholder names, Avarra "oumessa" = "the quiet field"): a
  cannabis-like crop, background flavour. Sages use it for focus, everyone for mental health
  and good times; one antagonist group shuns it. Gameplay: homestead or town crop, short Calm
  buff, trade good.

### Notable people

#### Mimmie-L'ou, food seller **[CANON]** (Bruh, 2026-11-21)

- **Species:** Human. **Origin:** born on Aruun, with ancestral Earth heritage. **Occupation:**
  cook and food merchant; she keeps a food stall in every major town.
- **Heritage.** Her ancestors were Earthborn people who crossed into Aruun generations ago,
  when the veil first began to weaken, through early, unstable Occlusions. Some came through
  by accident; others crossed looking for somewhere safe. Mimmie-L'ou is not an Earth visitor:
  she was born on Aruun, grew up there and considers it home. She has never needed to see
  Earth to feel she belongs.
- **Why she cooks.** Her family brought recipes across. Over generations they became part of
  local culture, adapted to native ingredients. She grows some ingredients, forages others and
  makes everything herself: a little piece of Earth history that survived the crossing.
- **Personality:** cheerful, welcoming, stubborn about food quality.
- **Story role:** one of the living connections between Earth's past and Aruun's present. Not
  a warrior, scholar or magical authority: an ordinary person whose family history is quietly
  tied to the biggest mystery on Aruun.
- **Grandma's cookbook.** An old handwritten cookbook passed down the generations. Some
  recipes call for ingredients that no longer exist, or that nobody can identify.
- **Look** (concept sheet): green dress, cream apron with a purple leaf emblem, curly brown
  hair, brown boots, wooden spoon; a stall with a purple awning, lantern, barrels, a soup-bowl
  banner and produce. Line: "Hungry? I got somethin' good cookin'!"

**[CANON]** Quest **Grandma's Cookbook**: she wants to recreate a family recipe, but one
ingredient is native to Earth. The player must find it during an Earth-Mars Occlusion; when
they return she makes the dish for the first time in generations.

**[PROPOSED]** (in the build, Bruh to confirm)
- The missing ingredient is the **tomato**. Her family has always cooked with **redroot** in
  its place, which is why the concept sheet's "tomatoes" become redroot in her everyday
  recipes. The sheet's "wheat" becomes **vermillion reed** (she grinds the seed heads).
- After the cookbook quest she keeps the tomato's seeds, so Grandmother's tomato soup joins
  her menu.
- She says "the other side", not "Earth": that Earth is inhabited is not common knowledge on
  Aruun (section 2), even if her family knows where it came from.
- Archivist Ilen recognises "tomato" as an old human family word and says such things grow
  only where the world is thin; the college "would rather people did not go looking".
- No money exists yet, so she barters: you bring ingredients, she cooks them into better food.

**[OPEN]** Does her heritage settle the "few humans" question (section 5, Peoples): are there
whole human families descended from early crossings, and how many? Is one woman at every
stall the same person who travels, or family members who all go by the name?

---

## 6. The old builders (the lost civilization)

**[CANON]** Something far older than today's peoples built structures across Aruun. Their
cities are buried; some of their machines still work. Avarra is shared by Aruun and Earth down
to grammar and irregularities, is kept on Aruun as a classical ritual tongue, works unusually
well with magick, and hints that it was designed. Avarra is tied to the deepest history and to
the Occlusion.

**[CANON]** The Echo Ruin is "a buried shrine of the old builders". Its Echo Heart is a
mechanism that answers a word; when it does, "the world remembers".

**[OPEN]** These are the questions Bruh listed. None of them has an answer yet, and no content
should invent one:

1. Who were the old builders?
2. Why did they create the ancient language?
3. Why are their cities buried?
4. What happened to their civilization?
5. Why do their machines still work?
6. What is the Echo Heart?
7. What does it mean when "the world remembers"?
8. Are the old builders the makers of the Occlusion? Are the Ruins of Avarra the origin of the
   language (the shared name suggests it)?

**[PROPOSED]** Recurring mechanic for "the world remembers": restored structures recover lost
functions, replay short fragments of past events (an "echo"), or change the land around them
(water returns to a channel, a stair rises). The Echo Sentinel is already written this way:
"an echo in the Resonant sense" that replays a fixed guard drill (art thread lore, not yet
confirmed).

---

## 7. The ancient language (Avarra)

### What it is **[CANON]**

- The language is called **Avarra**. Its words describe fundamental concepts and properties of
  reality, not spells.
- Words can have several contextual applications; the same word affects different objects
  differently.
- Combinations follow grammatical rules. Behaviour must be predictable and testable; arbitrary
  strings never execute anything.
- Players learn by finding words in the world and experimenting, not by unlocking recipes or
  reading spell scrolls. They first recognise symbols without knowing their meaning; context
  reveals meaning gradually.
- The same glyph means the same thing everywhere: on a ruin wall, a tool, a building or the UI.
  Its application can change; its identity never does.
- Eventually the language lets players change how the world itself behaves.

### Established vocabulary **[CANON]**

Bruh named these as existing vocabulary that must stay intact (2026-11-17).

| Avarra | Meaning | Where it appears |
| --- | --- | --- |
| **Vuh** | Away | Echo Ruin arch and antechamber; opens the sealed gate. In the build: a push on creatures. |
| **Ahm** | Rise | Echo Ruin antechamber and hall of the step; raises a stair. In the build: raises a stone column. |
| **Sem** | Hither | Echo Ruin court pillars; wakes the Echo Heart. In the build: a pull on creatures. |
| **oumessa** | the quiet field (grinleaf) | Plant name. |
| **Keth-Avar** | the veil between living worlds | Name of the Great Occlusion. |

**[BUILD]** `content/language/lexicon.json` still labels Vuh, Ahm and Sem "(placeholder)" and
says the words are provisional. That label is now out of date for these three and should be
removed. It also has **Dohm** = "Heavy" (`word_weigh`), learned from the Echo Heart.

**[OPEN]** Is **Dohm** ("Heavy") canon too, or still a placeholder?

**[BUILD]** How learning works now: a word is Unseen → Seen (read once) → Guessed (read in a
second place) → Known (spoken and something answered). Read with E, speak with X. The
castaway starts with no words. Words cost "gravitational debt" (the Graviturge limit).

### The inscription concept words **[CANON as a starter set, Avarra forms OPEN]**

Bruh wants these ten as the first inscription words (2026-11-17), and also said they are
design suggestions, not replacements for the existing language. So they are treated as
**concepts** (English glosses). Each still needs an Avarra word; where a canon word already
covers the concept, that word is the Avarra form.

| Concept | Meaning | Canon word that already covers it | Nearest discipline |
| --- | --- | --- | --- |
| SONIC | vibration, resonance, sound | none yet | Resonant |
| FRACTURE | break, split, weaken | none yet | Resonant / Graviturge |
| PHASE | pass through, shift | none yet | Veilwalker |
| LIGHT | illuminate, reveal | none yet | Luminist |
| BIND | hold, connect, restrain | partly **Sem** (hither) | Graviturge |
| FORCE | push, propel, impact | partly **Vuh** (away) | Graviturge |
| FORTIFY | strengthen, protect | none yet | (any) |
| RECLAIM | restore, recover | none yet | Vitalist |
| ASCEND | rise, lift | **Ahm** (rise) | Graviturge |
| SEVER | separate, cut | none yet | (any) |

**[PROPOSED]** One lexicon, one entry per word: Avarra form, glyph, English gloss, semantic
category, and what it does when spoken, inscribed or carved into a mechanism. ASCEND *is* Ahm
inscribed on an item; FORCE is Vuh; BIND overlaps Sem. New concepts get Avarra forms from Bruh
(or proposed by Claude and marked PROPOSED until approved).

**[OPEN]** Should Claude propose Avarra forms for the seven uncovered concepts, or will Bruh
coin them? Is "REVEAL" (used in the LIGHT + REVEAL example) its own word?

### Grammar **[PROPOSED]**

A first, testable grammar. Words have roles (the build already uses *action*, *thing* and
*modifier*):

- A phrase needs exactly one **action** word (what happens: Vuh, Ahm, Sem, SONIC...).
- **Modifiers** shape it (strength, direction, duration, area).
- On an item, the **item's use** is the target (a pickaxe strike, a boot's jump).
- Combinations are a lookup of defined pairs, never free composition. Examples from the brief:
  SONIC + FRACTURE = resonant mining; LIGHT + REVEAL = show hidden inscriptions and
  structures; FORCE + BIND = hold or move objects at a distance; FORTIFY + RECLAIM = slowly
  repair equipment.

---

## 8. Magick, disciplines and classes

### The seven disciplines **[CANON]** (alpha doc)

| Discipline | Domain | Limit | Signature weapon / sidearm |
| --- | --- | --- | --- |
| Resonant | Vibration and sound | Needs a medium; overuse strains hearing, balance, body | Harmonic Glaive / Tuning Hammer |
| Graviturge | Which way is down, and how hard | Gravitational debt builds and must be repaid | Fallhammer / Anchor Mace |
| Luminist | Redirect existing light | Can't create light; darkness is a hard stop | Prismblade / Mirror Dagger |
| Baromancer | Pressure in gases and liquids | Every push makes a deficit somewhere | Pressure Lance / Compression Gauntlet |
| Fluxwright | Heat transfer | Can't create or destroy heat | Thermal Chain / Heatknife |
| Vitalist | Living processes | Can't create life or casually raise the dead | Living Staff / Thornblade |
| Veilwalker | Boundaries and thresholds, including the Occlusion's edge | Rare, dangerous; needs real relationships, not free teleport | Threshold Knife / Boundary Needle |

On Earth (or inside an Occlusion): thicker air strengthens Resonants, stronger gravity first
punishes then empowers Graviturges, a brighter sun boosts Luminists.

Gear rule: gear expands options, moves thresholds, adds storage or substitutes for a missing
condition. It never makes a discipline limitless, and better gear brings new weaknesses.

**[BUILD]** Graviturge is the demo's discipline. The Fallhammer exists as an item; words cost
gravitational debt.

### The five classes **[CANON]** (2026-11-17)

Bruh: classes are established disciplines of Aruun's society, not arbitrary RPG jobs. They
decide *how* you approach problems, not which parts of the game you may play. A Cartographer
can fight; an Artificer can explore; a Naturalist can build.

| Class | Place in Aruun | Gameplay identity |
| --- | --- | --- |
| Artificer | Engineers and makers | Devices, automation, equipment |
| Linguist | Scholars of the ancient tongue | Language, inscriptions, reality manipulation |
| Veilwarden | Protectors of dangerous places | Defence, containment, survival |
| Naturalist | Students of living systems | Healing, ecology, creatures, cultivation |
| Cartographer | Explorers and surveyors | Discovery, traversal, navigation |

Each class: a distinct skill tree (small, PoE-style), unique equipment, crafting specialties,
several viable combat and utility builds, and a relationship with the College or another
institution.

**[OPEN]** How do the five classes and the seven disciplines fit together?
**Recommendation:** the alpha doc already builds a character from *Ancestry + Culture +
Calling + Magick*. Make the five classes the **Callings** and keep the seven disciplines as
**Magick**. Then "Human + Teshari + Cartographer + Graviturge" is a valid character, both
canons survive, and the links are natural: Cartographers ↔ the Vaari Cartographic Conclave;
Veilwardens ↔ the Wardens of the Lyceum and the Veilwalker discipline; Linguists ↔ Avarra
scholars at the College.

**[PROPOSED]** The "Five Paths" poster (2026-11-17) is mood art. Its class kits (anomaly
shield, modular tools, botanical tools, surveying gear) fit; its heavy blue glow does not
follow the visual guide.

---

## 9. Inscriptions (Avarra on equipment)

**[CANON]** (Bruh, 2026-11-17: "I want them")

- Inscribing a word onto an item changes how that item interacts with the world.
- Players must discover a word before they can inscribe it.
- Inscribing needs a workstation (an ancient inscription table).
- Equipment has inscription slots by design and quality.
- Words have meanings, categories, compatibility rules and effects; some combine.
- Four kinds of effect: passive, activated (uses energy), conditional, compositional. Not every
  word is a permanent upgrade.
- Inscribed items show their inscriptions. Visuals: carved geometric symbols, embedded
  crystal, metal channels, subtle emissive light only when active, animated resonance. Not
  generic enchantment glow.
- Discovery: ruins, ancient machinery, inscriptions, experiments, the Grand College.
- Technical: data-driven word definitions with stable ids, server-authoritative effects,
  inscriptions saved, compatibility validated, automated tests.

**[CANON]** Vertical slice first: the **Sonic Pickaxe**, then one second item. Done when a
player discovers SONIC, visits an inscription table, applies it to a compatible pickaxe, and
can see and use its resonant mining in the world.

**[PROPOSED]** Example items from the brief: Resonant Hammer (SONIC + FORCE, shockwaves),
Phase Axe (PHASE, bypasses certain defences), Insight Lantern (LIGHT + REVEAL), Binding
Gauntlets (BIND + FORCE), Fortified Armour (FORTIFY), Ascendant Boots (ASCEND).

**[BUILD]** Today: `tether_stone` can only be crafted by someone who knows a word of Avarra
("you must know a word of Avarra to inscribe it"). No inscription table, slots or inscribed
items yet. The `word_fragment` item drops from Echo Sentinels.

---

## 10. Life: ecology, plants and creatures

**[CANON]** Aruunian life is "not Earth life painted red": convergent shapes where physics
pushes them, alien ancestry underneath. Native complex life sits in the working kingdom
**Aresia**; its main animal lineage, phylum **Hexarthra**, started six-limbed. Creatures follow
ecology (how they move, eat, sleep, react, territory, migration), not monster conventions.

### Plants **[CANON]** (flora poster)

Kingdoms: Ferrophyta (iron plants, reds and purples), Silicophyta (glass plants), Aerophyta
(wind plants), Thalassophyta (water plants).

| Species | Habitat | Use |
| --- | --- | --- |
| Vermillion Reed | Floodplains | Building, weaving |
| Glasswort | Rocky slopes, highlands | Stores water; lenses, light-crafting |
| Dusk Blossom | Canyons, shaded cliffs | Opens at twilight; luminous nectar |
| Floatbloom | Plains, mesas | Gas bladders drift for weeks |
| Skyroot Tree (30–80 m) | Basin edges, savannas | Root mats skitter in dust storms |
| Canyon Fan | Canyon walls | Harvests mist |
| Sulfur Lily | Volcanic fields | Dyes, medicine |
| Crystal Kelp (to 40 m) | Oceans, deep lakes | Solar sea forests |

### Creatures

| Creature | Status | Notes |
| --- | --- | --- |
| Redstrider | **[CANON]** | Tall migratory herbivore of wetlands and plains |
| Skyblade | **[CANON]** | Large canyon soarer on updrafts |
| Mudback | **[CANON]** | Semi-aquatic marsh amphibian |
| Nightstalker | **[CANON]** | Twilight pursuit predator |
| Reedhopper, Glassback, Duskmoth | **[PROPOSED]** | Designed by the art thread, in the game; Bruh said they "look great" |
| Echo Sentinel | **[BUILD]** | Old-builder guard stone, boss of the Echo Ruin |

**[PROPOSED]** (art thread, unconfirmed) Vaari herders milk glassbacks for water; duskmoth
wing scales make Luminist lamp oil; Tether light doesn't attract duskmoths.

**[PROPOSED]** Bigger systems Bruh listed as missing: food chains, breeding, migration,
domestication, environmental adaptation.

---

## 11. Visual language (summary of Bruh's guide v0.1)

**[CANON]** Core identity: an ancient civilization on a world that feels impossibly old, yet
remarkably alive. The player feels small, curious, and slowly aware the world hides things
in plain sight.

**Five pillars:** a living world; history has weight; technology mistaken for magic; scale
creates wonder; mystery without constant darkness.

### Palette **[CANON]**

| Name | Hex | Use |
| --- | --- | --- |
| Deep Canopy | `#263B35` | Living ecosystems |
| Living Green | `#527C62` | Living ecosystems, cultivation |
| Pale Moss | `#A5B68A` | Healthy vegetation |
| Living Red | `#B76F52` | Martian geology |
| Ancient Stone | `#805D4B` | Old structures |
| Warm Sand | `#D5B77C` | Sand, warm stone |
| Deep Teal | `#315E68` | Ancient mechanisms (unlit) |
| Resonance | `#62B8B1` | Active ancient language or technology |
| Veil Violet | `#8B78A9` | Unusual atmosphere, deep mystery, rare phenomena |

- Greens = life. Reds and rusts = geology, iron stone, volcanoes. Teal and cyan = ancient
  mechanisms and *activated* inscriptions. Violet = rare phenomena (and Occlusions). Gold and
  amber = warm light, fire, lamps, inhabited places.
- **Cyan never means "magic" in general.** It means something is interacting with the ancient
  language or technology right now. A quiet carved inscription on a plain wall beats a whole
  glowing ruin.

### Materials **[CANON]**

Living stone (warm red, ochre, layered), ancient stone (dense, precise cuts, no visible
mortar), cultivated stone (repaired, maintained), weathered metal (bronze, copper, dark iron),
resonance crystal (translucent, subtle internal light), organic (wood, fibre, leather, bone),
living (vines, moss, roots, fungus). Materials tell stories: a Confluence wall repaired three
times in different stonework; College buildings mixing ancient and new.

### Architecture **[CANON]**

- **Settlements:** human-scale, practical; stone foundations, timber frames, tiled or slate
  roofs; gardens, farms, water channels, bridges, workshops; built to the terrain, not on a
  flat grid. Bruh's reference art: timber and stone houses with blue tiled roofs, a crystal
  fountain plaza, workshops, stone walls with blue banners.
- **The College:** see section 5.
- **Ancient structures:** repeated geometric proportions, unusual precision, consistent
  symmetry, recessed symbols and carved channels, large monolithic surfaces, mechanisms built
  into the architecture. The Echo Ruin's halls should read as parts of one machine.
- **200 m test:** from 200 m a player can tell settlement, ruin and ancient machine apart.

### The language as a visual system **[CANON]**

Symbols built from consistent geometric parts, deliberate spacing, recognisable word
silhouettes, physically carved or embedded, lit only when active, identical across ruins,
tools, architecture and UI.

### Feedback **[CANON]**

| Interaction | Feedback |
| --- | --- |
| Sonic tool | Subtle vibration, ripples, particles from the struck material |
| Mechanism activates | Geometric channels light in sequence |
| Word discovered | The inscription becomes legible or resolves |
| Hidden structure revealed | Details emerge from existing geometry |
| Danger nearby | Vegetation, particles, lighting or atmosphere change |
| Restoration | Gradual change to terrain, plants or structures |

Small, precise feedback; no particle explosions for everything.

### Characters, UI, voxel rules **[CANON]**

- Clothing: layered fabric, leather, woven materials, practical armour, regional differences,
  carried and repaired gear. Academics without generic wizard robes. Classes have silhouettes,
  not uniforms.
- UI: clear, restrained, geometric motifs, dark neutral backgrounds with warm material
  accents, teal only for active ancient systems, minimal framing, no parchment or scrolls.
- Voxel rules: silhouettes first; block-scale detail; natural terrain variation, not noise;
  layered vegetation; distinct landmark silhouettes; material transitions show age and damage;
  interactables distinct from decoration; light guides attention but doesn't fix unclear
  shapes.

### Things Aruun avoids **[CANON]**

Generic high fantasy (random castles, wizard robes, glowing swords); desert-planet clichés;
glow overload; random alien glyphs; procedural noise without ecological logic; everything
looking equally old.

### The reference test **[CANON]**

Every new asset must pass: Does it belong on a living ancient Mars? Does it show its purpose?
Does it respect the language and lore? Was it built, evolved or adapted for a reason? Does it
read at gameplay distance in voxels? Is it distinct without breaking the identity?

Note: the guide itself says it defines visual direction only; its examples are not
permission to invent new history, symbols, terms or mechanics.

---

## 12. Survival, homes and progression

**[CANON]** Survival matters but is the secondary layer; interesting, not tedious. Block
hardness and tool tiers (no chopping trees bare-handed). Valheim-like progression ramp.
Functional bases; quests and other ways to get bases besides building. Building with shape
tools that fill if you have the resources; destruction inspired by Teardown.

**[PROPOSED]** (design doc, 2026-11-16) Homesteads start by planting a **Tether**: an
Avarra-inscribed stone attuned to a discipline. **[BUILD]** `tether_stone` exists as an item.

**[PROPOSED]** Survival pressures come from the same conditions magick uses (light, heat,
water, pressure, gravity), so survival and magick are one simulation.

---

## 13. Roadmap priorities **[CANON]** (Bruh, 2026-11-17)

1. Canonical lore, geography and world identity (this file).
2. Ancient language and discovery progression.
3. Exploration, ruins and archaeological gameplay.
4. Classes, equipment and specialization.
5. Creatures and living ecosystems.
6. Settlements, NPCs and civilization.
7. Deeper survival and crafting.
8. Advanced multiplayer and shared world events.

Systems Bruh listed as missing entirely **[PROPOSED]**: a real ecosystem; subterranean
exploration (buried cities, caves, underground lakes, ancient facilities); ancient transport
(portals, mechanisms, forgotten roads, language-powered travel; Ilyen's Gate may matter most);
civilization simulation (schedules, trade, relationships, settlements that change); research
and discovery (archaeology, instruments, reconstructing lost tech); planetary events
(eruptions, storms, seasons, strange atmospheric phenomena).

---

## 14. Open questions for Bruh

The short list, in the order that unblocks the most work. Answer any of them in the project
chat and this file gets updated.

1. **Grand College = Avarran Lyceum?** (Recommended: yes, one place, two names.) Where is it?
2. **Classes vs disciplines:** classes as Callings, disciplines as Magick? (Recommended: yes.)
3. **What makes the Teshari Confluence a Confluence?** Rivers, cultures, a thin place in the
   Occlusion, or several?
4. **Avarra forms** for SONIC, FRACTURE, PHASE, LIGHT, FORTIFY, RECLAIM, SEVER: will you coin
   them, or should Claude propose some? Is Dohm ("Heavy") canon?
5. **The old builders:** any of the eight questions in section 6 you already have answers to.
6. **Occlusion enemies and the first two bosses.**
7. **Sky colour** (pink like the concept art, or blue) and the big planet in the Occlusion
   image.
8. **Humans in Teshbal**, Elduun Marshes' region, the *Keth* and *Ily-* name families,
   Earth names vs Vaari titles in game text.

---

## Glossary

| Term | Meaning | Status |
| --- | --- | --- |
| Aruun | Mars when it was alive | CANON |
| Avarra | The ancient language; also the Ruins of Avarra and the Avarran Lyceum | CANON |
| Great Occlusion / Keth-Avar | The veil hiding Earth and Aruun from each other | CANON |
| Occlusion (site) | A local break where both worlds overlap; Earth gravity inside | CANON |
| Confluence (site) | Older term for an Occlusion site; also the starting town's name | CANON, meaning OPEN |
| Old builders | The lost civilization whose ruins and machines remain | CANON (name is descriptive) |
| Echo Ruin | The buried old-builder shrine 90 m south of the Confluence | CANON (from the build) |
| Echo Heart | The Echo Ruin's mechanism; wakes to Sem | CANON, nature OPEN |
| Echo Sentinel | Guard stone of the old builders | BUILD |
| Tether | Avarra-inscribed homestead stone | PROPOSED |
| Grand College | The great castle-college | CANON, link to Lyceum OPEN |
| Vaari | The main sapient people | CANON |
| Castaway | The player: an Earth human stranded by an Occlusion | CANON |
| Mimmie-L'ou | Human food seller born on Aruun, of Earthborn descent; stall in every major town | CANON |
| Grandma's Cookbook | Mimmie-L'ou's family cookbook, and the quest to cook an Earth recipe from it | CANON |
