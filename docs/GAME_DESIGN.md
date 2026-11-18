# Aruun Game Design: the core loop

Version 0.1 · 2026-11-18 · owner: Bruh (ghostfrogcherry)

This is the plan for how Aruun *plays*. The [World Bible](WORLD_BIBLE.md) says what Aruun *is*;
this file says what the player does in it, why each system exists, how the systems feed each
other, what is already built, and what to build next. When the two disagree, the World Bible
wins on lore and this file wins on gameplay.

It uses the same tags as the World Bible:

| Tag | Means |
| --- | --- |
| **[CANON]** | Bruh decided it (date given). Only Bruh changes it. |
| **[PROPOSED]** | A design idea that fits the canon. Bruh approves, edits or drops it. |
| **[BUILD]** | What the Unity game does today. |
| **[OPEN]** | A question only Bruh can answer. Not to be filled with invented answers. |

Numbers in PROPOSED sections (seconds, metres, damage) are starting points for playtesting,
not promises. They exist so the ideas are concrete enough to build and test.

---

## 1. The one idea

**[CANON]** (Bruh, 2026-11-18)

> Explore a living world, uncover the rules that govern it, learn to use those rules, and
> venture farther into a world that is becoming increasingly unstable.

Aruun is an **exploration RPG** where building, survival, combat, ancient technology and
discovery all feed into each other. It must not feel like Minecraft with quests bolted on.

**[CANON]** The central reward is **discovery**: not loot, not XP, not bigger numbers. The most
memorable reward is realising that something you didn't understand now makes sense:

- a symbol you recognise,
- a mechanism you can finally activate,
- a mountain you can now climb,
- a creature whose behaviour you finally understand,
- an Occlusion you recognise early enough to prepare for.

> **Aruun rewards curiosity with capability.**

### The design test

Every feature gets checked against three questions. If a feature fails all three, it doesn't
go in.

1. **Does it create a new possibility?** (A discovery must open a door, not fill a counter.)
2. **Does it feed another system?** (Mining feeds inscriptions; inscriptions open ruins; ruins
   open regions; regions bring Occlusions; Occlusions bring materials that feed mining.)
