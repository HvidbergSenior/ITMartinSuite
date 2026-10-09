// GalCiv4 lookup cards (Strategi ItemAdvice) for civ abilities, civ traits and factions + a custom-civ guide.
// Effects come from the wiki (shown automatically when the title matches a wiki entry); tiers and advice are judgement.
const fs = require('fs');
const path = require('path');
const A = []; // [title, tier, purpose, useOn, avoidOn]
const ab = (t, tier, p, u, a) => A.push({ game: 'galciv4', title: t, group: 'Civ abilities', purpose: `**Tier ${tier}** · Civ ability\n- ${p}`, useOn: u, avoidOn: a, example: '' });

ab('Fertile', 'S', 'More citizens from turn 1 and 25 % faster growth – every planet works sooner.', '- Wide expansion, any victory. Pair with **Farmer** or **Urbanite** traits.', '- Few food worlds nearby – growth stalls on food.');
ab('Bureaucrats', 'S', '50 Control at the start = two or three early executive orders (Draft Colonists).', '- Fast early expansion; Oligarchy/Empire players who live on Control.', '- Rarely – it is strong in every game.');
ab('Inspired', 'S', 'One more leader, all leaders 25 % cheaper – more governors and admirals early.', '- Wide empires that need governors on every Core World.', '- Tiny empires with few jobs for leaders.');
ab('Wealthy', 'S', '2,000 credits and Universal Translator at the start – buy your first colony ships and buildings.', '- Fast start, trade from turn 1, buying instead of building.', '- Long games where the head start fades – plan to invest it early.');
ab('Resourceful', 'A', 'Starts with Durantium and Hyper Silicates – Colonization Centers and key buildings right away.', '- Early growth buildings on every Core World.', '- When you do not build the buildings that need them.');
ab('Unrelenting', 'A', '+10 Control and +1 influence per planet – steady borders and orders.', '- Wide empires; culture pressure on neighbours.', '- One-planet tall play.');
ab('Ancient', 'A', 'Research from artifacts and anomalies, plus an extra policy slot.', '- Explorers who survey everything early; policy-heavy builds.', '- Crowded maps with few anomalies.');
ab('Craftsman', 'A', '+50 % income from trade routes.', '- Trade-focused peaceful games with many partners.', '- Isolated starts or constant war (no routes).');
ab('Xenophobic', 'A', '+50 % research but -75 % influence and -3 diplomacy.', '- Tech victory, turtle play, small compact empire.', '- Culture or diplomacy wins; crowded maps where borders matter.');
ab('Warriors', 'A', '+10 % weapon damage and 10 % less damage taken.', '- Conquest and early wars.', '- Peaceful games – the bonus does nothing without fights.');
ab('Promethion Born', 'A', 'Double ship hit points but no normal healing.', '- Short decisive wars; build repair modules or starbases.', '- Long wars far from repair.');
ab('Proliferation', 'A', 'Every ship is built twice, at -25 % hit points.', '- Cheap probes, colony ships and swarms of small warships.', '- When you rely on a few big flagships.');
ab('Slavers', 'A', 'Enslaved citizens never protest and eat less – production without approval worries.', '- Production-heavy and conquering empires.', '- Diplomacy-focused games – other civs dislike it.');
ab('Spores', 'A', 'Revives dead worlds – more planets than your neighbours.', '- Wide empires; maps with many dead worlds.', '- Water-rich maps where good planets are plenty.');
ab('Adaptable', 'A', 'Colonises extreme worlds nobody else wants.', '- Expansion on crowded maps; you get the leftovers for free.', '- Maps full of good class worlds.');
ab('Traders', 'A', 'Starts with a freighter, strong persuasion and 3 free trade licenses.', '- Trade and diplomacy wins.', '- Isolated starts.');
ab('Intuitive', 'B', 'Free culture trait and early Xeno Anthropology.', '- Culture and influence plans.', '- Pure military games.');
ab('Devout', 'B', 'Starts with a Cleric and a temple; culture grows with ideology.', '- Ideology-heavy culture play.', '- Fast war games.');
ab('Certain', 'B', '+2 influence, special improvements, research per culture trait.', '- Culture victory and border pressure.', '- When you rarely unlock culture traits.');
ab('Empathy', 'B', '+1 diplomacy, +15 % persuasion, -10 % crime.', '- Diplomacy and alliances.', '- Lone wolf conquest.');
ab('Hopeful', 'B', 'Higher approval on planets.', '- Big populations where approval limits output.', '- Small empires – approval is easy there anyway.');
ab('Loyal', 'B', 'Leaders +10 loyalty and 25 % cheaper; culture for leaders on command ships.', '- Many leaders, fleets with admirals.', '- When leaders rarely defect anyway.');
ab('Engineers', 'B', 'Colony and asteroid production decays half as fast.', '- Wide empires with many colonies.', '- Tall empires with few colonies.');
ab('Voyagers', 'B', '+2 sensor range and +1 ship move.', '- Exploration race and fast fleets.', '- Late game – speed matters less.');
ab('Starfaring', 'B', '+25 % ship range and subspace streams from the start.', '- Far expansion; grab distant good planets.', '- Small maps.');
ab('Scout Fleet', 'B', 'A colony ship and three probes at the start, but -100 Control.', '- Fast exploration start.', '- Control-hungry openings (no Draft Colonists).');
ab('Watcher', 'B', 'Cosmic Eye order: reveals space and gives research.', '- Exploration and science.', '- When you never use executive orders.');
ab('Crime Lord', 'B', 'Allied with pirates; crime unlocks bonuses.', '- Sneaky, chaotic play with pirate fleets.', '- Lawful, high-approval builds.');
ab('Radiated', 'B', 'Colonises radioactive worlds; can pollute enemy planets.', '- Crowded maps and hostile play.', '- Peaceful diplomacy.');
ab('Ravenous', 'B', '1,000 credits and approval after each conquered planet.', '- Conquest from the early game.', '- Peaceful games.');
ab('Genocidal', 'B', 'Big advantage when invading planets.', '- Conquest.', '- Diplomacy.');
ab('Hive Mind', 'B', 'Ships inside your territory gain HP per colony.', '- Defensive wide empires.', '- Wars far from home.');
ab('Paranoid', 'B', '+25 % ship HP and attack inside your borders.', '- Turtle and defend.', '- Offensive wars.');
ab('Nocturnal', 'B', 'Seasons switch between production/growth and research/influence.', '- Players who plan around the cycle.', '- If you dislike micromanagement.');
ab('Unwavering', 'B', 'Colonies are hard to culture-flip.', '- Next to culture-heavy neighbours.', '- When nobody pushes culture.');
ab('Archaeologist', 'B', 'An extra artifact charge every time charges are earned.', '- Artifact-rich maps.', '- Few artifacts.');
ab('Experienced', 'C', '25 % more ship experience.', '- Long wars with the same fleets.', '- Most games – small effect.');
ab('Cartographer', 'C', 'Knows where other homeworlds are; reveals space.', '- Planning early wars or diplomacy.', '- It gives no economy.');
ab('Resilient', 'C', 'Ships immune to nebulae; Hyperion improvements.', '- Nebula-heavy maps.', '- Most maps.');
ab('War Profiteers', 'C', '+200 % from trade routes with civs at war.', '- When the AIs fight a lot.', '- Peaceful galaxies.');
ab('Raiders', 'C', '-50 % production and income; big rewards for winning battles.', '- Constant war – fun but risky.', '- Any peaceful plan.');
ab('Exterminators', 'C', 'Transports replaced by planet-killing spore ships.', '- Pure destruction games.', '- If you want to keep conquered planets.');

