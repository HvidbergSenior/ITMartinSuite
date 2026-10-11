// Wingspan lookup cards (Strategi ItemAdvice, POST /api/advice): every digital bird + every digital bonus card.
const fs = require('fs');
const path = require('path');
const { score, tierOf, sets, habShort, all } = require('./wingspan_tiers.js');
const dir = __dirname;

const digital = ['core', 'european', 'oceania', 'asia', 'promoUK', 'promoEurope', 'promoAsia', 'promoNZ', 'promoCA', 'promoUS'];
const setLabel = { core: 'Base game' };
for (const [k, v] of sets) setLabel[k] = v[0].toUpperCase() + v.slice(1);
const bonusAll = require(path.join(dir, 'wsdata', 'bonus.json')).filter(x => digital.includes(x.Set) && !/^\[/.test(x['Bonus card']));
const seenName = new Set();
const birds = all.filter(b => digital.includes(b.Set))
  .filter(b => !seenName.has(b['Common name']) && seenName.add(b['Common name']))
  .map(b => ({ b, ...score(b) })).map(r => ({ ...r, tier: tierOf(r.s) }));
const isRaven = r => /raven/i.test(r.b['Common name']);
const P = r => r.b['Power text'] || '';

function cat(r) {
  const p = P(r);
  if (!p) return 'none';
  if (/egg\] to draw 2/.test(p)) return 'eggconvert';
  if (/additional bird|play another bird|bird from the tray/i.test(p)) return 'extra';
  if (r.b.Color === 'pink') return 'pink';
  if (/copy|repeat/i.test(p)) return 'copy';
  if (/less than \d+ ?cm|wingspan is/i.test(p)) return 'predator';
  if (/tuck/i.test(p)) return 'tuck';
  if (/cache/i.test(p)) return 'cache';
  if (/lay/i.test(p)) return 'egg';
  if (/gain|die\]/i.test(p)) return 'food';
  if (/draw/i.test(p)) return 'cards';
  return 'other';
}
const partners = {
  eggconvert: [['tuck', 'turns the cards into points'], ['egg', 'refills the eggs it spends']],
  tuck: [['eggconvert', 'feeds it cards'], ['cards', 'feeds it cards']],
  egg: [['eggconvert', 'turns the eggs into cards']],
  food: [['extra', 'spend the food on 2 birds per action']],
  cache: [['food', 'more food to spend while the cache scores']],
  predator: [['copy', 'repeats the hunt'], ['food', 'food while you hunt']],
  extra: [['eggconvert', 'a cheap engine bird to play as the second bird'], ['food', 'food for the second bird']],
  copy: [['eggconvert', 'copy the best engine'], ['tuck', 'copy a tuck power']],
  cards: [['tuck', 'turns the cards into points']],
  pink: [['egg', 'more eggs on the same nests']],
  none: [['food', 'food to pay for it late']],
  other: [['food', 'food to pay for it']],
};
const weak = {
  eggconvert: 'you have no eggs to spend – keep an egg layer in the grassland',
  extra: 'played late – the extra bird matters most in rounds 1-2',
  pink: 'played late – it only fires on other players’ turns, so it needs many rounds',
  copy: 'there is nothing good to copy in that row',
  predator: 'the deck is full of big birds, or the row has no other engine',
  tuck: 'your hand is empty – it needs cards to tuck',
  cache: 'you need the food for birds instead',
  egg: 'you have no plan for eggs (egg goals, bonus cards, bird costs)',
  food: 'the food it gives is not the food your birds eat',
  cards: 'you cannot afford to play the cards you draw',
  none: 'played early – a no-power bird does nothing for your engine',
  other: 'its condition does not fit your board',
};
const order = { S: 0, A: 1, B: 2, C: 3, D: 4 };
const rank = (a, b) => order[a.tier] - order[b.tier] || b.s - a.s;
const icons = t => t.replace(/\[([a-z ]+)\]/g, '$1');

