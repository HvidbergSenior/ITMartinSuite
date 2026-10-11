Wingspan tier lists + card lookup for the Strategi app.
Needs wsdata/master.json and wsdata/bonus.json from https://github.com/navarog/wingsearch (src/assets/data) next to the scripts.
node wingspan_tiers.js  -> wingspan-tiers.json  (POST /api/guides)
node wingspan_lookup.js -> wingspan-advice.json (POST /api/advice, upsert by title)