const T = [];
const tr = (t, tier, p, u, a) => T.push({ game: 'galciv4', title: t, group: 'Civ traits', purpose: `**Tier ${tier}** · Civ trait (custom civilization)\n- ${p}`, useOn: u, avoidOn: a, example: '' });
tr('Productive +1: Productive', 'S', '+10 % manufacturing per level (+2 is more). Faster buildings and ships everywhere.', '- Every build. The safest trait.', '- Never a bad pick.');
tr('Clever +1: Clever', 'S', '+10 % research per level.', '- Tech victory; every build that wants techs early.', '- Pure early rush.');
tr('Rich +1: Rich', 'S', '+10 % income per level.', '- Buying buildings, trade, Oligarchy.', '- Rarely bad.');
tr('Influential +1: Influential', 'A', '+10 % influence growth – bigger borders, culture.', '- Culture victory, wide play.', '- War-only builds.');
tr('Urbanite +1: Urbanites', 'A', '+25 % population cap and Planetary Outposts tech.', '- Tall empires with few, big worlds.', '- Wide spam of small colonies.');
tr('Bureaucrat +1: Bureaucrats', 'A', '+1 Control per month and organisational techs.', '- Executive-order heavy play.', '- If you never spend Control.');
tr('Farmer +1: Farmers', 'B', '+25 % food and farm buildings.', '- With Fertile – growth needs food.', '- Synthetic species (no food).');
tr('Miners +1: Miners', 'B', '+50 % asteroid production.', '- Asteroid-rich starts.', '- Few asteroids.');
tr('Militant +1: Militant', 'B', '+30 starting Control.', '- Fast first executive orders.', '- Long games – one-time bonus.');
tr('Likeable +1: Likeable', 'B', '+2 diplomacy.', '- Diplomacy and alliances.', '- Lone conquest.');
tr('Explorer +1: Explorers', 'B', '+2 ship range and sensor range.', '- Exploration race.', '- Small maps.');
tr('Fast +1: Fast', 'B', '+1 ship move.', '- War and exploration.', '- Tall peaceful play.');
tr('Persuasive +1: Persuasive', 'C', '+30 persuasion in events and diplomacy.', '- Event-rich games.', '- Take the negative level for points instead.');
tr('Deceptive +1: Deceptive', 'C', '+10 deception in events.', '- Sneaky event choices.', '- Take the negative level for points.');
tr('Intimidating +1: Intimidating', 'C', '+30 intimidation in events and diplomacy.', '- Bully diplomacy.', '- Take the negative level for points.');
tr('Brutal +1: Brutal', 'C', 'Better planetary invasions.', '- Invasion-heavy war.', '- Peaceful play – take Gentle (-) for points.');
tr('Courageous +1: Courageous', 'C', '+20 % planetary defence.', '- Border worlds next to aggressors.', '- Safe maps – take Craven (-) for points.');
tr('Veteran +1: Veteran', 'C', 'Ships gain experience faster.', '- Long wars.', '- Peaceful games – take the negative level for points.');