const advice = [];
for (const r of birds) {
  const c = cat(r);
  const hab = r.habs.map(h => habShort[h]).join('/');
  const foods = ['Invertebrate', 'Seed', 'Fish', 'Fruit', 'Rodent', 'Nectar', 'Wild (food)']
    .filter(f => r.b[f]).map(f => `${r.b[f]} ${f.replace(' (food)', '').toLowerCase()}`);
  const food = foods.join(r.b['/ (food cost)'] ? ' or ' : ' + ') || 'free';
  const color = { brown: 'when activated', white: 'when played', pink: 'once between turns', teal: 'round end', yellow: 'game end' }[r.b.Color] || '';
  let purpose = `**Tier ${r.tier}** · ${setLabel[r.b.Set] || r.b.Set} · ${hab} · ${food} · ${r.vp} pts · ${r.b['Egg limit']} eggs · ${r.b['Nest type']} nest · wingspan ${r.b.Wingspan} cm\n`;
  purpose += P(r) ? `- **Power (${color}):** ${icons(P(r))}\n` : '- **No power.**\n';
  purpose += `- **Why:** ${r.why}${r.notes.length ? '; ' + r.notes.join(', ') : ''}.`;
  if (isRaven(r)) purpose += '\n- **Raven note:** you don’t play ravens – this is what an opponent gets.';
  const lines = [];
  for (const [pc, reason] of partners[c] || []) {
    const pick = birds.filter(x => x !== r && !isRaven(x) && cat(x) === pc && x.habs.some(h => r.habs.includes(h)) && order[x.tier] <= 1)
      .sort(rank).slice(0, 3);
    if (pick.length) lines.push(`- **Same row:** ${pick.map(x => x.b['Common name']).join(', ')} – ${reason}.`);
  }
  const bb = bonusAll.filter(x => r.b[x['Bonus card']] === 'X').map(x => x['Bonus card']);
  if (bb.length) lines.push(`- **Counts for bonus cards:** ${bb.join(', ')}.`);
  if (r.b['Egg limit'] >= 4) lines.push('- **Egg plans:** Breeding Manager, Oologist and egg round goals.');
  if (r.b['Nest type'] === 'wild') lines.push('- **Wild nest:** counts for every nest bonus card and nest round goal.');
  const avoid = [`- When ${weak[c]}.`];
  if (r.cost >= 3) avoid.push('- When food is short – 3 food is a whole gain-food action.');
  if (r.b['Egg limit'] <= 1) avoid.push(`- In an egg plan – it holds only ${r.b['Egg limit']} egg${r.b['Egg limit'] === 1 ? '' : 's'}.`);
  advice.push({ game: 'wingspan', title: r.b['Common name'], group: `Tier ${r.tier}`, purpose, useOn: lines.join('\n') || '- Any row it fits.', avoidOn: avoid.join('\n'), example: '' });
}