3. **Does it respect what the player has learned?** (A player who reads the signs, studies an
   enemy or understands a word should be visibly better off than one who didn't.)

---

## 2. The target look

**[CANON]** (Bruh, 2026-11-18: "This is how detailed I want the game to be. And how I want it
to look.") Three concept boards, nine scenes: the Grand College, the Teshari Confluence, the
Echo Ruin, and Occlusions. They are the visual target for the whole game.

What the boards ask for, written as things we can build and check:

| Area | Target |
| --- | --- |
| Voxel scale | Small, dense voxels. Characters and props read as voxel but finely detailed (cloth folds, backpacks, lanterns). Our 0.25 m voxels are the right family; characters need more detail than the current rigs. |
| Sky | Pink, orange and violet sunset sky with layered clouds, and a **huge red planet** low on the horizon. |
| Landscape | Tall cliffs and plateaus, many waterfalls, a river through the valley, stone arch bridges, lush trees, purple, red and pink flora, a smoking volcano in the distance, depth haze between layers. |
| Grand College | A gothic, many-spired castle on cliffs above waterfalls. Visible from far away; it is the landmark the player orients by. |
| Ruins | Mossy dark stone, carved pillars with cyan glyph panels, waterfalls inside the Echo Ruin, a great stair up to a glowing doorway. |
| Occlusions | A violet tear with lightning splitting the sky, chunks of ground floating around it, red-eyed golems and beasts, purple light washing over everything. |
| Hero | Third-person castaway: hood or cape, big backpack, lantern, weapon on the back. |
| Lighting | Strong warm sunlight, volumetric shafts, fog in valleys, bloom on lights, cool shadowed interiors. |

**The HUD in the boards** **[CANON as target]**:

- Top left: character portrait with level, and three bars: **red** health, **blue**
  energy/stamina, **green** a third resource (see section 6.6).
- Top centre: a **compass strip** (W, N, E) with objective markers.
- Top right: **gold quest title** (the place, e.g. "The Echo Ruin") with the current objective
  below it ("Decode the inscription").
- Corner: a **round minimap**.
- Bottom centre: an **8-slot hotbar** with item counts.
- In the world: an **"E Examine"** prompt, and inscription popups that list what the player
  knows ("Resonance Inscription: Vuh = Away, Ahm = Rise, Sem = Hither").

**[PROPOSED]** How this fits the Visual Language Guide (World Bible §11):

- The **cyan glyph glow** in the art is allowed because those ruins are *active*: the guide says
  cyan appears only when the language or old technology is working. Dormant ruins stay carved
  stone. That makes waking a ruin a visible reward.
- The **violet** of Occlusions is the guide's Veil Violet. It should be the only strong purple
  in the game, so a player learns "violet light = something is wrong".
- The HUD keeps the guide's "restrained geometric UI, no parchment": thin gold lines and dark
  panels, as in the boards.

**[OPEN]** The big red planet in the sky. Is it a real body in Aruun's sky (a moon? Earth seen
through the veil? something else?), or art licence? It is now on screen in every outdoor shot,
so it will need a lore answer. (World Bible §14 question 7: the sky colour is now answered as
pink by these boards; the planet is still open.)

---

## 3. The loop at three time scales

A good loop works whether you play for one minute or one hundred hours. Aruun's loop has three
layers that nest inside each other.

### 3.1 The moment (seconds to a few minutes)

```
notice something  →  investigate it  →  act on it  →  learn something
      ↑                                                     │
      └──────────── the world now looks different ──────────┘
```

Examples:

- You see a glyph on a pillar → press E to examine → it's a symbol you've seen before → it is
  now *Guessed* and the popup offers a meaning.
- A nightstalker circles you → you lock on and watch → it always feints before the real lunge
  → next time you dodge the second strike, not the first.
- Pebbles start drifting upward near your feet → you stop mining → the air ripples → you run,
  and an Occlusion opens behind you.

### 3.2 The expedition (30 to 90 minutes)

**[PROPOSED]** The expedition is Aruun's main unit of play: leave a safe place, go somewhere, come
back changed.

| Step | What happens | Systems used |
| --- | --- | --- |
| 1. Prepare | Pick gear, inscribe tools, cook food, decide where to go and why. | Crafting, inscriptions, survival, map |
| 2. Travel | Cross terrain, find landmarks and shortcuts, meet creatures. | Exploration, traversal, ecology |
| 3. Discover | Find a ruin, a settlement, a rare deposit, a new creature, a hidden path. | Exploration, language, NPCs |
| 4. Overcome | Fight, solve a mechanism, mine something hard, survive a hazard or an Occlusion. | Combat, language, mining, survival |
| 5. Return | Bring back materials, words, knowledge, maps, stories. | Bag, journal, map |
| 6. Apply | Upgrade gear, inscribe a new word, improve your home, unlock a skill, open a new route. | Crafting, inscriptions, base, skills |

The return trip matters: what you bring home makes the *next* expedition reach farther. That is
the whole loop in one line.

### 3.3 The campaign (tens of hours)

The five acts in section 9. Each act changes **what kind of expedition you can go on**, not just
how strong you are.

---

## 4. Every activity feeds another

**[PROPOSED]** This table is the heart of the design. Each row is an activity; the columns say
what it gives to the others. If a new feature can't fill in at least two cells of its row, it
is a chore, not a system.

| Activity | Gives to... | Example |
| --- | --- | --- |
| **Exploring** | Language, mining, story, travel | You find a buried arch (a ruin), a vein of resonant crystal (a material), a Vaari herder (a quest), a cliff path (a shortcut). |
| **Reading ruins** | Inscriptions, mechanisms, combat | A word read in a ruin can be spoken at a mechanism, cut into a tool, or used against an enemy that is weak to it. |
| **Mining** | Crafting, inscriptions, ruins | Better materials make better tools and more inscription slots; deep mining uncovers buried rooms. |
| **Crafting** | Mining, combat, survival, building | Tools open harder deposits; weapons and armour open dangerous places; kits make long trips possible. |
| **Inscribing** | Mining, combat, traversal | An Ulun pick fractures stone a plain pick can't; Ahm boots reach a ledge you couldn't climb. |
| **Fighting** | Materials, knowledge, safety | Creatures drop parts for gear; bosses teach a rule of the world; cleared areas become safe routes. |
| **Studying creatures** | Combat, farming, travel | Knowing a creature's habits tells you when to avoid it, how to fight it, and what it can give you. |
| **Surviving** | Exploration range | Food, warmth and rest set how far and how long you can go. Better survival = longer expeditions. |
| **Building a home** | Survival, crafting, safety | A Tether home gives safe rest, workstations, storage and a place to recover between trips. |
| **Talking to people** | Story, trade, knowledge | NPCs sell what you can't make, point to places on the map (true directions), and teach meanings you couldn't guess. |
| **Surviving an Occlusion** | Materials, knowledge, story | Earth-side objects and Occlusion-only materials; each one teaches you the signs a little better. |

### The example expedition

**[CANON]** (Bruh, 2026-11-18) One trip that touches almost every system:

1. While exploring, you find a rare material.
2. You bring it home and use it to improve your Sonic Pickaxe.
3. The improved tool lets you reach a buried ruin.
4. Inside, you discover a word that activates an ancient mechanism.
5. The mechanism opens a shortcut to a new region.
6. While exploring that region, an Occlusion occurs.

**[PROPOSED]** Concretely, using things that exist or are planned:

| Step | Concrete version |
| --- | --- |
| Rare material | **Singing quartz** (placeholder name): a crystal vein in cliff faces that hums when you're close. Too brittle to mine with a flint pick: it shatters into nothing. |
| Improve the pick | At the inscription table, singing quartz lets the Ulun (SONIC) pick **tune** to a material. Tuned to the old builders' stone, it can break *dressed stone*, which plain picks can't. |
| Buried ruin | South of the Echo Ruin, a sealed passage of dressed stone. It was always visible; now it's reachable. |
| The word | Inside: FRACTURE's glyph (Avarra form OPEN) on a cracked pillar, plus a mechanism that only answers a spoken word. |
| The mechanism | An old-builder **lift** (or gate) that answers Ahm. It raises a stone road out of a gorge. |
| New region | The road crosses into the next basin: new biome, new creatures, a settlement. |
| Occlusion | During the first night there, the first scripted Occlusion (section 7). |

---

## 5. Progression through understanding

**[CANON]** (Bruh, 2026-11-18) Progression comes from understanding Aruun. The ancient language
is the strongest tool for this:

1. Discover words in ruins.
2. Learn their meanings through context and experimentation.
3. Apply knowledge to mechanisms and equipment.
4. Unlock new ways to interact with the environment.
5. Discover combinations and more advanced applications.

### 5.1 How a word is learned

**[BUILD]** A word goes **Unseen → Seen** (read once) **→ Guessed** (read in a second place)
**→ Known** (spoken, and something answered). Read with E, speak with X. The castaway starts with
no words.

**[PROPOSED]** Three ways to use a Known word, each discovered separately:

| Use | Where | What it feels like |
| --- | --- | --- |
| **Spoken** | Anywhere, costs the discipline's resource (gravitational debt for a Graviturge) | A short, strong effect: push, lift, pull. |
| **Carved into a mechanism** | Ruins, gates, lifts, old roads | The world responds: the ruin "remembers" its function. |
| **Inscribed on equipment** | Inscription table | A lasting change in how the item meets the world. |

A player may know a word as a spoken effect and not yet realise it can go on a tool. The
discovery "oh, Ahm goes on boots" is itself a reward.

**[PROPOSED]** Meaning is learned, not given. The first popup for a Seen word shows only the
glyph. Guessed shows a hint drawn from where you read it ("near a raised stair: *rise?*"). Known
shows the meaning. Some meanings are only completed by an NPC scholar or the Grand College, so
people matter.

### 5.2 Combinations

**[PROPOSED]** (World Bible §7 grammar) Combinations are a fixed table of defined pairs, never free
text. Finding a working pair is a discovery in its own right; the journal records it. Pairs
already defined: Ulun + Vuh on a hammer = Resonant strike; Tahr + Oshen = Self-mending. Planned
from the brief: SONIC + FRACTURE = resonant mining; LIGHT + REVEAL = show hidden inscriptions;
FORCE + BIND = move objects at a distance.

### 5.3 Equipment as technological understanding

**[CANON]** (Bruh, 2026-11-18) A better pickaxe isn't just a pickaxe with more damage. It might use
resonance to fracture certain materials, opening deposits that were impractical to mine.

**[PROPOSED]** So tools progress in two directions:

- **Tier** (what the tool is made of): hand → flint → bronze → … Tier sets *speed* and
  *durability*, the Valheim-style ramp.
- **Understanding** (what is inscribed on it): Ulun, FRACTURE, tuning... Inscriptions set *what
  the tool can do at all*.

The interesting gates are understanding gates. Example pickaxe ladder:

| Pick | How you get it | What it opens |
| --- | --- | --- |
| Bare hands | Start | Soil, sand, loose gravel. |
| Flint pick **[BUILD]** | Crafted from flint and timber | Stone, ore near the surface. |
| Ulun pick **[BUILD]** | Ulun (SONIC) inscribed | Resonant mining: ring the pick and nearby stone of one kind breaks in a burst. |
| Tuned Ulun pick | + singing quartz | Dressed old-builder stone; sealed passages. |
| Ulun + FRACTURE pick | Second word, found in the buried ruin | Fractures along seams: whole veins drop at once; dense deep rock becomes practical. |

The same idea applies to every tool family: an axe that **Severs** (cuts a whole tree at its
base), a lantern that **Reveals**, boots that **Rise**.

### 5.4 Skills and disciplines

**[BUILD]** The Graviturge tree (13 nodes, 3 branches, P to open). Points come from words (1),
quests (1), guardians (2).

**[PROPOSED]** Skill points come mostly from *understanding* (new words, first combinations, bosses,
first discoveries of a region) rather than from killing. A player who explores and reads levels
up; a player who only grinds doesn't. This is how "discovery is the reward" shows up in numbers.

**[OPEN]** (World Bible §14 question 2) Classes as Callings and disciplines as Magick. The loop
works either way; the recommendation stands.

---

## 6. Combat

**[CANON]** (Bruh, 2026-11-18) Combat rewards **preparation, equipment choices, movement and class
development**:

- Different enemy **behaviours**, not just different health pools.
- **Environmental** advantages and hazards.
- Equipment with meaningful specialisations.
- Class abilities that create different approaches.
- Bosses that **teach the player something about the world**; they change what the player
  understands or can do, not just unlock the next health tier.

**[CANON]** Zelda-like combat with **lock-on (Z-targeting)**, so dodging matters. If you don't
use what's available to you, you get mollywhopped.

### 6.1 Lock-on

**[PROPOSED]** How lock-on works, from the player's side:

- **Hold or tap to lock** (middle mouse or a bindable key; left trigger on a controller).
  The nearest enemy in front of you gets a marker. The camera keeps you and the target both on
  screen.
- While locked, **movement becomes strafing**: you circle the enemy instead of turning away.
  Attacks always go toward the target.
- **Flick to switch** targets (mouse flick or right stick).
- Lock **breaks** when the target dies, goes out of sight for 2 s, or goes beyond ~25 m.
- Works in first and third person; third person is the main combat view.
- Unlocked combat still works (for crowds and quick fights), but bosses and elite enemies are
  tuned assuming you lock on.

### 6.2 The verbs

**[BUILD]** Light swing (LMB), heavy swing (R), guard (hold Q), dodge (Left Alt). Combat
numbers live in `Core/Combat/CombatRules.cs` (stamina costs, poise, heavy wind-up).

**[PROPOSED]** The full set, Zelda-style:

| Verb | Locked-on version | Purpose |
| --- | --- | --- |
| Light attack | Quick combo, 3 hits | Safe damage |
| Heavy attack | Slow, breaks guard and poise | Punish openings |
| Guard | Blocks from the front, costs stamina | Safety, at a cost |
| **Parry** | Guard in the last ~0.2 s before a hit: the enemy staggers | Reward for reading the tell |
| **Dodge** | Sidestep, back-hop or roll by stick direction; short invulnerability (~0.25 s) | The core defensive skill |
| **Perfect dodge** | Dodge at the last moment: time slows briefly, a free counter-attack | The "flurry rush" moment |
| Word / ability | Spoken word or class ability on the target | Changes the fight's rules |
| Use item | Food, bandage, bomb, thrown item | Preparation pays off |

### 6.3 Why you get mollywhopped if you don't

**[PROPOSED]** Rules that make dodging and lock-on necessary without being unfair:

- **Every dangerous attack has a tell**: a wind-up pose, a sound, a flash in the eyes (the red
  eyes in the concept art). A player who watches can always react.
- **Enemies hit hard.** Three or four mistakes kill you early on. Armour reduces this but never
  removes it ("weaknesses never disappear").
- **Guarding is not enough.** Heavy enemy attacks break guard; some can't be blocked at all
  (grabs, slams, Occlusion gravity crushes). Those must be dodged.
- **Stamina is shared** by attacks, guard and dodges. Spam any of them and you're open.
- **Enemies punish standing still** (area slams, flanking packs) and punish **running straight
  away** (lunges, ranged throws).

### 6.4 Enemy behaviours

**[PROPOSED]** Each enemy is a combination of a few behaviour types, so learning one type helps
against many enemies:

| Behaviour | What it does | How you beat it |
| --- | --- | --- |
| Feinter | Fakes an attack before the real one (nightstalker) | Wait for the second movement |
| Charger | Long straight rush | Sidestep at the last moment |
| Shielded | Blocks from the front (echo sentinel) | Circle while locked on; heavy breaks it |
| Pack | Attacks in turns, flanks | Keep your back to a wall; lock on the leader |
| Ranged | Throws or spits from distance | Close in by dodging forward |
| Burrower / flier | Disappears, reappears | Read the ground or shadow tell |
| Gravity-bound | Strong only in Earth gravity, or only in Aruun gravity | Change *where* you fight |

The four mobs from the art thread and the four canon creatures already have behaviour designs
in [design/mob_behaviours.md](design/mob_behaviours.md); they slot into this table.

### 6.5 The environment as a weapon

**[PROPOSED]** Every fight happens somewhere, and the place should matter:

- **Low gravity (0.38 g) is your advantage.** Earth humans jump ~1.6 m on Aruun: jump attacks,
  leaping onto ledges, and fighting from height work in your favour.
- **Occlusion gravity (1 g) takes it away.** Jumps drop to ~0.6 m, falls hurt more, thrown items
  drop fast. Fighting at the edge of an Occlusion is a choice: lure enemies across the boundary
  where *they* are slowed (some are) or *you* are.
- **Water, cliffs, fire and loose rock**: knock enemies off edges with Vuh, drop rock on them
  with a Fallhammer slam, fight in shallow water to slow chargers.
- **Destruction**: walls and pillars break (Teardown-inspired), so cover can be taken away, and
  so can the enemy's.

### 6.6 Bars and resources

**[PROPOSED]** The three HUD bars from the concept art: **red = health**, **blue = stamina**
(attacks, guard, dodge, activated inscriptions), **green = the discipline resource** (for a
Graviturge, *how much gravitational debt you can still take on*). The survival layer (hunger,
warmth) shows as icons near the bars, not as more bars.

### 6.7 Bosses

**[CANON]** Bosses change what the player understands or can do.

**[PROPOSED]** Every boss follows the same recipe:

1. **A rule of the world** it is built around (resonance, gravity, the veil...).
2. **A fight that teaches that rule.** You can't win by hitting harder; you win by using the
   rule.
3. **A change afterwards**: a new word, a new ability, a new region, or a change to the world
   itself (as with boss 2 and Occlusions).

**[BUILD]** The Echo Sentinel guards the Echo Ruin and drops `word_fragment`.

**[PROPOSED]** The Echo Sentinel as the *tutorial guardian* (not boss 1): it teaches that old
builder machines answer the language. It takes almost no damage until you speak Vuh to knock it
off its plinth, after which it's vulnerable. Reward: Dohm or the next word.

**[OPEN]** (World Bible §14 question 6) Who are boss 1 and boss 2? Below are only the *job* each
boss has to do in the loop, so whatever Bruh picks fits:

| Boss | Job in the loop | What the fight should teach | What changes after |
| --- | --- | --- | --- |
| **Boss 1** | Establish the danger and significance of Occlusions. | Earth gravity: you can't jump-fight it the way you're used to; you must use footing, dodges and the boundary. | You can **sense** Occlusion warning signs from farther away (the first tier of recognising them). |
| **Boss 2** | Mark the change in how Occlusions behave. | Something about the veil itself. | **Random Occlusions begin** across the world (section 7.3). The world is now unstable. |

---

## 7. Occlusions as world events

**[CANON]** (World Bible §2 and Bruh, 2026-11-18) Occlusions are not ordinary dungeons. They are
dynamic world events where Earth and Mars overlap: Earth gravity inside, warning signs before,
no pop-ups.

### 7.1 Warning signs

**[PROPOSED]** A ladder of signs, so attentive players get more time. Times are from the first
sign until the boundary snaps shut around the area.

| Stage | ~Time left | What you notice |
| --- | --- | --- |
| 1. Wrongness | 60 s | Loose pebbles, leaves and dropped items drift or roll uphill. Creatures go quiet or flee. |
| 2. Distortion | 40 s | Distant sounds stretch and warp; a low hum. Air ripples like heat haze in one direction. |
| 3. Bleed | 20 s | Flickers of the other side: a glimpse of concrete, a car horn, a streetlight. Violet light at the edges of the screen. |
| 4. Tear | 0 s | The violet tear opens with lightning; chunks of ground lift and float; the boundary is set. |

Recognising stage 1 is a learned skill. After boss 1 (section 6.7) the signs start farther out.
Some gear (a Luminist lens, a Cartographer's instrument) could extend this further.

### 7.2 Inside an Occlusion

**[PROPOSED]**

- **Earth gravity (1 g)** for the player, thrown items, loose blocks, and creatures that cross in.
- **The boundary is visible** once set: a shimmering wall where light, colour and gravity change.
- **Earth intrudes**: patches of road, rusted cars, a broken highway span, a lamppost, sliced
  through Aruun terrain at the seam (from the Occlusion reference image).
- **Intruders** (enemy design OPEN): the red-eyed golems and beasts of the concept art.
- **It ends**: the Occlusion lasts a few minutes, or until something at its heart is dealt with.
  Then it **leaves a scar**: the Earth pieces stay as minable, explorable debris. This is where
  Earth materials (steel, glass, concrete, plastic) come from: rare, useful, and a little
  unsettling.
- **Getting out**: walk out of the boundary before it closes, or survive until it ends.
  Physically leaving is always possible, but your jump is weak and the ground may be floating.

### 7.3 The three phases

**[CANON]** (Bruh, 2026-11-18)

| Phase | Occlusions are... | Purpose |
| --- | --- | --- |
| **Before boss 1** | Scripted encounters, carefully introduced enemy types. | Teach what an Occlusion is and how Earth gravity feels. |
| **Boss 1 to boss 2** | Still scripted, larger and harder. | Controlled difficulty and story progression. |
| **After boss 2** | **Random events**: unpredictable locations, can interrupt exploring or gathering. | Pressure to prepare and respond. The world is unstable. |

**[PROPOSED]** Rules for random Occlusions so they feel fair, not cheap:

- They never start inside a claimed Tether home's radius (homes are safe, at least at first).
- They always run the full warning ladder: never instant.
- A "pressure" value rises the longer you're out in the world and falls when you rest at home,
  so long expeditions are riskier than short ones.
- Rate and size can be set by the ruleset (light: rare and small; hardcore: frequent and large).

### 7.4 The decision we're designing for

**[CANON]** (Bruh, 2026-11-18) A player is halfway through mining, sees the environment begin to
distort, and has to decide whether to finish collecting resources or get out. No quest marker.

**[PROPOSED]** For that decision to be interesting, both choices need a pull:

- **Stay**: the deposit is rare (you came a long way), a partial dig drops nothing, and some
  materials *only* appear while an Occlusion is near (veil-touched crystal).
- **Go**: inside, you're weaker (gravity), intruders come through, and dying far from home costs
  you the bag.

---

## 8. Exploration and discovery

**[CANON]** (Bruh, 2026-11-18) Exploration is the biggest source of discovery. Players should find:
new biomes and geographical features; settlements and interesting characters; rare materials;
ancient ruins and forgotten mechanisms; new creatures and their habitats; hidden paths,
shortcuts and landmarks.

### 8.1 Discovery chains

**[CANON]** A good discovery creates a new possibility. A buried structure reveals a symbol; the
symbol unlocks a mechanism; the mechanism reveals a new area.

**[PROPOSED]** Every placed discovery is authored as a **chain link**: what it *needs* and what it
*opens*. That gives us a simple data shape to build and test:

```
discovery:   buried_passage_south
  needs:     tool can break dressed_stone      (tuned Ulun pick)
  gives:     word_fracture (Seen), mechanism lift_gorge
  opens:     route teshari_to_next_basin
```

The content validator can then check that every chain is completable and nothing is gated on
itself.

### 8.2 Knowledge gates, not key gates

**[PROPOSED]** Doors in Aruun open because you *understand* something, not because you carry a
key item. Gates come in kinds:

| Gate | Opened by |
| --- | --- |
| Language | Speaking or inscribing a word you know |
| Tool | A tool that can do something (break dressed stone, cut through roots) |
| Traversal | Being able to reach it (Ahm boots, a glider, a lift, a shortcut) |
| Environment | The right conditions (night, low water, after an Occlusion scar) |
| Social | Trust or knowledge from an NPC |

### 8.3 The journal

**[PROPOSED]** The journal records **understanding**, not checklists: the glyphs you've seen
(and where), meanings you've guessed, creature notes ("feints before striking"), combinations
that worked, Occlusion signs you've witnessed. Nothing in it is a "3 / 50 collected" counter.

### 8.4 The map

**[CANON]** The painted map is the fixed starting region; beyond it the world is procedural from
the seed. Towns sit at fixed distances and directions so NPC directions stay true.

**[PROPOSED]** The map starts blank and fills as you travel. A Cartographer fills it faster and
can mark things for others in co-op. Landmarks (the Grand College, the volcanoes) are visible
from far away so players can navigate by sight first and by map second.

---

## 9. The five acts

**[CANON]** (Bruh, 2026-11-18) The campaign escalates in five acts without becoming a linear
corridor.

| Act | Bruh's goal | **[PROPOSED]** What the player does | **[PROPOSED]** Systems introduced | **[PROPOSED]** Act ends when |
| --- | --- | --- | --- | --- |
| **1. Establish yourself** | Learn the world, basic equipment, survival, the starting region. | Wake at the First Stone. Reach the Teshari Confluence. Craft flint tools, eat, sleep, meet people, claim the old steading. | Survival, mining tiers, crafting, combat basics, lock-on, town, first quests, first home. | You read your first word. |
| **2. Discover the old world** | Investigate ruins, meet the language, first forgotten technology. | Echo Ruin, first words, first inscription (Ulun pick), first mechanisms, the Echo Sentinel. First scripted Occlusion glimpsed from afar. | Language, inscriptions, ruins, mechanisms, journal. | The buried passage opens a road out of the basin. |
| **3. Become capable** | Class abilities, better equipment, more dangerous places, larger threats. | Second region, the Grand College, skill trees, combinations, scripted Occlusions up close. **Boss 1.** | Classes and skills, combinations, Earth-gravity fights, travel. | Boss 1 falls and you can sense Occlusions. |
| **4. Confront the instability** | Defeat the second boss and enter random Occlusions. | Harder regions, a larger Occlusion arc, **boss 2**. | Occlusion scars, Earth materials, expedition pressure. | Boss 2 falls; random Occlusions begin. |
| **5. Understand Aruun** | Deeper mysteries, the Earth–Mars relationship, the most significant ancient systems. | Open world with random Occlusions; ancient transport (Ilyen's Gate), the old builders' story, the veil itself. | Ancient travel, world-changing words, late-game ruins. | Open-ended, with a main story ending (OPEN). |

Acts 1 and 2 are roughly what the current demo covers. The demo (M4) ends near the end of act 2.

**[OPEN]** Where the story ends, and whether the player can (or wants to) go home to Earth.

---

## 10. Survival and homes in the loop

**[CANON]** Survival is the secondary layer: interesting, not tedious. Block hardness and tool
tiers. Functional bases. A separate survival-only mode exists.

**[PROPOSED]** Survival's job in the loop is to set **how far and how long** you can go:

- Food and rest set expedition length. Better food = longer trips.
- Warmth, water and air matter in specific biomes (cold heights, dry plateaus), so each new
  region asks for new preparation. That's survival as a *gate* for exploration, not as a chore.
- The **Tether home** is the safe end of every expedition: rest, workstations (inscription table,
  bench), storage, and later the only place random Occlusions can't reach.
- Homes can be **found and claimed** (the old steading) as well as built.

**[BUILD]** Light and hardcore rulesets, hunger/conditions, campfire, bench crafting, farming
(grinleaf, redroot), shape building, the old steading as the first claimable home.

---

## 11. What is built and what isn't

As of main at PR #29 (2026-11-18). Rough status for planning, not a test report.

| System | Built | Still to build for the target |
| --- | --- | --- |
| World | Painted-map region, biomes, 0.25 m voxels, day/night, far terrain, PostFX | Pink sky + big planet, waterfalls, cliffs and plateaus at concept-art scale, volumetric fog, distant volcano |
| Town | Teshari Confluence with NPCs, dialogue, trade | Grand College (landmark), more settlements at fixed directions |
| Mining & crafting | Tool tiers, block health, bench, campfire, recipes | Bronze-tier and beyond, rare materials (singing quartz), tuning |
| Language | 7 words, learn by reading, speak with X, Echo Ruin with word doors | Meaning hints, more mechanisms, FRACTURE and others, journal of understanding |
| Inscriptions | Inscription table, Ulun pick, Resonant hammer, Tahr, Oshen | Inscription slots by quality, boots/lantern/axe items, more pairs |
| Combat | Light, heavy, guard, dodge, poise, creature brains | **Lock-on**, strafing, parry, perfect dodge, enemy tells, behaviour mix, bosses |
| Creatures | 7–8 creatures with behaviours, Echo Sentinel | Occlusion intruders, ecology (habitats, food chains) |
| Skills | Graviturge tree (13 nodes) | Class trees (Callings), points from discovery |
| Homes | Shape building, Tether, old steading | Base functions (storage, workstations, safety radius) |
| Occlusions | Earth-gravity constant, fall damage tuned | **Everything**: warning signs, boundary, Earth intrusions, intruders, scars, director |
| Bosses | Echo Sentinel (guardian) | Boss 1, boss 2 (both OPEN) |
| HUD | Vitals, bag, quest panel, map screen | Portrait + 3 bars, compass, minimap, 8-slot hotbar, E Examine, inscription popup |
| Co-op | Host and join, 2–4 players | Shared Occlusion events, shared journal discoveries |

---

## 12. Roadmap toward the target

**[PROPOSED]** Milestones after M4. Each one is playable on its own and ends with a check Bruh can
do on gfccachy. The order is a recommendation: **feel first** (look and combat, since those are
what friends notice in the first minute), then **the loop** (the discovery chain), then **the
instability** (Occlusions and bosses).

| # | Name | Goal | Done when |
| --- | --- | --- | --- |
| **M5** | **The look** | Bring the starting region toward the concept boards. | A screenshot from the Confluence shows a pink sky with the big planet, haze, a waterfall, cliffs, and a distant Grand College silhouette, and the HUD matches the boards (portrait, 3 bars, compass, minimap, 8-slot hotbar, E Examine popup). Still runs smoothly on gfccachy. |
| **M6** | **Read the fight** | Zelda-style combat. | Lock-on with strafing and target switching, parry, perfect dodge, visible tells on every dangerous attack, 3–4 behaviour types across existing creatures. Test: a nightstalker pack is hard if you mash and fair if you lock on and dodge. |
| **M7** | **The discovery chain** | Bruh's example expedition, start to finish. | Find singing quartz → tune the Ulun pick → open the buried passage → learn FRACTURE → wake the gorge lift → reach the next basin. Journal records it. Smoke test walks the whole chain. |
| **M8** | **The first Occlusion** | Scripted Occlusion and boss 1. | Warning ladder, boundary with Earth gravity, Earth intrusions and floating chunks, first intruder enemies, the scar left behind, boss 1 (once Bruh names it). |
| **M9** | **Unstable world** | Boss 2 and random Occlusions. | Boss 2; Occlusion director with pressure, never inside a Tether radius; the "finish mining or run" moment happens naturally in a playtest. |
| **M10** | **The wider world** | Act 3+ content. | Grand College you can enter, a second class (Calling), a second settlement, a second biome region beyond the painted map. |

Two things run alongside every milestone: keeping the World Bible current, and the smoke test
(`--play --smoke`) growing to cover each new system.

---

## 13. Open questions for Bruh

In the order that unblocks the most:

1. **Milestone order.** Look → combat → discovery chain → Occlusions (recommended), or another
   order?
2. **Boss 1 and boss 2.** Who or what are they? (Section 6.7 has the job each one does.)
3. **Occlusion intruders.** The concept art shows red-eyed golems and beasts. Earth things,
   things from between the worlds, or Aruun creatures warped by the gravity?
4. **The big planet** in the sky: what is it?
5. **Lock-on key.** Middle mouse (recommended for mouse and keyboard), or another key?
6. **Earth materials from Occlusion scars.** Yes or no? (Steel, glass, concrete as rare loot.)
7. **Skill points from discovery** rather than kills: agree?
8. **Placeholder names** in this doc: singing quartz, the gorge lift, the buried passage.

---

## Related documents

- [World Bible](WORLD_BIBLE.md): canon, lore, open questions.
- [Visual consistency report](VISUAL_CONSISTENCY_REPORT.md)
- [Inscriptions](design/inscriptions.md): the inscription slice as built.
- [Mob behaviours](design/mob_behaviours.md)
- Gameplay parity plan (M1–M4): https://claude.ai/artifact/4MeKuTxreqhtC7mDu2sRV6