const C = [];
const civ = (t, abil, tier, p, u, a) => C.push({ game: 'galciv4', title: t, group: 'Civilizations', purpose: `**Tier ${tier}** · Faction · abilities: ${abil} (check in game – can change with patches)\n- ${p}`, useOn: u, avoidOn: a, example: '' });
civ('Terran Alliance', 'Explorers, Inspired', 'A', 'The all-rounder: extra cheap leader, colonisation events on Mars and Artemis.', '- Learning the game; any victory.', '- Nothing special – no big trick.');
civ('Terran Resistance', 'Resourceful, Unrelenting', 'A', 'Early Colonization Centers from its starting resources; steady Control and influence.', '- Wide expansion; culture pressure.', '- Few planets nearby.');
civ('Drengin Empire', 'Slavers, Unrelenting', 'A', 'Production from slaves who never protest; built for war.', '- Early war on a weak neighbour.', '- Peaceful diplomacy.');
civ('Yor Singularity', 'Adaptable, Unwavering (synthetic)', 'B', 'No food – citizens are manufactured. Takes the extreme worlds.', '- Crowded maps; players who like a different economy.', '- First games – it plays very differently.');
civ('Altarian Republic', 'Ancient, Empathy', 'A', 'Research from anomalies, extra policy slot, good diplomacy.', '- Diplomacy and culture wins.', '- Early war.');
civ('Mimot Brotherhood', 'Fertile, Proliferation', 'S', 'Huge growth and every ship built twice – the fastest expansion.', '- Wide play; swarm of cheap colony ships.', '- Food-poor starts.');
civ('Navigators', 'Archaeologist, Starfaring', 'B', 'Long range from turn 1, extra artifact charges.', '- Grabbing distant good planets.', '- Small maps.');
civ('Torian Regime', 'Fertile, Unwavering (aquatic)', 'A', 'Extra citizens, fast growth, safe from culture flips.', '- Peaceful expansion on wet worlds.', '- Dry maps.');
civ('Krynn Syndicate', 'Devout, Bureaucrats', 'A', '50 Control at the start and a culture push from ideology.', '- Fast executive orders, culture victory.', '- Pure war.');
civ('Iconian Refuge', 'Xenophobic, Paranoid', 'B', 'Big research, weak influence; strong at home.', '- Tech victory from a compact turtle.', '- Wide or diplomatic play.');
civ('Festron Hunt', 'Ravenous, Unrelenting', 'B', 'Gets rich by conquering planets.', '- Early aggressive war.', '- Peaceful neighbours far away.');
civ('Xeloxi', 'Warriors, Crime Lord', 'B', 'Pirate allies and stronger weapons.', '- Chaotic military play.', '- Clean diplomacy.');
civ('Arcean Republic', 'Loyal, Vigilant', 'B', 'Cheap loyal leaders and defence.', '- Safe defensive expansion.', '- Fast offence.');
civ('Manti Cluster', 'Paranoid, Warriors', 'B', 'Very strong inside its borders.', '- Expand, then defend.', '- Offensive wars far away.');
civ('Drath Freehold', 'War Profiteers, Ancient', 'B', 'Profits when others fight; research from anomalies.', '- Galaxies full of wars.', '- Peaceful maps.');
civ('Corporate Sector', 'Traders, Wealthy', 'S', '2,000 extra credits, a freighter and trade from turn 1.', '- Economy and buying everything.', '- Isolated starts.');
civ('Onyx Hive', 'Promethean, Experienced (silicon)', 'B', 'Growth depends on Promethion – find it first.', '- Maps with Promethion stars nearby.', '- Without Promethion it stalls.');
civ('Baratak Grove', 'Intuitive, Spores', 'A', 'A free culture trait and more planets by reviving dead worlds.', '- Wide culture play.', '- Few dead worlds.');