// Bonus cards: 25 are marked per bird in the data; the rest depend on how you build your board.
const boardHelp = {
  'Avian Theriogenologist': ['birds with small egg limits (1-2) plus egg layers to fill them', 'A', 'your birds hold 5-6 eggs each – filling them takes too long'],
  'Behaviorist': ['a mix of brown, white and pink/teal powers in each column', 'S', 'you play only brown powers'],
  'Breeding Manager': ['birds that hold 4+ eggs and egg-laying powers', 'S', 'your birds hold 1-3 eggs'],
  'Citizen Scientist': ['tuck birds (flocking) spread over many birds', 'A', 'you have no tuck engine'],
  'Ecologist': ['a balanced board – keep your smallest habitat growing', 'S', 'you go all-in on one habitat'],
  'Ethologist': ['one habitat with all power colours (brown, white, pink, teal, yellow)', 'A', 'you only have brown birds'],
  'Omnivore Specialist': ['birds whose cost includes a wild food', 'B', 'few wild-cost birds show up'],
  'Oologist': ['many cheap birds with at least 1 egg each', 'S', 'you play few, big birds'],
  'Pellet Dissector': ['predators and birds that cache fish or rodents', 'B', 'you have no cache or predator birds'],
  'Visionary Leader': ['card-draw birds; keep 5-8 cards in hand at the end', 'S', 'you must play every card you draw'],
  'Winter Feeder': ['food engines; stop spending food in the last turns', 'A', 'you need every food for birds'],
  'Mechanical Engineer': ['one bird of each nest type (wild nests count as any)', 'B', 'your birds share one nest type'],
  'Site Selection Expert': ['matching nests in the same column', 'B', 'you place birds without looking at nests'],
};
const ordered = ['consecutive birds in that habitat with steadily rising or falling numbers – plan the order you play them', 'C', 'you cannot choose the order of your birds'];
const nestTypes = ['different nest types in that habitat – wild nests help', 'B', 'your birds there share one nest type'];
for (const x of bonusAll) {
  const name = x['Bonus card'];
  const fits = birds.filter(r => r.b[name] === 'X');
  const share = Math.round(100 * fits.length / birds.length);
  let tier, use, avoid;
  if (fits.length) {
    tier = ['Visionary Leader', 'Oologist', 'Breeding Manager', 'Behaviorist', 'Ecologist'].includes(name) ? 'S' : share >= 25 ? 'A' : share >= 12 ? 'B' : 'C';
    const best = fits.filter(r => !isRaven(r)).sort(rank).slice(0, 10).map(r => `${r.b['Common name']} (${r.tier})`);
    use = `- **${fits.length} of ${birds.length} digital birds fit (${share} %).** Best of them: ${best.join(', ')}.\n- Search a bird name to see which bonus cards it counts for.`;
    avoid = `- When your hand has fewer than 3 birds that fit${share < 12 ? ' – few birds fit, so it pulls you toward weak cards' : ''}.`;
  } else {
    const h = boardHelp[name] || (/Population Monitor/.test(name) ? nestTypes : /Data Analyst|Ranger/.test(name) ? ordered : ['birds that match its condition', 'B', 'your birds do not match']);
    tier = h[1];
    use = `- **Fits:** ${h[0]}.\n- It depends on how you build your board, not on single birds.`;
    avoid = `- When ${h[2]}.`;
  }
  const vp = icons((x.VP || '').replace(/\[point\]/g, ' pts'));
  let purpose = `**Tier ${tier}** · Bonus card · ${setLabel[x.Set] || x.Set}\n- **Scores:** ${icons(x.Condition || '')} – ${vp}.`;
  if (x['Explanatory text']) purpose += `\n- ${x['Explanatory text'].replace(/<[^>]+>/g, ' ').trim()}`;
  advice.push({ game: 'wingspan', title: name, group: 'Bonus cards', purpose, useOn: use, avoidOn: avoid, example: '' });
}
fs.writeFileSync(path.join(dir, 'wingspan-advice.json'), JSON.stringify(advice));

// Data for the start helper page (/game/wingspan/start): what it needs to choose birds, food and a bonus card.
const foodKeys = { Invertebrate: 'invertebrate', Seed: 'seed', Fish: 'fish', Fruit: 'fruit', Rodent: 'rodent', Nectar: 'nectar', 'Wild (food)': 'wild' };
const start = {
  birds: birds.map(r => ({
    name: r.b['Common name'], tier: r.tier, score: Math.round(r.s * 10) / 10, set: setLabel[r.b.Set] || r.b.Set,
    habitats: r.habs.map(h => habShort[h]),
    food: Object.fromEntries(Object.entries(foodKeys).filter(([k]) => r.b[k]).map(([k, v]) => [v, r.b[k]])),
    foodOr: !!r.b['/ (food cost)'], cost: r.cost, points: r.vp, eggs: r.b['Egg limit'] || 0, nest: r.b['Nest type'] || '',
    color: r.b.Color || '', kind: cat(r), power: icons(P(r)), why: r.why, raven: isRaven(r),
    bonus: bonusAll.filter(x => r.b[x['Bonus card']] === 'X').map(x => x['Bonus card']),
  })),
  bonus: advice.filter(a => a.group === 'Bonus cards').map(a => ({ name: a.title, tier: a.purpose.match(/Tier (\w)/)[1], scores: a.purpose.split('\n')[1].replace('- **Scores:** ', ''), fits: a.useOn })),
};
fs.writeFileSync(path.join(dir, 'wingspan-start.json'), JSON.stringify(start));
console.log('advice', advice.length, 'birds', birds.length, 'bonus', bonusAll.length);
