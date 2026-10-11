// Wingspan card tier list generator (Strategi app). Data: Wingsearch master.json (navarog/wingsearch).
// Scores every bird in the digital sets (core, european, oceania) by power category + cost/points/eggs/nest/habitats,
// writes one guide per set as JSON for POST /api/guides.
const fs = require('fs');
const path = require('path');
const dir = __dirname;
const all = require(path.join(dir, 'wsdata', 'master.json'));

// [test, base score, reason]. First match wins - order matters.
const rules = [
  [/Discard 1 \[egg\] from any of your other birds to gain 2/, 9, 'turns 1 egg into 2 food of any type every activation – the strongest food engine in the game'],
  [/Discard 1 \[egg\] to draw 2 \[card\]/, 9.5, 'turns 1 egg into 2 cards every activation – starts the best card/tuck engine'],
  [/^Discard 1 .*?\. If you do, play another bird/, 9, 'a brown power that plays an extra bird EVERY time the row activates'],
  [/Discard 1 \[egg\] from any of your other birds to gain 1/, 6.5, 'turns 1 egg into 1 food of your choice – useful, but half as strong as the ravens'],
  [/Draw \[card\] equal to the number of players/, 5, 'a card for everyone, you get one extra – small'],
  [/Cache 1 \[\w+\] from your supply on each/, 6.5, 'turns spare food into points on several birds at once'],
  [/Discard all remaining face-up \[card\] and refill/, 5.5, 'refreshes the tray and takes one card'],
  [/Draw 2 \[card\]\. All other players draw/, 5.5, '2 cards for you, but 1 for every opponent'],
  [/Discard 1 \[\w+\] to choose a \[card\] from the tray and tuck/, 7, '1 food becomes a sure point every activation'],
  [/Discard 1 \[wild\] to tuck 1 \[card\]|Discard 1 \[\w+\] to tuck 1 \[card\] from the deck/, 6.5, 'turns a spare food into a point every activation'],
  [/^If the player to your (left|right|left or right) has/, 6, 'free food or a point when a neighbour holds that food – fires often'],
  [/Draw and discard 5 \[card\]/, 6, 'random fish points – some activations give 3, some 0'],
  [/Give 1 \[nectar\]/, 6, 'nectar into eggs or dice – fair'],
  [/Discard all \[egg\] from 1 of your birds/, 6, 'trades eggs for twice as many tucked cards, once'],
  [/Tuck 1 \[card\] from the deck behind each bird in this habitat/, 7, 'a point behind every bird in the row at once'],
  [/Tuck the smallest bird in the tray/, 7, 'a sure point every activation, no cost'],
  [/Keep 1 and give the other to another player/, 5.5, 'a card for you, a card for an opponent'],
  [/If you do, gain 1 \[nectar\]/, 6, 'swaps food into nectar, the wild food'],
  [/Shuffle the discard pile, then draw 2/, 6.5, 'a card or a point every activation'],
  [/Look at 3 \[card\] from the deck\. Keep 1/, 6.5, 'a card or a point most activations'],
  [/Discard a bird from your \[\w+\] and put this bird in its place/, 7, 'replaces a weak bird and comes with 4 eggs and 2 fruit'],
  [/spent nectar/, 6.5, 'nectar into 2 dice, and spent nectar scores the habitat majority'],
  [/Play (an additional|another) bird/, 8, 'plays a second bird for one action – a whole extra turn early in the game'],
  [/If you used all 4 types of actions this round, play another bird/, 7, 'a free extra bird each round, but only if you used all 4 actions'],
  [/takes the “lay eggs” action/, 8.5, 'lays free eggs on other players’ turns – almost everyone lays eggs, so it fires again and again'],
  [/When another player tucks/, 7.5, 'free tucks on other players’ turns – strong if your opponents tuck'],
  [/When another player plays a bird in their/, 7.5, 'gains free food whenever someone plays a bird in that habitat'],
  [/When another player (takes the “gain food”|gains)/, 7, 'free food on other players’ turns – steady, but the birdfeeder may be empty'],
  [/When another player's \[predator\] succeeds/, 4.5, 'only fires when an opponent’s predator succeeds – often never'],
  [/Tuck 1 \[card\] from your hand behind this bird\. If you do, (draw 1|you may also lay|lay 1 \[egg\] on any|lay up to 2|gain 1)/, 8, 'flocking: 1 point per activation (tucked card) PLUS a card, egg or food back'],
  [/Tuck 1 \[card\] from your hand behind this bird\. If you do, all players/, 6, 'tucks for a point, but every player gets food too'],
  [/Choose 1-\d birds in (this habitat|your).*Tuck/, 7.5, 'tucks behind several birds at once – many points from one activation if you have cards'],
  [/Tuck up to 3 \[card\] from your hand behind this bird/, 8, 'up to 3 points per activation and refills your hand'],
  [/Discard 1 \[\w+\] to tuck 2 \[card\] from the deck/, 7.5, '1 food becomes 2 points every activation'],
  [/Discard (up to 5|any number of)/, 7, 'converts spare food into points or cards – good late when food has nothing left to buy'],
  [/(less than|wingspan is less than) (\d+)cm/, null, 'predator'],
  [/wingspan is over \d+cm/, 6, 'predator for big birds – succeeds less often than the small-bird hunters'],
  [/Look at a \[card\] from the deck\. If (it can live|its food cost)/, 6, 'predator-style gamble: free point when it hits'],
  [/Roll all dice not in (the )?birdfeeder/, 4.5, 'dice-roll hunter: unreliable, and weak when few dice are outside the feeder'],
  [/Repeat a brown power on another bird/, 8, 'copies your best brown power in the row – doubles an engine'],
  [/Repeat 1 \[predator\] power/, 6.5, 'repeats a predator – good only in a predator row'],
  [/Copy a brown power/, 6.5, 'copies a neighbour’s bird – depends on what they build'],
  [/^Cache 1 \[\w+\] from the supply on this bird\./, 8, 'a sure point every activation, no dice needed'],
  [/Gain 1 \[\w+\] from the supply and cache it on this card/, 8.5, 'a point or a food every activation – flexible and sure'],
  [/Gain 1 \[\w+\] from the birdfeeder, if available\. You may cache it/, 7, 'food or a point each activation, if the die is in the feeder'],
  [/Steal 1/, 6.5, 'takes food from an opponent (they get a die back) – fine, not great'],
  [/^Gain 1 \[\w+\] from the supply\./, 7.5, 'a guaranteed food every activation, no dice needed'],
  [/Gain 1 \[\w+\] from the supply, or discard/, 7.5, 'a guaranteed food every activation'],
  [/Gain 1 \[die\] from the birdfeeder|Gain 1 \[\w+\]( or \[\w+\])? from the birdfeeder|Gain 1 \[\w+\] from the birdfeeder, if/, 6, 'extra food from the feeder – only when the right die is there'],
  [/Lay 1 \[egg\] on each bird in this column/, 8, 'several eggs per activation'],
  [/Lay 1 \[egg\] on any bird/, 7.5, 'an extra egg anywhere every activation – fills egg goals and pays bird costs'],
  [/Lay 1 \[egg\] on an adjacent bird/, 7, 'an extra egg every activation'],
  [/Lay 1 \[egg\] on this bird/, 6.5, 'an extra egg every activation, but only on itself'],
  [/Discard 1 \[\w+\](\. If you do, lay| from your supply\. If you do, lay)/, 6.5, 'food into eggs – OK when food is plentiful'],
  [/Draw 2 \[card\]\. If you do, discard 1/, 7, 'card filtering: better hand every activation'],
  [/Draw 1 \[card\] for each empty card slot/, 6.5, 'strong early when the row is empty, weaker later'],
  [/Draw 2 \[card\] from the deck\. Tuck 1/, 7.5, 'a point AND a card every activation'],
  [/^Draw 1 \[card\]\.( If you do, discard 1)?/, 5.5, 'one extra card per activation – small'],
  [/Draw 1 face-up \[card\]|Gain 1 face-up \[card\]/, 6, 'picks a card you want from the tray'],
  [/All players|Each player gains|Choose a food type\. All players|Player\(s\) with the fewest/, 5.5, 'everyone gets the bonus – good only if you gain more from it than they do'],
  [/Choose 1 other player\./, 6, 'you and one opponent both gain – small edge'],
  [/move it to another habitat/, 4, 'moves itself to another habitat – fiddly and slow'],
  [/Reset the birdfeeder/, 6, 'rerolls the feeder to get the food you need'],
  [/Trade 1|Remove any 1 \[die\]|From the supply, gain 1 food of a type/, 6, 'food fixing – helps when the feeder is bad'],
  [/This bird counts double toward the end-of-round goal/, 5.5, 'counts double for a round goal – only useful if the goal fits'],
  [/Lay 1 \[egg\] on this bird for each other bird|Choose a habitat with no \[egg\]|For each action cube on their/, 7, 'many eggs or points once per round for free'],
  [/Cache 1 \[\w+\] from the supply on this bird for each \[predator\]/, 6, 'points per predator – good in a predator game'],
  [/Gain 3 \[\w+\] from the supply/, 7, '3 food at once – pays for your next big bird'],
  [/Gain all \[\w+\] that are in the birdfeeder/, 6.5, 'empties the feeder for you – strong when it holds several of one food'],
  [/Draw the 3 face-up \[card\]/, 6.5, '3 cards at once'],
  [/^Draw 2 \[card\]\.$/, 5.5, '2 cards once'],
  [/Lay 1 \[egg\] on each (of your birds|bird in your)/, 7, 'several eggs at once – big when played late on a full board'],
  [/play this bird on top of another bird/, 7, 'replaces a weak bird for free – lets you upgrade a full row'],
  [/For each \[\w+\] in this bird's cost, you may pay 1 \[card\]/, 6, 'can be paid with cards instead of food – easy to play'],
  [/covers 2 \[\w+\] spaces/, 5, 'takes two spaces – fewer birds in that row'],
  [/bonus card/i, 5.5, 'an extra bonus card – often 3-6 points, but slow'],
  [/Cache up to \d+/, 6, 'game end: leftover food becomes points'],
];

// Fallbacks for powers no exact rule covers (mostly Asia + Bird Packs): judged by what the power does.
const fallback = [
  [/Play (1 )?(an )?additional bird|Play a bird from the tray/i, 8, 'plays a second bird for one action – a whole extra turn early in the game'],
  [/up to 3 times|Roll (any|all|them)|Roll it|roll any/i, 5, 'a dice or card gamble – sometimes big, often nothing'],
  [/^When another player/, 6.5, 'fires on other players’ turns – free value while you wait'],
  [/copy|Activate a “When Activated”/i, 6.5, 'copies another power – as good as the power it copies'],
  [/Tuck .*(draw|gain|lay)|to (draw|gain|lay).*tuck|tuck 1 \[card\] from your hand.* to /i, 7.5, 'tucks for a point AND gives a card, food or egg back'],
  [/Tuck up to 5|Tuck 2 \[card\]|tuck 3 \[card\]|tuck all|tuck up to 2/i, 7, 'several points from tucked cards at once'],
  [/tuck/i, 6.5, 'a point from a tucked card most activations'],
  [/cache/i, 6.5, 'a point from cached food most activations'],
  [/lay (2 \[egg\]|\d+ \[egg\] on each|1 \[egg\])|lay 2/i, 6.5, 'extra eggs – points and bird costs'],
  [/gain .*from the (general )?supply|gain 1 \[die\]|gain 2 \[die\]/i, 6.5, 'extra food most activations'],
  [/draw/i, 6, 'extra cards'],
];

function score(b) {
  const p = b['Power text'] || '';
  let base = 2.5 + 0.4 * (b['Victory points'] || 0), why = 'no power – it is only worth its points, so play it late';
  if (p) {
    base = 5.5; why = 'a niche power';
    for (const [re, s, w] of rules) {
      const m = p.match(re);
      if (!m) continue;
      if (w === 'predator') {
        const cm = +m[2];
        base = cm >= 100 ? 7.5 : cm >= 75 ? 7 : cm >= 65 ? 6 : cm >= 50 ? 5 : 4;
        why = `predator: free point when the drawn bird is under ${cm} cm` + (cm >= 75 ? ' – succeeds most of the time' : cm >= 50 ? ' – succeeds about half the time' : ' – fails often');
      } else { base = s; why = w; }
      break;
    }
    if (why === 'a niche power') {
      for (const [re, s, w] of fallback) { if (re.test(p)) { base = s; why = w; break; } }
    }
    if (b.Color === 'yellow' && base === 5.5) { base = 6; why = 'game end power – counts once, play it in round 4'; }
    if (b.Color === 'teal' && base === 5.5) { base = 6.5; why = 'round end power – fires every round, play it early'; }
  }
  const notes = [];
  let s = base;
  const cost = b['Total food cost'] || 0, vp = b['Victory points'] || 0, eggs = b['Egg limit'] || 0;
  const habs = ['Forest', 'Grassland', 'Wetland'].filter(h => b[h]);
  if (cost <= 1) { s += 0.7; notes.push(cost === 0 ? 'free to play' : 'costs only 1 food'); }
  else if (cost >= 3) { s -= 0.7; notes.push(`expensive (${cost} food)`); }
  s += (vp - 4) * 0.25;
  if (vp >= 7) notes.push(`${vp} points – a good round-4 finisher`);
  if (vp <= 1) notes.push(`only ${vp} point${vp === 1 ? '' : 's'}`);
  if (eggs >= 5) { s += 0.3; notes.push(`holds ${eggs} eggs`); }
  if (eggs <= 1) { s -= 0.3; notes.push(eggs === 0 ? 'holds no eggs' : 'holds only 1 egg'); }
  if (b['Nest type'] === 'wild') { s += 0.2; notes.push('wild nest counts for every nest bonus'); }
  if (habs.length >= 2) { s += 0.3; notes.push(`lives in ${habs.length} habitats`); }
  return { s, why, notes, habs, cost, vp };
}

const tierOf = s => s >= 8.5 ? 'S' : s >= 7.3 ? 'A' : s >= 6 ? 'B' : s >= 4.8 ? 'C' : 'D';
const tierText = {
  S: 'S tier – take it almost every time',
  A: 'A tier – strong, build around it',
  B: 'B tier – good in the right row or with the right goal',
  C: 'C tier – filler; play it for points, a goal or a bonus card',
  D: 'D tier – usually skip',
};
const habShort = { Forest: 'forest', Grassland: 'grassland', Wetland: 'wetland' };
const sets = [
  ['asia', 'Asia expansion'],
  ['promoUK', 'Bird Pack: British birds'],
  ['promoEurope', 'Bird Pack: Continental Europe'],
  ['promoAsia', 'Bird Pack: Asia'],
  ['promoNZ', 'Bird Pack: New Zealand'],
  ['promoCA', 'Bird Pack: Canada'],
  ['promoUS', 'Bird Pack: United States'],
  ['core', 'base game'],
  ['european', 'European expansion'],
  ['oceania', 'Oceania expansion'],
];

const guides = [];
for (const [set, label] of sets) {
  const birds = all.filter(b => b.Set === set).map(b => ({ b, ...score(b) })).sort((x, y) => y.s - x.s || x.b['Common name'].localeCompare(y.b['Common name']));
  const by = { S: [], A: [], B: [], C: [], D: [] };
  for (const r of birds) by[tierOf(r.s)].push(r);
  let body = `## How this list works\n` +
    `- Every ${label} bird (${birds.length}) is rated the same way: what its power does for you, how cheap it is, its points, egg space, nest and how many habitats it fits.\n` +
    `- Each line: **name** (habitats, food cost, points) – why it is good or bad.\n` +
    `- Brown powers are rated for playing them in rounds 1-2; a 9-point bird with no power is better in round 4 than its tier says.\n` +
    `- **Raven note** marks ravens: you don't play them, they are listed so you know what an opponent gets.\n` +
    `- Counts: ${Object.entries(by).map(([t, l]) => `${t} ${l.length}`).join(', ')}.\n`;
  for (const t of ['S', 'A', 'B', 'C', 'D']) {
    if (!by[t].length) continue;
    body += `\n## ${tierText[t]}\n`;
    for (const r of by[t]) {
      const name = r.b['Common name'];
      const raven = /raven/i.test(name);
      const hab = r.habs.map(h => habShort[h]).join('/');
      const extra = r.notes.length ? '; ' + r.notes.join(', ') : '';
      body += `- ${raven ? '**Raven note** – ' : ''}**${name}** (${hab}, ${r.cost} food, ${r.vp} pts) – ${r.why}${extra}.\n`;
    }
  }
  guides.push({
    game: 'wingspan', section: 'mekanik', title: `Tier list: every bird – ${label}`, trigger: '', source: 'AI',
    sources: ['Card data: Wingsearch (all card texts)|https://navarog.github.io/wingsearch/', 'BoardGameGeek strategy forum|https://boardgamegeek.com/boardgame/266192/wingspan/forums/66'],
    body,
  });
  console.log(label, Object.entries(by).map(([t, l]) => `${t}${l.length}`).join(' '), 'chars', body.length);
}
fs.writeFileSync(path.join(dir, 'wingspan-tiers.json'), JSON.stringify(guides, null, 1));
module.exports = { score, tierOf, sets, habShort, all };
