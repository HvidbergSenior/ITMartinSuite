window.starrealms = (function() {

    var getItem = function(k) { return localStorage.getItem(k); };
    var setItem = function(k, v) { localStorage.setItem(k, v); };
    var removeItem = function(k) { localStorage.removeItem(k); };

    // ── Sound (synthesized, no audio files needed) ──────────────────────────
    var ctx = null;
    function audioCtx() {
        if (!ctx) {
            var AC = window.AudioContext || window.webkitAudioContext;
            ctx = new AC();
        }
        if (ctx.state === "suspended") ctx.resume().catch(function() {});
        return ctx;
    }

    function tone(freqStart, freqEnd, duration, type, gainPeak) {
        try {
            var c = audioCtx();
            var osc = c.createOscillator();
            var gain = c.createGain();
            osc.type = type || "sine";
            osc.frequency.setValueAtTime(freqStart, c.currentTime);
            osc.frequency.exponentialRampToValueAtTime(Math.max(freqEnd, 1), c.currentTime + duration);
            gain.gain.setValueAtTime(0, c.currentTime);
            gain.gain.linearRampToValueAtTime(gainPeak || 0.15, c.currentTime + 0.01);
            gain.gain.exponentialRampToValueAtTime(0.001, c.currentTime + duration);
            osc.connect(gain);
            gain.connect(c.destination);
            osc.start(c.currentTime);
            osc.stop(c.currentTime + duration);
        } catch (e) { /* audio not available */ }
    }

    function playHit() {
        // sharp descending laser-zap
        tone(700, 120, 0.18, "sawtooth", 0.12);
    }

    function playHeal() {
        // bright ascending chime
        tone(400, 900, 0.22, "triangle", 0.12);
    }

    function playEliminate() {
        tone(300, 60, 0.6, "sawtooth", 0.18);
    }

    function playWinner() {
        var c = audioCtx();
        [523, 659, 784, 1047].forEach(function(f, i) {
            setTimeout(function() { tone(f, f, 0.3, "triangle", 0.15); }, i * 140);
        });
    }

    function vibrate(ms) {
        if (navigator.vibrate) { try { navigator.vibrate(ms); } catch (e) {} }
    }

    // ── Confetti (lightweight canvas, no deps) ──────────────────────────────
    function confetti() {
        var canvas = document.createElement("canvas");
        canvas.style.position = "fixed";
        canvas.style.top = "0";
        canvas.style.left = "0";
        canvas.style.width = "100%";
        canvas.style.height = "100%";
        canvas.style.pointerEvents = "none";
        canvas.style.zIndex = "9999";
        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;
        document.body.appendChild(canvas);
        var ctx2d = canvas.getContext("2d");

        var colors = ["#e74c3c", "#3498db", "#2ecc71", "#f1c40f", "#9b59b6", "#e67e22"];
        var pieces = [];
        for (var i = 0; i < 150; i++) {
            pieces.push({
                x: Math.random() * canvas.width,
                y: -20 - Math.random() * canvas.height * 0.5,
                w: 6 + Math.random() * 6,
                h: 8 + Math.random() * 10,
                color: colors[Math.floor(Math.random() * colors.length)],
                vy: 2 + Math.random() * 3,
                vx: -2 + Math.random() * 4,
                rot: Math.random() * 360,
                vr: -8 + Math.random() * 16
            });
        }

        var start = Date.now();
        function frame() {
            var elapsed = Date.now() - start;
            ctx2d.clearRect(0, 0, canvas.width, canvas.height);
            pieces.forEach(function(p) {
                p.x += p.vx; p.y += p.vy; p.rot += p.vr;
                ctx2d.save();
                ctx2d.translate(p.x, p.y);
                ctx2d.rotate(p.rot * Math.PI / 180);
                ctx2d.fillStyle = p.color;
                ctx2d.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
                ctx2d.restore();
            });
            if (elapsed < 3500) {
                requestAnimationFrame(frame);
            } else {
                document.body.removeChild(canvas);
            }
        }
        requestAnimationFrame(frame);
    }

    return {
        getItem: getItem,
        setItem: setItem,
        removeItem: removeItem,
        playHit: playHit,
        playHeal: playHeal,
        playEliminate: playEliminate,
        playWinner: playWinner,
        vibrate: vibrate,
        confetti: confetti
    };
})();
