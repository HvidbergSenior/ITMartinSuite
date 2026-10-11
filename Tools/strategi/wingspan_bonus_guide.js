// Wingspan guide #19 "Tier list: bonus cards" body, built from wingspan-advice.json (run wingspan_lookup.js first).
const fs = require('fs');
const path = require('path');
const dir = __dirname;

const bonus = require(path.join(dir, 'wingspan-advice.json')).filter(a => a.group === 'Bonus cards').map(a => {
  const [head, scores] = a.purpose.split('\n');
  const [, tier, set] = head.match(/Tier (\w)\*\* · Bonus card · (.+)$/);
  const fit = a.useOn.split('\n')[0].replace(/^- /, '');
  const pct = (fit.match(/\((\d+) %\)/) || [])[1];
  return { name: a.title, tier, set, scores: scores.replace('- **Scores:** ', ''), fit, pct: pct ? +pct : null, avoid: a.avoidOn.replace(/^- /, '') };
});

const tiers = [
  ['S', 'easy points in almost every game'],
  ['A', 'good when your hand fits'],
  ['B', 'needs a plan from round 1'],
  ['C', 'avoid unless your hand already has 3+ birds for it'],
];
const setOrder = ['Base game', 'European expansion', 'Oceania expansion', 'Asia expansion'];
const sort = (a, b) => (b.pct ?? 0) - (a.pct ?? 0) || a.name.localeCompare(b.name);
const short = s => s.replace(/^Asia expansion$/, 'Asia').replace(/^(European|Oceania) expansion$/, '$1').replace(/^Base game$/, 'Base');

const out = [];
out.push('## How to pick your starting bonus card');
out.push('- Keep the one that matches **the birds in your starting hand** and your planned engine.');
out.push('- **Share of birds that fit** is the quick guide: 30 %+ is easy, under 15 % is hard. Shares below are out of all 596 birds in the digital game (base, European, Oceania, Asia + Bird Packs).');
out.push('- Cards marked "board" do not depend on single birds but on how you build your board.');
out.push('- All bonus cards from every expansion are here, with the set in brackets. Search a card name (🔎) for its best birds.');
for (const [t, label] of tiers) {
  const list = bonus.filter(b => b.tier === t).sort(sort);
  if (!list.length) continue;
  out.push('', `## ${t} tier – ${label}`);
  for (const b of list) {
    const fit = b.pct != null ? `${b.pct} % of birds fit` : `board: ${b.fit.replace(/^\*\*Fits:\*\* /, '').replace(/\.$/, '')}`;
    out.push(`- **${b.name}** (${short(b.set)}) – ${b.scores.replace(/\.$/, '').replace(/\b1 pts/g, '1 pt')}. ${fit[0].toUpperCase() + fit.slice(1)}.`);
  }
}
out.push('', '## By set');
for (const s of setOrder) {
  const list = bonus.filter(b => b.set === s).sort((a, b) => 'SABCD'.indexOf(a.tier) - 'SABCD'.indexOf(b.tier) || a.name.localeCompare(b.name));
  out.push(`- **${s}** (${list.length}): ${list.map(b => `${b.name} ${b.tier}`).join(', ')}.`);
}
const other = bonus.filter(b => !setOrder.includes(b.set));
if (other.length) out.push(`- **Other** (${other.length}): ${other.map(b => `${b.name} ${b.tier} (${b.set})`).join(', ')}.`);
out.push('', '## Raven side note');
out.push('- Ravens empty eggs: if you face a raven player, the egg bonus cards (Oologist, Breeding Manager, Avian Theriogenologist) are weaker for you, because a raven player can empty your eggs.');

const body = out.join('\n');
fs.writeFileSync(path.join(dir, 'wingspan-bonus-guide.md'), body);
console.log('bonus', bonus.length, 'chars', body.length, tiers.map(([t]) => t + bonus.filter(b => b.tier === t).length).join(' '));