const guide = {
  game: 'galciv4', section: 'valg', title: 'Make your own civilization: specialised, fun and unique (not just powerful)', trigger: 'Custom civilization', source: 'AI',
  sources: ['GalCiv4 wiki: civ abilities and traits|https://wiki.galciv.com'],
  body: `## The idea
- A power civ takes Productive + Clever + Rich and is good at everything – and plays the same every game.
- A fun civ has **one fantasy**, **one engine** that feeds it, and **one real weakness** you have to play around. The weakness gives trait points AND makes decisions matter.
- Search any ability or trait in the 🔧 lookup to see its tier, effect and what it fits.

## How to build one (in the civilization editor)
- **1. Pick the fantasy:** merchants, swarm, zealots, ghosts, scavengers … one sentence.
- **2. Pick the ability that IS the fantasy** (not the strongest one).
- **3. Spend traits on the engine** that makes the fantasy work (2-3 traits).
- **4. Take 2-3 negative traits** that fit the story and that you can live with – they pay for the engine.
- **5. Ideology, ship style, name and leader** to match. A civ that looks and talks like its fantasy is more fun to play.
- Check the point cost of each trait in the editor – costs can change with patches.

## Stats that are nice in almost every build
- **Productive** and **Clever**: faster everything. If you take only one positive trait, take one of these.
- **Rich**: buying is fast; strong with trade abilities.
- **Influential**: borders and culture.
- Cheap negatives for peaceful builds: **Gentle (Brutal -)**, **Craven (Courageous -)**, **Bad Liar (Deceptive -)**, **Meek (Intimidating -)** – they hurt only in fights and events.
- Negatives to avoid: **Unproductive**, **Foolish**, **Poor** – they slow your whole empire.

## 8 themed builds
- **The Merchant Guild** – Traders or Craftsman · Rich, Likeable, Persuasive · weak: Gentle, Craven, Passive. Win by trade and alliances; every war is bad for business, so you buy peace.
- **The Swarm** – Proliferation · Productive, Fast, Militant · weak: Poor, Unlikeable. Every ship comes twice but fragile: flood the map with probes, colony ships and small warships.
- **The Zealots** – Devout or Certain · Influential, Intimidating · weak: Foolish (-1), Unlikeable. Culture-flip neighbours instead of invading them.
- **The Ghost Fleet** – Promethion Born · Veteran, Fast · weak: Unlikeable, Poor (-1). Few tough ships that cannot heal – every battle must count; build repair starbases on the front.
- **The Scavengers** – Adaptable or Radiated · Miners, Urbanites · weak: Unlikeable, Craven. Live on the worlds nobody wants; asteroids feed you.
- **The Hermit Scholars** – Xenophobic · Clever, Urbanites · weak: Unlikeable, Meek, Gentle. Tiny, tall, unbeatable research; you must defend few worlds very well.
- **The Gardeners** – Spores or Fertile · Farmer, Influential · weak: Gentle, Craven. Bring dead worlds to life and grow huge peaceful populations.
- **The Raiders** – Raiders or Ravenous · Brutal, Fast, Veteran · weak: Poor, Unlikeable. No real economy – you live from battles and conquered planets. Hard and very different.

## Make it unique in play, not only on paper
- Give yourself a **house rule** that matches the fantasy: the Merchant Guild never declares war, the Swarm never builds ships bigger than medium, the Scholars never colonise more than 6 worlds.
- Pick **event choices** that fit the story even when they are not the best – it makes each game tell a story.
- Name your leaders and ships in the civ's style.`,
};

const advice = [...A, ...T, ...C];
fs.writeFileSync(path.join(__dirname, 'gc4-advice.json'), JSON.stringify(advice));
fs.writeFileSync(path.join(__dirname, 'gc4-civguide.json'), JSON.stringify([guide]));
console.log('advice', advice.length, 'abilities', A.length, 'traits', T.length, 'civs', C.length);
