// Birthday front page (Components/Fest.razor): heart confetti on arrival, rotating messages, photo slideshow, Happy Birthday via WebAudio.
window.fest = (() => {
    const pal = ['#ffd76e', '#ff9dbb', '#c2185b', '#fff5f7', '#e8648f'];
    const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const msgs = [
        'Vibeke holder sammen på det hele – derfor kan Martin koncentrere sig om ITMartin 💛',
        'Hun tror på idéerne – også de skøre – og siger ærligt til, når noget skal laves om',
        'Mor til tre drenge – og har stadig humøret i behold 😄',
        'Ude i al slags vejr: løb, cykel og gåture – og altid hjem med et smil 🏃‍♀️',
        'Det var hendes elmåler-nøgle, der fik MinElpris til at virke første gang ⚡',
        'Verdens bedste kone. Tillykke, Vibz! 🎂',
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
        const heart = confetti.shapeFromPath ? [confetti.shapeFromPath({ path: 'M12 21C5 15 0 11 0 6 0 2.7 2.7 0 6 0c2.6 0 4.6 1.6 6 3.5C13.4 1.6 15.4 0 18 0c3.3 0 6 2.7 6 6 0 5-5 9-12 15z' })] : undefined;
        const o = { colors: pal, shapes: heart, scalar: 1.6 };
        confetti({ ...o, particleCount: 120, spread: 110, origin: { y: .3 } });
        setTimeout(() => confetti({ ...o, particleCount: 50, angle: 60, spread: 60, origin: { x: 0, y: .7 } }), 250);
        setTimeout(() => confetti({ ...o, particleCount: 50, angle: 120, spread: 60, origin: { x: 1, y: .7 } }), 400);
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
            const pics = document.querySelectorAll('.fest-show img'); if (!pics.length) return;   // slideshow, same beat as the messages
            const k = [...pics].findIndex(p => p.classList.contains('on'));
            pics[k < 0 ? 0 : k].classList.remove('on'); pics[(k + 1) % pics.length].classList.add('on');
        }, 4500);
    }
    // Blazor renders the page after this script loads - look for the block a few times.
    [300, 1200, 3000].forEach(ms => setTimeout(start, ms));
    return { boom, song, start };
})();
