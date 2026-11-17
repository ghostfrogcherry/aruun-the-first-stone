# Mob behaviours

How the four mobs in [mobs.md](mobs.md) act, why (lore), and the numbers that keep them fair.
The numbers live in `content/creatures/creatures.json` and the code is `scripts/entities/mob_brains.gd`. Not built yet: crouching (so the reedhopper's shorter crouched notice range and gathering resting moths by day), the sentinel's Earth-gravity variant, and lamps or torches as moth lights (only campfires draw them so far). The glassback's shell is hit from the front or rear and its flanks are soft.

**Balance baseline** (from the current game data): the castaway has 100 health and 100 stamina. A flint axe hits for 17, a reed spear for 13, the fallhammer for 30. A nightstalker bite is 12. Health comes back slowly (0.5 per second after 6 s without a hit). Hardcore scales creature damage with its own multiplier, so these are the Light numbers.

**Design rules used**

1. Every dangerous move has a tell of at least 0.6 s and a punish window after it.
2. Every mob has one lore-true trick that makes the fight easier if you learn it, and none of them need it.
3. Nothing dangerous hunts you at home. A Tether keeps its ground quiet.
4. On Aruun the castaway is strong and jumps far (0.38 g). Mobs are tuned for that; in Earth-gravity Occlusion sites the same mob is harder.

---

## Reedhopper: skittish prey

**Lore.** Small river grazers that every predator on Aruun eats, so they survive by seeing first and leaping far. Their red backs match the vermillion reeds.

| | |
|---|---|
| Health | 12 (one spear thrust or axe blow) |
| Group | 3 to 6, by day, on riverbanks |
| Notices you at | 10 m standing, 5 m crouched |
| Speed | 1.2 m/s grazing, 8.5 m/s fleeing, 2.2 m hops |

**Behaviour**

1. **Graze.** Wanders and feeds near water and reeds.
2. **Alert** (you are inside its notice range). Freezes for 1.2 s with ears up. This is your one clean shot.
3. **Flee** (you come within 6 m, sprint, or hit any of the group). The whole group bolts in bursts of three hops with a 0.8 s pause between bursts. They run about 30 m, toward reeds if any are near, and drift back after 20 s.

**Balance.** Never attacks. It is faster than the player, so chasing fails and patience works: crouch, wait for the freeze, strike. One reedhopper gives 1 raw meat and sometimes a small hide, so a group is a meal, not a stockpile. Redstriders stay the better hide source.

**Gravity trick.** A push or pull word catches one mid-hop and drops it stunned for a moment.

---

## Glassback: leave it alone, or learn the charge

**Lore.** A six-legged highland grazer that grows glasswort-like crystal on its shell to store water through the dry season. Vaari herders "milk" calm glassbacks for water on long walks. It has no natural fear because nothing can bite through the shell.

| | |
|---|---|
| Health | 140 |
| Group | 1 or 2, by day, rocky slopes and highland meadows |
| Shell | hits on the back do 30% damage and prick you for 4 |
| Flank and belly | full damage; 150% while flipped |
| Charge | 22 damage, heavy knockback |

**Behaviour**

1. **Graze.** Slow (0.9 m/s). Ignores you.
2. **Warn.** Stand within 3 m for 2 s and it lowers its head and stamps. Back off and nothing happens.
3. **Provoked** (you hit it, or crowd it for 4 s). It turns to face you.
4. **Charge.** 1.0 s wind-up, then a straight 12 m run at 6 m/s. It can barely steer (20 degrees per second), so a sidestep or a jump clears it.
5. **Recover.** 2.0 s panting after every charge. If it hits rock it is stunned for 2.5 s instead, and it smashes soft blocks (earth, clay, timber) it runs through.
6. **Calm down.** After 12 s without being hit, or once you are 16 m away, it goes back to grazing.

**Balance.** Five charges kill a careless player, but each one is dodgeable and gives a 2 s opening. With an axe it takes two or three openings. It is the first mob that teaches "dodge, then punish", before the nightstalker tests it at speed. It never chases far, so walking away always works.

**Peaceful option.** Approach a calm glassback crouched from behind to take one glasswort leaf from its shell (regrows in 5 minutes). That makes it a walking water source in dry highlands, and worth more alive than dead.

**Gravity trick.** A lifting word flips it on its back for 4 s. Bait a charge into a wall you want opened and it will mine for you.

---

## Duskmoth: the price of light

**Lore.** Night pollinators of the dusk blossom, drawn to its glow. Open flame confuses them and they mob it. Their wing scales are the base of Luminist lamp oil. A Tether's steady Avarra light does not draw them, which is one reason the Vaari camp beside stones.

| | |
|---|---|
| Health | 8 (one hit from anything) |
| Group | 3 to 8, dusk to dawn, forest edges and meadows; never more than 8 per player |
| Sees light from | 30 m |
| Dive | 5 damage |

**Behaviour**

1. **Drift.** With no light near, they wander between blossoms and ignore players unless struck.
2. **Gather.** A campfire, lamp or torch within 30 m pulls the swarm in. They circle it 3 to 5 m out.
3. **Dive.** Every 4 to 6 s, one moth (two at most) folds its wings, flares its eye spots for 0.6 s, and dives at the nearest player within 6 m of the light. After a dive it hangs low for 0.8 s, then climbs for 2 s.
4. **Dusted.** Three dive hits within 8 s blur your vision for 6 s. No damage.
5. **Scatter.** Five seconds after the light goes out, or at dawn, they leave. By day they rest on tree trunks.

**Balance.** A full swarm does about 2 damage per second if you stand still in the light, which a campfire's comfort does not cover, so you must act: swat them (one hit each), move the light, or go dark. They are a nuisance by design and never a killer on their own; the danger is being dusted when a nightstalker arrives.

**Ways out, all true to the lore**

- **Camp at a Tether.** No duskmoths within 8 m of one. Home is safe.
- **Decoy.** A second fire or lamp 15 m from camp holds the swarm there.
- **Grinleaf.** While you have the Calm buff, moths do not dive at you.
- **Daytime.** Resting moths can be gathered crouched for wing dust without a fight.

---

## Echo Sentinel: a recording that hits back

**Lore.** Not alive. The old builders set guard stones in their halls, held together by gravity magick around a lit core. What remains is an echo in the Resonant sense: it replays the last guard's drill, move for move, forever. That is its weakness. It never improvises.

| | |
|---|---|
| Core health | 320, plus 60% per extra player |
| Stone body | immune to weapons |
| Core | reachable from behind (full damage) or whenever the stones part (150%) |
| Where | ruin halls only; never leaves its hall; does not respawn |

**The drill.** Always the same order: **throw, throw, slam**, then repeat.

- **Throw** (14 damage, 0.8 s wind-up). One of its four orbit stones. A push word sends it back for 20 core damage and heavy stagger.
- **Slam** (26 damage, 1.1 s wind-up). A ground shockwave 3.5 m wide. Jump it; Aruun's gravity gives you the airtime. Afterwards the stones hang apart for 2.5 s and the core is open. The slam cracks soft floor blocks, so the hall changes as the fight goes on.
- **Pull** (from 60% health). 1.2 s of teal glow, then it drags everyone in sight toward it for 1.5 s and slams. Break line of sight behind a pillar, or push against it.

**Phases**

1. **Above 60%.** The drill as written. It keeps 6 to 9 m away.
2. **60% to 25%.** A pull before every slam.
3. **Below 25%.** No stones left to throw. It slams twice in a row and stays open for 3.5 s.

**Stagger.** Returning two stones, or dropping a raised fist with a pull word during the slam wind-up, staggers it for 3 s with the core open.

**Fairness rules.** It is dormant rubble until someone comes within 8 m, and cannot be hurt while it assembles (2.5 s). If everyone leaves the hall for 10 s it settles and heals fully, so you can always retreat. In co-op the pull targets the farthest player, so nobody hides at range.

**Balance.** With an axe, each opening is worth roughly 60 to 100 damage, so a solo fight is four or five clean openings: two to three minutes. Weapons alone can win through the back of the core. Gravity words make it faster and safer, which is the lesson it exists to teach before the demo's real boss.

**In an Occlusion site.** If a hall sits in Earth gravity, its stones weigh more: it moves 30% slower but slams 25% harder, and you can no longer jump the shockwave as easily.

---

## New items these need

`crystal_shard`, `wing_dust`, `ancient_stone`, `bronze_fitting`, `word_fragment`. Suggested first uses: wing dust + clay jar = lamp (light that draws moths, on purpose); crystal shards for a water flask upgrade.

## New things the code needs

- Behaviours `skittish`, `defender`, `swarm`, `guardian` beside the existing `herd` and `hunter`.
- Flying movement for the duskmoth and hover for the sentinel.
- Hit zones (back or flank, core or stone) and a "flipped" state.
- Light sources as something creatures can sense; a `dusted` condition.
