// Birthday front page (Components/Fest.razor): confetti on arrival, rotating messages, Happy Birthday via WebAudio.
window.fest = (() => {
    const pal = ['#ffc21a', '#ff6a1a', '#19c3b1', '#ff4f8b', '#fff1dc'];
    const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const msgs = [
        'Født i 1977 – samme år som Star Wars og discoens storhedstid 🪩',
        'Level 49 låst op – sidste år i 40\'erne 🎮',
        'Ønsker sig: tre ting på én gang. Musik, guitar og et strategispil 🎸',
        'Har bygget over 20 apps – og et par stykker til i dag 🐦',
        'Taber stadig til Eigil i Duel. Også i dag 🃏',
        'Næste år er det de store 50 🎉',
    ];
    let loaded = null, playing = false;
    function lib() {
        if (window.confetti) return Promise.resolve();
        return loaded ??= new Promise(ok => {
            const s = document.createElement('script');
            s.src = 'https://cdn.jsdelivr.net/npm/canvas-confetti@1.9.3/dist/confetti.browser.min.js';
            s.onload = ok;
            s.onerror = () => { loaded = null; const b = document.createElement('script');   // backup CDN
                b.src = 'https://unpkg.com/canvas-confetti@1.9.3/dist/confetti.browser.js'; b.onload = ok; document.head.appendChild(b); };
            document.head.appendChild(s);
        });
    }
    async function boom(auto) {
        if (reduce && auto === true) return;   // "reduce motion" (e.g. iPhone setting): no automatic burst, the button still works
        await lib();
        confetti({ particleCount: 160, spread: 110, origin: { y: .3 }, colors: pal });
        setTimeout(() => confetti({ particleCount: 70, angle: 60, spread: 60, origin: { x: 0, y: .7 }, colors: pal }), 250);
        setTimeout(() => confetti({ particleCount: 70, angle: 120, spread: 60, origin: { x: 1, y: .7 }, colors: pal }), 400);
    }
    function song() {
        if (playing) return; playing = true;
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const f = m => 440 * Math.pow(2, (m - 69) / 12);
        const tune = [[67,.75],[67,.25],[69,1],[67,1],[72,1],[71,2], [67,.75],[67,.25],[69,1],[67,1],[74,1],[72,2],
                      [67,.75],[67,.25],[79,1],[76,1],[72,1],[71,1],[69,2], [77,.75],[77,.25],[76,1],[72,1],[74,1],[72,3]];
        const bass = [48,48,48,48,48,43, 43,43,43,43,43,48, 48,48,48,48,41,41,41, 41,41,48,48,43,48];
        let t = ctx.currentTime + .1; const beat = .38;
        const tone = (fr, at, dur, type, vol) => {
            const o = ctx.createOscillator(), g = ctx.createGain(); o.type = type; o.frequency.value = fr;
            g.gain.setValueAtTime(0, at); g.gain.linearRampToValueAtTime(vol, at + .02); g.gain.exponentialRampToValueAtTime(.001, at + dur);
            o.connect(g).connect(ctx.destination); o.start(at); o.stop(at + dur + .05);
        };
        tune.forEach(([m, d], i) => { tone(f(m), t, d * beat + .1, 'triangle', .25); tone(f(bass[i] - 12), t, d * beat, 'sawtooth', .04); t += d * beat; });
        setTimeout(() => { playing = false; boom(); }, (t - ctx.currentTime) * 1000);
    }
    let n = 0, timer = null;
    function start() {
        const el = document.getElementById('fest-msg');
        if (!el) { clearInterval(timer); timer = null; return; }
        if (timer) return;
        boom(true);
        timer = setInterval(() => {
            const m = document.getElementById('fest-msg'); if (!m) return;
            m.classList.add('out');
            setTimeout(() => { n = (n + 1) % msgs.length; m.textContent = msgs[n]; m.classList.remove('out'); }, 350);
        }, 4500);
    }
    // Blazor renders the page after this script loads - look for the block a few times.
    [300, 1200, 3000].forEach(ms => setTimeout(start, ms));
    return { boom, song, start };
})();
