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

    // Filtered white-noise burst - the basis for thuds, crunches, farts and
    // other "physical" sounds a pure oscillator can't produce.
    function noiseBurst(duration, gainPeak, filterType, filterFreq, filterQ) {
        try {
            var c = audioCtx();
            var size = Math.max(1, Math.floor(c.sampleRate * duration));
            var buffer = c.createBuffer(1, size, c.sampleRate);
            var data = buffer.getChannelData(0);
            for (var i = 0; i < size; i++) data[i] = Math.random() * 2 - 1;
            var src = c.createBufferSource();
            src.buffer = buffer;
            var filter = c.createBiquadFilter();
            filter.type = filterType || "lowpass";
            filter.frequency.setValueAtTime(filterFreq || 800, c.currentTime);
            if (filterQ) filter.Q.setValueAtTime(filterQ, c.currentTime);
            var gain = c.createGain();
            gain.gain.setValueAtTime(0, c.currentTime);
            gain.gain.linearRampToValueAtTime(gainPeak || 0.15, c.currentTime + 0.005);
            gain.gain.exponentialRampToValueAtTime(0.001, c.currentTime + duration);
            src.connect(filter);
            filter.connect(gain);
            gain.connect(c.destination);
            src.start(c.currentTime);
            src.stop(c.currentTime + duration);
        } catch (e) { /* audio not available */ }
    }

    // A pitch-sweeping oscillator with an LFO wobbling its frequency - the
    // basis for farts, quacks, kazoos and other comedic "wah-wah" tones.
    function wobbleTone(freqStart, freqEnd, duration, type, gainPeak, wobbleRate, wobbleDepth) {
        try {
            var c = audioCtx();
            var osc = c.createOscillator();
            var lfo = c.createOscillator();
            var lfoGain = c.createGain();
            var gain = c.createGain();
            osc.type = type || "sawtooth";
            osc.frequency.setValueAtTime(freqStart, c.currentTime);
            osc.frequency.exponentialRampToValueAtTime(Math.max(freqEnd, 1), c.currentTime + duration);
            lfo.frequency.setValueAtTime(wobbleRate || 30, c.currentTime);
            lfoGain.gain.setValueAtTime(wobbleDepth || 40, c.currentTime);
            lfo.connect(lfoGain);
            lfoGain.connect(osc.frequency);
            gain.gain.setValueAtTime(0, c.currentTime);
            gain.gain.linearRampToValueAtTime(gainPeak || 0.15, c.currentTime + 0.01);
            gain.gain.exponentialRampToValueAtTime(0.001, c.currentTime + duration);
            osc.connect(gain);
            gain.connect(c.destination);
            osc.start(c.currentTime); lfo.start(c.currentTime);
            osc.stop(c.currentTime + duration); lfo.stop(c.currentTime + duration);
        } catch (e) { /* audio not available */ }
    }

    // ~30 different "you got hit" sound flavors, picked at random each time
    // so repeated damage during a game doesn't turn into the same beep over
    // and over. Each takes the magnitude tier (duration/gain, already scaled
    // by how many points were lost) so a small ding and a big shot both keep
    // the "bigger hit = bigger sound" feel regardless of flavor. A handful
    // are silly on purpose (fart, quack, boing, kazoo, trombone).
    var HIT_SOUNDS = [
        function(t) { tone(720, 90, t.dur, "sawtooth", t.gain); },                                   // classic zap
        function(t) { tone(600, 40, t.dur, "square", t.gain); },                                     // 8-bit hit
        function(t) { tone(1400, 200, t.dur * 0.7, "sawtooth", t.gain); },                            // laser
        function(t) { noiseBurst(t.dur, t.gain, "lowpass", 300, 1); },                                // dull thud
        function(t) { noiseBurst(t.dur * 0.6, t.gain * 1.1, "bandpass", 1200, 4); },                  // crack
        function(t) { tone(900, 100, t.dur, "square", t.gain * 0.9); setTimeout(function() { noiseBurst(t.dur * 0.4, t.gain * 0.6, "lowpass", 500); }, t.dur * 300); }, // metallic clank
        function(t) { tone(2200, 1800, 0.05, "square", t.gain); setTimeout(function() { tone(2200, 1800, 0.05, "square", t.gain); }, 90); }, // alarm blip x2
        function(t) { tone(300, 900, t.dur * 0.5, "sine", t.gain * 0.8); setTimeout(function() { tone(900, 60, t.dur * 0.6, "sawtooth", t.gain); }, t.dur * 200); }, // sci-fi pew-thud
        function(t) { noiseBurst(t.dur, t.gain, "highpass", 3000, 2); },                              // glass shatter
        function(t) { tone(150, 40, t.dur, "square", t.gain); noiseBurst(t.dur, t.gain * 0.7, "lowpass", 200); }, // explosion crackle
        function(t) { tone(80, 55, t.dur * 1.2, "sine", t.gain * 1.1); },                             // deep gong thump
        function(t) { tone(500, 480, 0.03, "square", t.gain); setTimeout(function() { tone(500, 480, 0.03, "square", t.gain); }, 60); setTimeout(function() { tone(500, 480, 0.03, "square", t.gain); }, 120); }, // robotic beep-boop-beep
        function(t) { tone(1000, 20, t.dur, "triangle", t.gain); },                                   // power-down whirr
        function(t) { noiseBurst(t.dur * 0.5, t.gain, "bandpass", 200, 8); },                         // punch thud
        function(t) { tone(2500, 400, t.dur * 0.8, "sawtooth", t.gain * 0.8); },                      // whip crack
        function(t) { tone(60, 60, t.dur, "square", t.gain); },                                       // anvil clang (flat low buzz)
        function(t) { noiseBurst(t.dur, t.gain * 0.9, "lowpass", 900); tone(400, 100, t.dur * 0.5, "sawtooth", t.gain * 0.6); }, // crash bang
        function(t) { tone(1600, 1900, 0.05, "sine", t.gain); setTimeout(function() { tone(1900, 1500, 0.05, "sine", t.gain); }, 60); }, // radio static pop
        // ── funny / comedic ──────────────────────────────────────────────
        function(t) { wobbleTone(180, 45, t.dur * 1.3, "sawtooth", t.gain, 55, 60); },                // fart
        function(t) { wobbleTone(150, 35, t.dur * 1.5, "square", t.gain * 1.1, 40, 80); },            // wet raspberry
        function(t) { tone(1200, 1800, 0.08, "square", t.gain); setTimeout(function() { tone(1800, 900, 0.1, "square", t.gain * 0.8); }, 70); }, // squeaky toy
        function(t) { tone(200, 600, t.dur * 0.5, "sine", t.gain); setTimeout(function() { tone(600, 200, t.dur * 0.5, "sine", t.gain); }, t.dur * 300); }, // boing/spring
        function(t) { tone(500, 130, t.dur, "sawtooth", t.gain); setTimeout(function() { tone(400, 100, t.dur, "sawtooth", t.gain * 0.8); }, t.dur * 350); }, // "womp womp" trombone
        function(t) { tone(1800, 400, t.dur, "triangle", t.gain); },                                  // slide whistle down
        function(t) { wobbleTone(350, 250, t.dur, "sawtooth", t.gain, 90, 100); },                    // duck quack
        function(t) { wobbleTone(90, 70, t.dur * 1.4, "sawtooth", t.gain * 1.1, 12, 20); },           // cow moo
        function(t) { wobbleTone(260, 260, t.dur, "square", t.gain, 25, 15); },                       // kazoo buzz
        function(t) { noiseBurst(t.dur, t.gain, "lowpass", 600); tone(900, 200, t.dur * 0.4, "sine", t.gain * 0.5); }, // balloon deflate
        function(t) { tone(150, 400, 0.06, "sawtooth", t.gain); setTimeout(function() { tone(400, 100, t.dur, "sawtooth", t.gain * 0.9); }, 65); }, // cartoon "doh" honk
        function(t) { wobbleTone(220, 180, t.dur * 1.2, "square", t.gain, 18, 50); },                 // gurgle/burp
    ];

    // Sound + vibration scale with the SIZE of a point change, not just its
    // direction - a 1-point nudge is a joke, a 50-point swing is an event.
    // Loss (positive=false) picks a random flavor from HIT_SOUNDS so repeated
    // damage doesn't sound identical every time; gain uses a rising triangle
    // "heal" - both share the same magnitude tiers so a -20 and a +20 feel
    // equally significant, just tonally opposite.
    function playImpact(amount, positive) {
        var a = Math.max(1, Math.round(Math.abs(amount)));

        if (a === 1) {
            // A single point deserves a joke, not a hit - a little mouse squeak.
            tone(2000, 2700, 0.08, "sine", 0.09);
            setTimeout(function() { tone(2500, 1700, 0.06, "sine", 0.07); }, 55);
            return;
        }

        var tier =
            a <= 3  ? { dur: 0.16, gain: 0.10, layers: 1 } :  // small
            a <= 7  ? { dur: 0.22, gain: 0.14, layers: 1 } :  // average
            a <= 14 ? { dur: 0.30, gain: 0.19, layers: 2 } :
            a <= 19 ? { dur: 0.38, gain: 0.23, layers: 2 } :  // above average
            a <= 24 ? { dur: 0.44, gain: 0.26, layers: 3 } :  // a little above average
            a <= 34 ? { dur: 0.52, gain: 0.30, layers: 3 } :  // much
                      { dur: 0.75, gain: 0.40, layers: 4 };   // LOUD (50+)

        if (positive) {
            for (var i = 0; i < tier.layers; i++) {
                (function(i) {
                    setTimeout(function() { tone(380 + i * 60, 950 + i * 120, tier.dur, "triangle", tier.gain); }, i * 30);
                })(i);
            }
            return;
        }

        HIT_SOUNDS[Math.floor(Math.random() * HIT_SOUNDS.length)](tier);
    }

    function playClick() { tone(900, 1200, 0.045, "square", 0.05); }
    function playEliminate() { tone(300, 60, 0.6, "sawtooth", 0.18); }
    function playWinner() {
        [523, 659, 784, 1047].forEach(function(f, i) {
            setTimeout(function() { tone(f, f, 0.3, "triangle", 0.15); }, i * 140);
        });
    }

    function vibrate(ms) {
        if (navigator.vibrate) { try { navigator.vibrate(ms); } catch (e) {} }
    }

    function vibrateForAmount(amount) {
        vibrate(Math.min(300, 10 + Math.max(1, Math.abs(amount)) * 5));
    }

    // ── Screen wake lock (keep the display on during an active game) ────────
    var wakeLock = null;
    var wakeLockWanted = false;

    function requestWakeLock() {
        wakeLockWanted = true;
        if (!("wakeLock" in navigator)) return;
        navigator.wakeLock.request("screen").then(function(lock) {
            wakeLock = lock;
            lock.addEventListener("release", function() { wakeLock = null; });
        }).catch(function() { /* not available / permission denied - fail silently */ });
    }

    document.addEventListener("visibilitychange", function() {
        // The OS releases the lock whenever the tab is hidden (app switch,
        // screen off) - re-acquire it once the player comes back so it
        // doesn't just stop working after the first backgrounding.
        if (document.visibilityState === "visible" && wakeLockWanted && wakeLock === null) {
            requestWakeLock();
        }
    });

    // ── Point-change burst (small localized particle pop, not full-screen) ──
    function burst(x, y, color, count) {
        var canvas = document.createElement("canvas");
        canvas.style.position = "fixed";
        canvas.style.top = "0";
        canvas.style.left = "0";
        canvas.style.width = "100%";
        canvas.style.height = "100%";
        canvas.style.pointerEvents = "none";
        canvas.style.zIndex = "9998";
        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;
        document.body.appendChild(canvas);
        var ctx2d = canvas.getContext("2d");

        var n = count || 20;
        var particles = [];
        for (var i = 0; i < n; i++) {
            var angle = (Math.PI * 2 * i) / n + Math.random() * 0.3;
            var speed = 3 + Math.random() * 5;
            particles.push({
                x: x, y: y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                r: 3 + Math.random() * 4,
                life: 1
            });
        }

        var start = Date.now();
        function frame() {
            var elapsed = Date.now() - start;
            ctx2d.clearRect(0, 0, canvas.width, canvas.height);
            particles.forEach(function(p) {
                p.x += p.vx; p.y += p.vy; p.vy += 0.15; p.life -= 0.03;
                ctx2d.globalAlpha = Math.max(p.life, 0);
                ctx2d.fillStyle = color;
                ctx2d.beginPath();
                ctx2d.arc(p.x, p.y, p.r, 0, Math.PI * 2);
                ctx2d.fill();
            });
            ctx2d.globalAlpha = 1;
            if (elapsed < 650) {
                requestAnimationFrame(frame);
            } else {
                document.body.removeChild(canvas);
            }
        }
        requestAnimationFrame(frame);
    }

    // positive=true -> green sparkle burst (gain); false -> red/orange
    // "explosion" burst (loss), centered on the given element. amount scales
    // the burst size the same way playImpact scales the sound - a 1-point
    // change gets a token pop, a 50-point shot gets a real explosion.
    function explodeAt(el, positive, amount) {
        if (!el) return;
        var rect = el.getBoundingClientRect();
        var x = rect.left + rect.width / 2;
        var y = rect.top + rect.height / 2;
        var a = Math.max(1, Math.abs(amount || 1));
        var scale = Math.min(3, 0.5 + a / 15);
        if (positive) burst(x, y, "#2ecc71", Math.round(16 * scale));
        else { burst(x, y, "#e74c3c", Math.round(26 * scale)); burst(x, y, "#f1c40f", Math.round(10 * scale)); }
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
        playImpact: playImpact,
        playClick: playClick,
        playEliminate: playEliminate,
        playWinner: playWinner,
        vibrate: vibrate,
        vibrateForAmount: vibrateForAmount,
        confetti: confetti,
        requestWakeLock: requestWakeLock,
        explodeAt: explodeAt
    };
})();

// ─────────────────────────────────────────────────────────────────────────
// App logic: no Blazor Server interactivity, no SignalR - the Cloudflare
// Tunnel this app is served through kills long-lived connections, so every
// page here is static SSR + REST fetch + polling instead.
// ─────────────────────────────────────────────────────────────────────────

(function() {
    function esc(s) {
        var d = document.createElement("div");
        d.textContent = s == null ? "" : String(s);
        return d.innerHTML;
    }

    // A short, personal tag derived from the player's own name (initials),
    // shown in their chosen color - replaces picking from a fixed list of
    // generic space-themed emoji, so everyone's badge is actually theirs.
    function initialsFromName(name) {
        var parts = (name || "").trim().split(/\s+/).filter(Boolean);
        if (parts.length === 0) return "?";
        if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
        return (parts[0].charAt(0) + parts[1].charAt(0)).toUpperCase();
    }

    // Some endpoints return 200 with an empty body (Results.Ok() with no payload) rather
    // than 204 - calling response.json() directly on an empty body throws in Safari
    // ("String did not match the expected pattern"), so always read as text first.
    function parseJsonResponse(r) {
        return r.text().then(function(t) {
            if (!r.ok) throw new Error(t || r.statusText);
            return t ? JSON.parse(t) : null;
        });
    }

    function apiGet(url) {
        return fetch(url).then(parseJsonResponse);
    }

    function apiPost(url, body) {
        return fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body || {})
        }).then(parseJsonResponse);
    }

    function ensureLocalId(key) {
        var v = localStorage.getItem(key);
        if (!v) {
            v = (crypto.randomUUID ? crypto.randomUUID() : (Date.now() + "-" + Math.random())).replace(/-/g, "");
            localStorage.setItem(key, v);
        }
        return v;
    }

    // Most avatars are a plain emoji stored in the Avatar string, but a
    // profile can also point at a real image (e.g. "/img/itmartin.png") -
    // render that as a photo instead of text wherever an avatar shows up.
    function isImageAvatar(avatar) {
        return typeof avatar === "string" && (avatar.indexOf("/") !== -1 || avatar.indexOf("http") === 0);
    }
    function avatarHtml(avatar, cssClass) {
        if (isImageAvatar(avatar)) {
            return '<img class="' + (cssClass || "avatar-img") + '" src="' + esc(avatar) + '">';
        }
        return esc(avatar || "?");
    }

    // Big badge for a single-profile header (stats page): the emoji/picture
    // fills the whole circle and the name sits on top of it, so it reads
    // from across a room instead of a small icon next to body-sized text.
    function heroBadgeHtml(avatar, name, color) {
        var bg = isImageAvatar(avatar) ? "#000" : (color || "var(--bg3)");
        return '<div class="profile-hero-badge" style="background:' + bg + '">' +
            (isImageAvatar(avatar)
                ? '<img class="profile-hero-img" src="' + esc(avatar) + '">'
                : '<span class="profile-hero-emoji">' + esc(avatar || "?") + '</span>') +
            '<div class="profile-hero-name">' + esc(name) + '</div>' +
            '</div>';
    }

    function paintAvatarPreview(previewId, name, color) {
        var el = document.getElementById(previewId);
        if (!el) return "?";
        var initials = initialsFromName(name);
        el.textContent = initials;
        el.style.background = color;
        return initials;
    }

    function renderColorGrid(containerId, colors, selected, taken, onSelect) {
        var el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = colors.map(function(c) {
            var isTaken = taken.indexOf(c) !== -1 && c !== selected;
            var active = c === selected ? " color-btn--active" : "";
            var takenCls = isTaken ? " color-btn--taken" : "";
            return '<button class="color-btn' + active + takenCls + '" style="background:' + c + '" data-color="' + c + '"' + (isTaken ? " disabled" : "") + '></button>';
        }).join("");
        el.querySelectorAll(".color-btn:not([disabled])").forEach(function(btn) {
            btn.onclick = function() { onSelect(btn.getAttribute("data-color")); };
        });
    }

    var COLORS = ["#e74c3c", "#3498db", "#2ecc71", "#f1c40f", "#9b59b6", "#e67e22"];

    // ═══════════════════════════════════════════════════════════════════
    // HOME (/)
    // ═══════════════════════════════════════════════════════════════════

    function initHome() {
        var infoHidden = localStorage.getItem("sr_info_hidden") === "1";
        if (infoHidden) {
            document.getElementById("h-info-panel").style.display = "none";
            document.getElementById("h-info-toggle").textContent = "ℹ️ Vis info";
        }
        window.toggleInfo = function() {
            infoHidden = !infoHidden;
            document.getElementById("h-info-panel").style.display = infoHidden ? "none" : "";
            document.getElementById("h-info-toggle").textContent = infoHidden ? "ℹ️ Vis info" : "🙈 Skjul info";
            localStorage.setItem("sr_info_hidden", infoHidden ? "1" : "0");
        };

        var EMOJI_BASE = ["🚀","🛸","👽","🤖","🎯","⚔️","🏆","🔥","⭐","💥","🌟","🛡️","🪐","☄️","💫","🎮","🦾","🧨","⚡","👾","🐙"];

        var state = {
            avatar: "",
            color: localStorage.getItem("profile_color") || COLORS[0],
            rulesets: [],
            selectedRulesetId: null,
            selectedRuleset: null,
            startingPoints: 50,
            isRanked: true,
            profiles: [],
            emojiOptions: EMOJI_BASE.slice(),
            aiPictureUrl: null
        };

        document.getElementById("h-name").value = localStorage.getItem("profile_name") || "";

        function paintColors() { renderColorGrid("h-color-grid", COLORS, state.color, [], function(c) { state.color = c; paintColors(); }); }
        paintColors();

        function takenEmoji() {
            return state.profiles.map(function(p) { return p.avatar; }).filter(function(a) { return !isImageAvatar(a) && a; });
        }

        function renderEmojiGrid() {
            var taken = takenEmoji();
            var el = document.getElementById("h-emoji-grid");
            var cells = state.emojiOptions.map(function(e) {
                var isTaken = taken.indexOf(e) !== -1 && e !== state.avatar;
                var active = e === state.avatar ? " emoji-btn--active" : "";
                var takenAttr = isTaken ? " disabled title=\"Allerede i brug\"" : "";
                return '<button type="button" class="emoji-btn' + (isTaken ? " color-btn--taken" : "") + active + '" data-emoji="' + e + '"' + takenAttr + '>' + e + '</button>';
            });
            cells.unshift('<button type="button" class="emoji-btn emoji-btn--none' + (state.avatar === "" ? " emoji-btn--active" : "") + '" data-emoji="">Intet</button>');
            el.innerHTML = cells.join("");
            el.querySelectorAll(".emoji-btn:not([disabled])").forEach(function(btn) {
                btn.onclick = function() {
                    state.avatar = btn.getAttribute("data-emoji");
                    state.aiPictureUrl = null;
                    document.getElementById("h-ai-picture-preview").style.display = "none";
                    renderEmojiGrid();
                };
            });
        }

        window.requestMoreEmoji = function() {
            var btn = document.getElementById("h-emoji-more");
            btn.disabled = true;
            btn.textContent = "Genererer…";
            apiPost("/api/emoji-suggestions", { exclude: state.emojiOptions.concat(takenEmoji()) }).then(function(more) {
                if (more && more.length) state.emojiOptions = state.emojiOptions.concat(more);
                renderEmojiGrid();
            }).catch(function() {}).then(function() {
                btn.disabled = false;
                btn.textContent = "✨ Flere ikoner (AI)";
            });
        };

        window.generateProfilePicture = function() {
            var prompt = document.getElementById("h-ai-prompt").value.trim() || document.getElementById("h-name").value.trim();
            if (!prompt) return;
            var btn = document.getElementById("h-ai-generate");
            btn.disabled = true;
            btn.textContent = "Genererer…";
            apiPost("/api/profile-picture", { prompt: prompt }).then(function(res) {
                state.avatar = res.url;
                state.aiPictureUrl = res.url;
                var prev = document.getElementById("h-ai-picture-preview");
                prev.style.display = "";
                prev.innerHTML = '<img src="' + esc(res.url) + '"><span>Valgt som ikon</span>';
                renderEmojiGrid();
            }).catch(function() {
                showError("Kunne ikke generere billede - prøv igen.");
            }).then(function() {
                btn.disabled = false;
                btn.textContent = "🪄 Generér";
            });
        };

        window.onNameInput = function() {
            var name = document.getElementById("h-name").value.trim();
            renderNamePicker();

            var known = state.isRanked ? state.profiles.find(function(p) { return p.name.toLowerCase() === name.toLowerCase(); }) : null;
            var creator = document.getElementById("h-profile-creator");
            var colorSection = document.getElementById("h-color-section");
            var knownRow = document.getElementById("h-known-profile");
            var pinRow = document.getElementById("h-pin-row");
            var pinOk = true;
            if (!state.isRanked) {
                // Training still picks a name and a color like a real profile
                // - it just never becomes one (see saveOnGoToRuleset: isRanked
                // decides whether the result is ever attached to a
                // PlayerProfile, not whether the picker looks different).
                creator.style.display = "none";
                knownRow.style.display = "none";
                pinRow.style.display = "none";
                colorSection.style.display = name ? "" : "none";
                if (name) paintColors();
            } else if (!name) {
                creator.style.display = "none";
                colorSection.style.display = "none";
                knownRow.style.display = "none";
                pinRow.style.display = "none";
            } else if (known) {
                creator.style.display = "none";
                colorSection.style.display = "none";
                knownRow.style.display = "flex";
                document.getElementById("h-known-avatar").innerHTML = avatarHtml(known.avatar);
                state.avatar = known.avatar;
                state.color = known.color || state.color;
                pinRow.style.display = known.hasPin ? "" : "none";
                pinOk = !known.hasPin || document.getElementById("h-pin-input").value.trim().length > 0;
            } else {
                creator.style.display = "";
                colorSection.style.display = "";
                knownRow.style.display = "none";
                pinRow.style.display = "none";
                renderEmojiGrid();
                paintColors();
            }
            document.getElementById("h-btn-ruleset").disabled = !name || !pinOk;
        };
        window.onNameInput();

        // Only ~20-40 real players ever - show them all as one-tap buttons so
        // nobody has to type (and risk a typo splintering) a name that
        // already exists.
        var TRAINING_ADJECTIVES = ["Rap", "Rasende", "Snu", "Modig", "Hurtig", "List", "Vild", "Cool"];
        var TRAINING_NOUNS = ["Pirat", "Kommandør", "Ranger", "Merc", "Blob", "Drone", "Kaptajn", "Rumvæsen"];

        function generateTrainingName() {
            var a = TRAINING_ADJECTIVES[Math.floor(Math.random() * TRAINING_ADJECTIVES.length)];
            var n = TRAINING_NOUNS[Math.floor(Math.random() * TRAINING_NOUNS.length)];
            return a + n + Math.floor(Math.random() * 90 + 10);
        }

        // ITMartin is featured on its own; everyone else lives in a
        // collapsed "Andre navne" box so the picker doesn't turn into a wall
        // of buttons as the player list grows.
        function renderNamePicker() {
            var featuredEl = document.getElementById("h-name-featured");
            var otherBox = document.getElementById("h-name-other-box");
            var otherEl = document.getElementById("h-name-picker");
            if (!state.isRanked) { featuredEl.innerHTML = ""; otherBox.style.display = "none"; return; }

            var current = document.getElementById("h-name").value.trim().toLowerCase();
            function chip(p) {
                var active = p.name.toLowerCase() === current ? " name-chip--active" : "";
                return '<button type="button" class="name-chip' + active + '" data-name="' + esc(p.name) + '">' +
                    avatarHtml(p.avatar) + ' ' + esc(p.name) + '</button>';
            }

            var featured = state.profiles.filter(function(p) { return p.name === "ITMartin"; });
            var others = state.profiles.filter(function(p) { return p.name !== "ITMartin"; });

            featuredEl.innerHTML = featured.map(chip).join("");
            if (others.length > 0) {
                otherBox.style.display = "";
                otherEl.innerHTML = others.map(chip).join("");
            } else {
                otherBox.style.display = "none";
            }

            featuredEl.querySelectorAll(".name-chip").forEach(function(btn) {
                btn.onclick = function() { document.getElementById("h-name").value = btn.getAttribute("data-name"); window.onNameInput(); };
            });
            otherEl.querySelectorAll(".name-chip").forEach(function(btn) {
                btn.onclick = function() { document.getElementById("h-name").value = btn.getAttribute("data-name"); window.onNameInput(); };
            });
        }

        window.setMode = function(ranked) {
            state.isRanked = ranked;
            document.getElementById("h-mode-ranked").classList.toggle("mode-toggle-btn--active", ranked);
            document.getElementById("h-mode-training").classList.toggle("mode-toggle-btn--active", !ranked);
            document.getElementById("h-mode-hint").textContent = ranked
                ? "Vælg dit rigtige navn - tæller med i rangliste og statistik."
                : "Vælg navn og farve som normalt - resultatet tæller bare ikke med i rangliste eller statistik.";
            document.getElementById("h-name-section").style.display = "";
            var input = document.getElementById("h-name");
            input.style.display = "";
            input.value = ranked
                ? (localStorage.getItem("profile_name") || "")
                : generateTrainingName();
            window.onNameInput();
        };

        apiGet("/api/profiles").then(function(list) {
            state.profiles = list;
            renderNamePicker();
            window.onNameInput();
        }).catch(function() {});

        window.onJoinCodeInput = function() {
            document.getElementById("h-btn-join").disabled = !document.getElementById("h-join-code").value.trim();
        };

        window.showStep = function(n) {
            [0, 1, 2].forEach(function(i) {
                document.getElementById("step-" + i).style.display = i === n ? "" : "none";
            });
        };

        window.goToRuleset = function() {
            var name = document.getElementById("h-name").value.trim();
            if (!name) return;
            // Training's generated name is only for this one game - never
            // overwrite the real saved identity with it. The active name for
            // whichever game gets created/joined next travels via
            // sessionStorage (see handoff below), not the persisted default.
            if (state.isRanked) {
                localStorage.setItem("profile_name", name);
                localStorage.setItem("profile_avatar", state.avatar);
                localStorage.setItem("profile_color", state.color);
            }
            sessionStorage.setItem("active_name", name);
            sessionStorage.setItem("active_avatar", state.avatar);
            sessionStorage.setItem("active_color", state.color);
            sessionStorage.setItem("active_ranked", state.isRanked ? "1" : "0");
            // Whichever applies: unlocking an existing PIN-protected name, or
            // setting a fresh PIN while creating a brand-new one.
            var pinField = document.getElementById("h-pin-row").style.display !== "none"
                ? document.getElementById("h-pin-input")
                : document.getElementById("h-new-pin");
            sessionStorage.setItem("active_pin", (pinField && pinField.value.trim()) || "");

            apiGet("/api/rulesets").then(function(list) {
                state.rulesets = list;
                renderRulesetList();
                if (list.length > 0) selectRuleset(list[0].id);
                window.showStep(1);
            }).catch(showError);
        };

        function renderRulesetList() {
            var el = document.getElementById("h-ruleset-list");
            el.innerHTML = state.rulesets.map(function(r) {
                var active = r.id === state.selectedRulesetId ? " ruleset-card--active" : "";
                return '<button class="ruleset-card' + active + '" data-id="' + r.id + '">' +
                    '<div class="ruleset-name">' + esc(r.name) + (r.isBuiltIn ? "" : " 🛠️") + '</div>' +
                    '<div class="ruleset-desc">' + esc(r.description) + '</div>' +
                    '<div class="ruleset-meta">' + r.minPlayers + '-' + r.maxPlayers + ' spillere · start: ' + r.defaultStartingPoints + '</div>' +
                    '</button>';
            }).join("");
            el.querySelectorAll(".ruleset-card").forEach(function(btn) {
                btn.onclick = function() { selectRuleset(btn.getAttribute("data-id")); };
            });
        }

        function selectRuleset(id) {
            state.selectedRulesetId = id;
            state.selectedRuleset = state.rulesets.find(function(r) { return r.id === id; }) || null;
            if (state.selectedRuleset) state.startingPoints = state.selectedRuleset.defaultStartingPoints;
            renderRulesetList();
            document.getElementById("h-btn-points").disabled = !state.selectedRuleset;
        }

        window.toggleCustomForm = function() {
            var f = document.getElementById("h-custom-form");
            f.style.display = f.style.display === "none" ? "" : "none";
        };

        window.onCustomTeamToggle = function() {
            document.getElementById("c-team-fields").style.display = document.getElementById("c-team").checked ? "" : "none";
        };

        window.createCustomRuleset = function() {
            var name = document.getElementById("c-name").value.trim();
            if (!name) return;
            var isTeam = document.getElementById("c-team").checked;
            apiPost("/api/rulesets", {
                name: name,
                description: document.getElementById("c-desc").value.trim(),
                minPlayers: parseInt(document.getElementById("c-min").value, 10) || 1,
                maxPlayers: parseInt(document.getElementById("c-max").value, 10) || 6,
                isTeamMode: isTeam,
                playersPerTeam: isTeam ? (parseInt(document.getElementById("c-per-team").value, 10) || 2) : 0,
                sharedTeamPool: isTeam && document.getElementById("c-shared").checked,
                startingPoints: parseInt(document.getElementById("c-points").value, 10) || 50,
                createdByName: document.getElementById("h-name").value.trim()
            }).then(function(created) {
                return apiGet("/api/rulesets").then(function(list) {
                    state.rulesets = list;
                    renderRulesetList();
                    selectRuleset(created.id);
                    document.getElementById("h-custom-form").style.display = "none";
                });
            }).catch(showError);
        };

        window.goToPoints = function() {
            if (!state.selectedRuleset) return;
            state.startingPoints = Math.min(100, Math.max(50, state.startingPoints || 50));
            document.getElementById("h-points-slider").value = state.startingPoints;
            document.getElementById("h-points-value").textContent = state.startingPoints;
            window.showStep(2);
        };

        window.onPointsSliderInput = function() {
            state.startingPoints = parseInt(document.getElementById("h-points-slider").value, 10);
            document.getElementById("h-points-value").textContent = state.startingPoints;
        };

        window.createSession = function() {
            if (!state.selectedRuleset) { window.showStep(1); return; }
            document.getElementById("h-btn-create").disabled = true;
            document.getElementById("h-btn-create").textContent = "Opretter…";
            apiPost("/api/sessions", { rulesetId: state.selectedRulesetId, startingPoints: state.startingPoints, isRanked: state.isRanked })
                .then(function(session) { location.href = "/game/" + session.code; })
                .catch(function(err) {
                    document.getElementById("h-btn-create").disabled = false;
                    document.getElementById("h-btn-create").textContent = "🚀 Start nyt spil";
                    showError("Kunne ikke oprette: " + err.message);
                });
        };

        window.joinSession = function() {
            var code = document.getElementById("h-join-code").value.trim().toUpperCase();
            if (!code) return;
            apiGet("/api/sessions/" + encodeURIComponent(code)).then(function() {
                var name = document.getElementById("h-name").value.trim();
                if (name) {
                    if (state.isRanked) {
                        localStorage.setItem("profile_name", name);
                        localStorage.setItem("profile_avatar", state.avatar);
                        localStorage.setItem("profile_color", state.color);
                    }
                    sessionStorage.setItem("active_name", name);
                    sessionStorage.setItem("active_avatar", state.avatar);
                    sessionStorage.setItem("active_color", state.color);
                    sessionStorage.setItem("active_ranked", state.isRanked ? "1" : "0");
                    var pinField = document.getElementById("h-pin-row").style.display !== "none"
                        ? document.getElementById("h-pin-input")
                        : document.getElementById("h-new-pin");
                    sessionStorage.setItem("active_pin", (pinField && pinField.value.trim()) || "");
                }
                location.href = "/game/" + code;
            }).catch(function() { showError("Spil ikke fundet. Tjek koden."); });
        };

        function showError(msg) {
            var el = document.getElementById("h-error");
            el.textContent = typeof msg === "string" ? msg : "Der skete en fejl.";
            el.style.display = "";
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // GAME (/game/{code})
    // ═══════════════════════════════════════════════════════════════════

    function initGame(code) {
        var token = ensureLocalId("token_" + code);
        var deviceToken = ensureLocalId("device_id");
        var pulseTimers = {};

        // One-shot handoff from Home for the game about to be joined/created -
        // takes priority over the persisted default so a Training session's
        // generated name never gets confused with the real saved identity.
        // Consumed (not just read) so a later direct-URL visit to /game/{code}
        // falls back to the persisted profile instead of replaying a stale name.
        var handoffName = sessionStorage.getItem("active_name");
        var isRanked = sessionStorage.getItem("active_ranked") !== "0";
        var profileName = handoffName || localStorage.getItem("profile_name") || "";
        var joinAvatar = handoffName ? sessionStorage.getItem("active_avatar") : null;
        var joinColorOverride = handoffName ? sessionStorage.getItem("active_color") : null;
        var handoffPin = sessionStorage.getItem("active_pin") || "";
        sessionStorage.removeItem("active_name");
        sessionStorage.removeItem("active_avatar");
        sessionStorage.removeItem("active_color");
        sessionStorage.removeItem("active_ranked");
        sessionStorage.removeItem("active_pin");

        var state = {
            session: null,
            me: null,
            profileId: null,
            joinAvatar: joinAvatar || initialsFromName(profileName),
            joinColor: joinColorOverride || localStorage.getItem("profile_color") || COLORS[0],
            pollTimer: null,
            winnerShown: false,
            shotCombo: [],
            shootTargetId: null,
            shotSign: -1  // -1 = damage an opponent (default), +1 = heal/gain for myself
        };

        var profileAvatar = state.joinAvatar;

        // Training names are throwaway - never create/reuse a real
        // PlayerProfile for one, so it can't be found in the name picker or
        // counted on the leaderboard later. A PIN-protected name that fails
        // to unlock stops the join outright, rather than quietly letting
        // someone play under that name with no real claim to it.
        (isRanked
            ? apiPost("/api/profile", { deviceToken: deviceToken, name: profileName, avatar: profileAvatar, pin: handoffPin })
                .then(function(profile) { state.profileId = profile.id; })
                .catch(function(err) {
                    alert(err.message || "Kunne ikke bekræfte profilen.");
                    location.href = "/";
                    return Promise.reject(err);
                })
            : Promise.resolve()
        ).then(loadSession).catch(function() {});

        function loadSession() {
            apiGet("/api/sessions/" + encodeURIComponent(code)).then(function(session) {
                document.getElementById("g-loading").style.display = "none";
                state.session = session;
                state.me = session.players.find(function(p) { return p.token === token; }) || null;

                if (!state.me) {
                    if (profileName) {
                        doJoin(profileName, state.joinAvatar, state.joinColor, function() {
                            showInviteThenGame();
                        });
                    } else {
                        showNeedsName(session);
                    }
                    return;
                }

                // A session that hasn't been explicitly started yet always shows
                // the invite/waiting screen, even on reload - otherwise a host
                // who refreshes mid-setup lands straight on live score buttons
                // before anyone actually pressed "Start spillet".
                if (!session.hasStarted) {
                    showInviteThenGame();
                } else {
                    showMainGame();
                }
            }).catch(function(err) {
                document.getElementById("g-loading").textContent = "Spil ikke fundet.";
            });
        }

        function showNeedsName(session) {
            document.getElementById("n-ruleset-name").textContent = session.rulesetName;
            function paintAvatar() {
                state.joinAvatar = paintAvatarPreview("n-avatar-preview", document.getElementById("n-name").value, state.joinColor);
            }
            var takenColors = session.players.map(function(p) { return p.color; });
            function paintColors() {
                renderColorGrid("n-color-grid", COLORS, state.joinColor, takenColors, function(c) { state.joinColor = c; paintColors(); paintAvatar(); });
            }
            paintAvatar();
            paintColors();

            document.getElementById("g-needs-name").style.display = "";
            window.onJoinNameInput = function() {
                document.getElementById("n-btn-join").disabled = !document.getElementById("n-name").value.trim();
                paintAvatar();
            };
            window.joinAsPlayer = function() {
                var name = document.getElementById("n-name").value.trim();
                if (!name) return;
                doJoin(name, state.joinAvatar, state.joinColor, function() {
                    document.getElementById("g-needs-name").style.display = "none";
                    showInviteThenGame();
                });
            };
        }

        function doJoin(name, avatar, color, onDone) {
            if (isRanked) {
                localStorage.setItem("profile_name", name);
                localStorage.setItem("profile_avatar", avatar);
                localStorage.setItem("profile_color", color);
            }
            apiPost("/api/sessions/" + encodeURIComponent(code) + "/join", {
                token: token, name: name, avatar: avatar, color: color, profileId: state.profileId
            }).then(function() {
                return apiGet("/api/sessions/" + encodeURIComponent(code));
            }).then(function(session) {
                state.session = session;
                state.me = session.players.find(function(p) { return p.token === token; }) || null;
                onDone();
            }).catch(function(err) { alert(err.message || "Kunne ikke deltage"); });
        }

        function showInviteThenGame() {
            var session = state.session;
            document.getElementById("i-ruleset-name").textContent = session.rulesetName;
            document.getElementById("i-count").textContent = session.players.length + "/6";
            document.getElementById("i-players-list").innerHTML = session.players.map(function(p) {
                return '<div class="invite-player-row"><span class="color-dot" style="background:' + p.color + '"></span>' + avatarHtml(p.avatar) + ' ' + esc(p.name) + '</div>';
            }).join("");
            document.getElementById("g-invite").style.display = "";

            window.copyInviteLink = function() {
                navigator.clipboard && navigator.clipboard.writeText(location.href).catch(function() {});
            };
            window.hideInvite = function() {
                var btn = document.getElementById("g-invite-start-btn");
                if (btn) { btn.disabled = true; btn.textContent = "Starter…"; }
                apiPost("/api/sessions/" + encodeURIComponent(code) + "/start").then(function() {
                    document.getElementById("g-invite").style.display = "none";
                    showMainGame();
                }).catch(function(err) {
                    if (btn) { btn.disabled = false; btn.textContent = "▶️ Start spillet"; }
                    alert(err.message || "Kunne ikke starte spillet");
                });
            };
        }

        function otherPlayers() {
            return state.session.players
                .filter(function(p) { return !state.me || p.id !== state.me.id; })
                .sort(function(a, b) { return a.sortOrder - b.sortOrder; });
        }

        function showMainGame() {
            document.getElementById("g-main").style.display = "";
            starrealms.requestWakeLock();

            window.toggleRules = function() {
                var panel = document.getElementById("g-rules-panel");
                var show = panel.style.display === "none";
                panel.style.display = show ? "" : "none";
                document.getElementById("g-rules-toggle").textContent = (show ? "📜 Skjul regler" : "📜 Se regler for dette spil");
            };

            window.addToCombo = function(n) {
                state.shotCombo.push(n);
                starrealms.playClick();
                starrealms.vibrate(8);
                renderHero();
            };

            window.clearCombo = function() {
                if (state.shotCombo.length === 0) return;
                state.shotCombo = [];
                renderHero();
            };

            window.selectShootTarget = function(id) {
                state.shootTargetId = id;
                renderHero();
            };

            window.setShootMode = function(sign) {
                state.shotSign = sign;
                renderHero();
            };

            function applyShot(player, delta) {
                var before = player.points;
                player.points = Math.min(state.session.maxPoints, player.points + delta);
                var actualDelta = player.points - before;
                renderHero();
                renderOpponents({});
                if (actualDelta !== 0) {
                    var up = actualDelta > 0;
                    starrealms.vibrateForAmount(actualDelta);
                    starrealms.playImpact(actualDelta, up);
                    if (player.points <= 0) starrealms.playEliminate();
                    var isMe = state.me && player.id === state.me.id;
                    var targetEl = isMe
                        ? document.getElementById("g-hero-points")
                        : document.querySelector('.opp-card[data-opp-id="' + player.id + '"] .opp-points');
                    starrealms.explodeAt(targetEl, up, actualDelta);
                }
                apiPost("/api/sessions/" + encodeURIComponent(code) + "/adjust", { playerId: player.id, delta: delta })
                    .then(refreshState).catch(function(err) { alert(err.message || "Kunne ikke opdatere"); refreshState(); });
            }

            window.fireShoot = function() {
                var magnitude = state.shotCombo.reduce(function(a, b) { return a + b; }, 0);
                if (magnitude <= 0) return;
                var total = magnitude * state.shotSign;

                if (state.shotSign < 0) {
                    // Damage - always hits a chosen opponent, never the shooter.
                    var others = otherPlayers();
                    var targetId = others.length === 1 ? others[0].id : state.shootTargetId;
                    var target = others.find(function(p) { return p.id === targetId; });
                    if (!target) return; // 2+ opponents and none picked yet - button stays disabled for this case
                    state.shotCombo = [];
                    state.shootTargetId = null;
                    applyShot(target, total);
                } else {
                    // Heal/gain - applies to the shooter's own points.
                    if (!state.me) return;
                    var me = state.session.players.find(function(p) { return p.id === state.me.id; });
                    if (!me) return;
                    state.shotCombo = [];
                    applyShot(me, total);
                }

                showTurnEndedToast();
            };

            var turnToastTimer = null;
            function showTurnEndedToast() {
                // Firing a shot is the natural end of a turn - there's no
                // app-enforced turn order (that was deliberately removed, players
                // manage that themselves), but this gives visible confirmation
                // "that's your shot recorded, your turn's done" without gating
                // anything for other players.
                var el = document.getElementById("g-turn-toast");
                if (!el) return;
                el.textContent = "✅ Tur afsluttet";
                el.classList.add("turn-toast--show");
                clearTimeout(turnToastTimer);
                turnToastTimer = setTimeout(function() {
                    el.classList.remove("turn-toast--show");
                }, 1800);
            }

            window.resetGame = function() {
                if (!confirm("Nulstil spillet for alle spillere?")) return;
                apiPost("/api/sessions/" + encodeURIComponent(code) + "/reset").then(function() {
                    state.winnerShown = false;
                    document.getElementById("g-winner").style.display = "none";
                    refreshState();
                }).catch(function(err) { alert(err.message || "Kunne ikke nulstille"); });
            };

            window.hideWinner = function() {
                document.getElementById("g-winner").style.display = "none";
            };

            refreshState();
            state.pollTimer = setInterval(refreshState, 3000);
        }

        function refreshState() {
            apiGet("/api/sessions/" + encodeURIComponent(code)).then(function(session) {
                var prevPoints = {};
                if (state.session) state.session.players.forEach(function(p) { prevPoints[p.id] = p.points; });

                state.session = session;
                state.me = session.players.find(function(p) { return p.token === token; }) || state.me;

                renderHero(prevPoints);
                renderOpponents(prevPoints);
                renderRules();
                checkWinner();
            }).catch(function() { /* transient network hiccup - next poll will retry */ });
        }

        function renderHero(prevPoints) {
            if (!state.me) return;
            var s = state.session;
            var me = s.players.find(function(p) { return p.id === state.me.id; });
            if (!me) return;
            var eliminated = me.points <= 0;
            var el = document.getElementById("g-hero-card");
            el.className = "hero-card";
            el.style.borderColor = me.color;
            var others = otherPlayers();
            var isDamage = state.shotSign < 0;
            var comboTotal = state.shotCombo.reduce(function(a, b) { return a + b; }, 0);
            var comboText = state.shotCombo.length
                ? state.shotCombo.join(" + ") + " = " + (isDamage ? "−" : "+") + comboTotal
                : "Byg dit " + (isDamage ? "skud" : "helbred") + "…";
            var comboEmpty = state.shotCombo.length === 0;

            var needsTargetPick = isDamage && others.length > 1;
            var resolvedTargetId = !isDamage ? null : (others.length === 1 ? others[0].id : state.shootTargetId);
            var resolvedTarget = others.find(function(p) { return p.id === resolvedTargetId; });
            var canFire = !comboEmpty && (!isDamage || !!resolvedTarget);

            var shootLabel = isDamage
                ? "🔫 SKYD" + (resolvedTarget ? " " + esc(resolvedTarget.name) : "") + (comboEmpty ? "" : " −" + comboTotal)
                : "💚 HELBRED" + (comboEmpty ? "" : " +" + comboTotal);

            var heroProfileLink = me.profileId ? '/stats?profileId=' + encodeURIComponent(me.profileId) : null;

            el.innerHTML =
                '<div class="hero-top">' +
                    (heroProfileLink
                        ? '<a class="hero-avatar" href="' + heroProfileLink + '" style="background:' + me.color + '">' + (eliminated ? "💀" : avatarHtml(me.avatar, "avatar-img avatar-img--hero")) + '</a>'
                        : '<div class="hero-avatar" style="background:' + me.color + '">' + (eliminated ? "💀" : avatarHtml(me.avatar, "avatar-img avatar-img--hero")) + '</div>') +
                    '<div class="hero-name">' + esc(me.name) + ' <span class="hero-you">(dig)</span></div>' +
                '</div>' +
                '<div class="hero-points" id="g-hero-points" style="color:' + me.color + '">' + me.points + '</div>' +
                '<div class="shoot-combo">' +
                    '<div class="shoot-mode-row">' +
                        '<button class="mode-btn mode-btn--dmg' + (isDamage ? " mode-btn--active" : "") + '" onclick="setShootMode(-1)">⚔️ Skade</button>' +
                        '<button class="mode-btn mode-btn--heal' + (!isDamage ? " mode-btn--active" : "") + '" onclick="setShootMode(1)">💚 Helbred</button>' +
                    '</div>' +
                    '<div class="shoot-combo-display">' + esc(comboText) + '</div>' +
                    '<div class="shoot-chip-row">' +
                        [1, 2, 3, 4, 5, 10, 15, 20, 25, 50].map(function(n) {
                            return '<button class="chip-btn" onclick="addToCombo(' + n + ')">' + n + '</button>';
                        }).join("") +
                    '</div>' +
                    (needsTargetPick ?
                        '<div class="shoot-target-row">' +
                            '<div class="shoot-target-label">Skyd på:</div>' +
                            '<div class="shoot-target-chips">' +
                                others.map(function(p) {
                                    var active = p.id === resolvedTargetId ? " target-chip--active" : "";
                                    return '<button class="target-chip' + active + '" style="border-color:' + p.color + '" onclick="selectShootTarget(\'' + p.id + '\')">' +
                                        '<span class="target-chip-avatar" style="background:' + p.color + '">' + avatarHtml(p.avatar) + '</span>' + esc(p.name) +
                                        '</button>';
                                }).join("") +
                            '</div>' +
                        '</div>'
                        : "") +
                    '<div class="shoot-actions">' +
                        '<button class="btn-ghost btn-combo-clear" onclick="clearCombo()"' + (comboEmpty ? " disabled" : "") + '>↺ Ryd</button>' +
                        '<button class="btn-shoot' + (isDamage ? "" : " btn-shoot--heal") + '" onclick="fireShoot()"' + (canFire ? "" : " disabled") + '>' + shootLabel + '</button>' +
                    '</div>' +
                '</div>';

            // Catches point changes NOT caused by my own tap (e.g. a teammate
            // moving a shared team pool) - my own taps already animate
            // instantly and optimistically in applyShot() above, and by the
            // time this runs that local mutation makes prevPoints === me.points
            // already, so this never double-fires for the common case.
            if (prevPoints && prevPoints[me.id] !== undefined && prevPoints[me.id] !== me.points) {
                var up = me.points > prevPoints[me.id];
                var delta = me.points - prevPoints[me.id];
                starrealms.vibrateForAmount(delta);
                starrealms.playImpact(delta, up);
                if (eliminated) starrealms.playEliminate();
                starrealms.explodeAt(document.getElementById("g-hero-points"), up, delta);
            }
        }

        function renderOpponents(prevPoints) {
            var s = state.session;
            var el = document.getElementById("g-opp-grid");
            var others = s.players.filter(function(p) { return !state.me || p.id !== state.me.id; }).sort(function(a, b) { return a.sortOrder - b.sortOrder; });

            el.innerHTML = others.map(function(p) {
                var dead = p.points <= 0;
                var pulseCls = "";
                if (prevPoints[p.id] !== undefined && prevPoints[p.id] !== p.points) {
                    pulseCls = p.points > prevPoints[p.id] ? " score-row--up" : " score-row--down";
                }
                var teamLabel = "";
                if (s.isTeamMode && p.team !== null && p.team !== undefined) {
                    var mine = state.me && s.players.find(function(x) { return x.id === state.me.id; });
                    teamLabel = '<span class="opp-team">Hold ' + (p.team + 1) + (mine && mine.team === p.team ? " (dit)" : "") + '</span>';
                }
                var oppProfileLink = p.profileId ? '/stats?profileId=' + encodeURIComponent(p.profileId) : null;
                var oppAvatarHtml = dead ? "💀" : avatarHtml(p.avatar, "avatar-img avatar-img--opp");
                return '<div class="opp-card' + pulseCls + (dead ? " opp-card--dead" : "") + '" data-opp-id="' + p.id + '" style="border-color:' + p.color + '">' +
                    (oppProfileLink
                        ? '<a class="opp-avatar" href="' + oppProfileLink + '" style="background:' + p.color + '" title="Se profil">' + oppAvatarHtml + '</a>'
                        : '<div class="opp-avatar" style="background:' + p.color + '">' + oppAvatarHtml + '</div>') +
                    '<div class="opp-info"><div class="opp-name">' + esc(p.name) + teamLabel + '</div>' +
                    '<div class="opp-color-label"><span class="color-dot" style="background:' + p.color + '"></span>farve</div></div>' +
                    '<div class="opp-points" style="color:' + p.color + '">' + p.points + '</div>' +
                    '</div>';
            }).join("");

            others.forEach(function(p) {
                if (prevPoints[p.id] !== undefined && prevPoints[p.id] !== p.points) {
                    var delta = p.points - prevPoints[p.id];
                    starrealms.vibrateForAmount(delta);
                    starrealms.playImpact(delta, delta > 0);
                    if (p.points <= 0) starrealms.playEliminate();
                }
            });
        }

        function renderRules() {
            document.getElementById("g-rules-title").textContent = state.session.rulesetName;
            document.getElementById("g-rules-desc").textContent = state.session.rulesetDescription;
        }

        function checkWinner() {
            var s = state.session;
            if (!s.isCompleted) { state.winnerShown = false; return; }
            if (state.winnerShown) return;

            var winnerName, winnerColor, winnerIds;
            if (s.isTeamMode) {
                var aliveTeams = {};
                s.players.forEach(function(p) { if (p.points > 0) aliveTeams[p.team] = true; });
                var teams = Object.keys(aliveTeams);
                if (teams.length !== 1) return;
                var teamNum = parseInt(teams[0], 10);
                var teammate = s.players.find(function(p) { return p.team === teamNum; });
                winnerName = "Hold " + (teamNum + 1);
                winnerColor = teammate ? teammate.color : "#fff";
                winnerIds = s.players.filter(function(p) { return p.team === teamNum; }).map(function(p) { return p.id; });
            } else {
                var alive = s.players.filter(function(p) { return p.points > 0; });
                if (alive.length !== 1) return;
                winnerName = alive[0].name;
                winnerColor = alive[0].color;
                winnerIds = [alive[0].id];
            }

            state.winnerShown = true;
            document.getElementById("g-winner-card").style.borderColor = winnerColor;
            var nameEl = document.getElementById("g-winner-name");
            nameEl.style.color = winnerColor;
            nameEl.textContent = winnerName + " vinder!";
            document.getElementById("g-winner").style.display = "";
            starrealms.playWinner();
            starrealms.confetti();
            renderResultImage(s, winnerIds);
        }

        function roundRect(ctx, x, y, w, h, r) {
            ctx.beginPath();
            ctx.moveTo(x + r, y);
            ctx.arcTo(x + w, y, x + w, y + h, r);
            ctx.arcTo(x + w, y + h, x, y + h, r);
            ctx.arcTo(x, y + h, x, y, r);
            ctx.arcTo(x, y, x + w, y, r);
            ctx.closePath();
        }

        // Renders a shareable "results card" - final standings, winner
        // highlighted - and drops it into the winner overlay as both a
        // preview and a downloadable PNG.
        function renderResultImage(s, winnerIds) {
            var winnerSet = {};
            winnerIds.forEach(function(id) { winnerSet[id] = true; });

            var w = 720, h = 200 + s.players.length * 130 + 80;
            var canvas = document.createElement("canvas");
            canvas.width = w;
            canvas.height = h;
            var ctx = canvas.getContext("2d");

            var grad = ctx.createLinearGradient(0, 0, 0, h);
            grad.addColorStop(0, "#1a1d27");
            grad.addColorStop(1, "#0f1117");
            ctx.fillStyle = grad;
            ctx.fillRect(0, 0, w, h);

            ctx.textAlign = "center";
            ctx.fillStyle = "#6366f1";
            ctx.font = "800 34px -apple-system, Segoe UI, sans-serif";
            ctx.fillText("🚀 Star Realms Score", w / 2, 70);
            ctx.fillStyle = "#8890a6";
            ctx.font = "16px -apple-system, Segoe UI, sans-serif";
            ctx.fillText(s.rulesetName + " · " + new Date().toLocaleDateString("da-DK"), w / 2, 98);

            var players = s.players.slice().sort(function(a, b) { return b.points - a.points; });
            var y = 140;
            players.forEach(function(p) {
                var isWinner = !!winnerSet[p.id];

                ctx.fillStyle = isWinner ? "rgba(241,196,15,0.14)" : "rgba(255,255,255,0.04)";
                roundRect(ctx, 40, y, w - 80, 108, 16);
                ctx.fill();
                if (isWinner) {
                    ctx.strokeStyle = "#f1c40f";
                    ctx.lineWidth = 3;
                    roundRect(ctx, 41.5, y + 1.5, w - 83, 105, 15);
                    ctx.stroke();
                }

                ctx.beginPath();
                ctx.arc(96, y + 54, 32, 0, Math.PI * 2);
                ctx.fillStyle = p.color;
                ctx.fill();
                ctx.fillStyle = "#fff";
                ctx.font = "700 22px -apple-system, Segoe UI, sans-serif";
                ctx.textAlign = "center";
                // Canvas can't render an <img> avatar as text - fall back to
                // initials for the exported share image (loading+clipping a
                // photo into a circle here isn't worth it for a nice-to-have export).
                ctx.fillText(isImageAvatar(p.avatar) ? initialsFromName(p.name) : (p.avatar || "?"), 96, y + 62);

                ctx.textAlign = "left";
                ctx.fillStyle = "#f0f0f0";
                ctx.font = "700 25px -apple-system, Segoe UI, sans-serif";
                ctx.fillText(p.name + (isWinner ? " 🏆" : ""), 148, y + 62);

                ctx.textAlign = "right";
                ctx.fillStyle = p.color;
                ctx.font = "800 32px -apple-system, Segoe UI, sans-serif";
                ctx.fillText(String(p.points), w - 64, y + 68);

                y += 128;
            });

            var dataUrl = canvas.toDataURL("image/png");
            var img = document.getElementById("g-winner-preview");
            if (img) { img.src = dataUrl; img.style.display = ""; }
            var link = document.getElementById("g-winner-download");
            if (link) {
                link.href = dataUrl;
                link.download = "star-realms-" + s.code.toLowerCase() + ".png";
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // STATS (/stats)
    // ═══════════════════════════════════════════════════════════════════

    function initStats() {
        var deviceToken = ensureLocalId("device_id");
        var urlProfileId = new URLSearchParams(location.search).get("profileId");
        var profile = null;
        var isOwnProfile = false;
        var activeMonths = null;
        var activeRuleset = "";
        var editState = { avatar: "", emojiOptions: ["🚀","🛸","👽","🤖","🎯","⚔️","🏆","🔥","⭐","💥","🌟","🛡️","🪐","☄️","💫","🎮","🦾","🧨","⚡","👾","🐙"], profiles: [] };

        window.toggleEditProfile = function() {
            var panel = document.getElementById("s-edit-panel");
            var opening = panel.style.display === "none";
            panel.style.display = opening ? "" : "none";
            if (opening) {
                document.getElementById("s-edit-name").value = profile ? profile.name : "";
                editState.avatar = profile ? profile.avatar : "";
                apiGet("/api/profiles").then(function(list) { editState.profiles = list; renderEmojiGridStats(); }).catch(function() {});
                renderEmojiGridStats();
            }
        };

        function renderEmojiGridStats() {
            var taken = editState.profiles.map(function(p) { return p.avatar; }).filter(function(a) { return a && !isImageAvatar(a) && a !== profile.avatar; });
            var el = document.getElementById("s-emoji-grid");
            var cells = editState.emojiOptions.map(function(e) {
                var isTaken = taken.indexOf(e) !== -1;
                var active = e === editState.avatar ? " emoji-btn--active" : "";
                return '<button type="button" class="emoji-btn' + (isTaken ? " color-btn--taken" : "") + active + '" data-emoji="' + e + '"' + (isTaken ? " disabled" : "") + '>' + e + '</button>';
            });
            cells.unshift('<button type="button" class="emoji-btn emoji-btn--none' + (editState.avatar === "" ? " emoji-btn--active" : "") + '" data-emoji="">Intet</button>');
            el.innerHTML = cells.join("");
            el.querySelectorAll(".emoji-btn:not([disabled])").forEach(function(btn) {
                btn.onclick = function() {
                    editState.avatar = btn.getAttribute("data-emoji");
                    document.getElementById("s-ai-picture-preview").style.display = "none";
                    renderEmojiGridStats();
                };
            });
        }

        window.requestMoreEmojiStats = function() {
            var btn = document.getElementById("s-emoji-more");
            btn.disabled = true; btn.textContent = "Genererer…";
            apiPost("/api/emoji-suggestions", { exclude: editState.emojiOptions }).then(function(more) {
                if (more && more.length) editState.emojiOptions = editState.emojiOptions.concat(more);
                renderEmojiGridStats();
            }).catch(function() {}).then(function() { btn.disabled = false; btn.textContent = "✨ Flere ikoner (AI)"; });
        };

        window.generateProfilePictureStats = function() {
            var prompt = document.getElementById("s-ai-prompt").value.trim() || document.getElementById("s-edit-name").value.trim();
            if (!prompt) return;
            var btn = document.getElementById("s-ai-generate");
            btn.disabled = true; btn.textContent = "Genererer…";
            apiPost("/api/profile-picture", { prompt: prompt }).then(function(res) {
                editState.avatar = res.url;
                var prev = document.getElementById("s-ai-picture-preview");
                prev.style.display = "";
                prev.innerHTML = '<img src="' + esc(res.url) + '"><span>Valgt som ikon</span>';
                renderEmojiGridStats();
            }).catch(function() { alert("Kunne ikke generere billede - prøv igen."); })
              .then(function() { btn.disabled = false; btn.textContent = "🪄 Generér"; });
        };

        window.saveProfileEdit = function() {
            var name = document.getElementById("s-edit-name").value.trim();
            if (!name) return;
            var wasNew = !profile;
            apiPost("/api/profile", { deviceToken: deviceToken, name: name, avatar: editState.avatar }).then(function(updated) {
                profile = updated;
                isOwnProfile = true;
                localStorage.setItem("profile_name", updated.name);
                localStorage.setItem("profile_avatar", updated.avatar);
                document.getElementById("s-hero").innerHTML = heroBadgeHtml(profile.avatar, profile.name);
                document.getElementById("s-who").textContent = "Dine opgør mod dine modstandere";
                document.getElementById("s-edit-panel").style.display = "none";
                if (wasNew) {
                    document.getElementById("s-empty").style.display = "none";
                    document.getElementById("s-content").style.display = "";
                    document.getElementById("s-edit-toggle").style.display = "";
                    paintFilters();
                    loadRows();
                    loadMyTeams();
                }
            }).catch(function(err) { alert((err && err.message) ? err.message.replace(/^"|"$/g, "") : "Kunne ikke gemme"); });
        };

        function paintFilters() {
            [["s-filter-all", null], ["s-filter-1", 1], ["s-filter-6", 6], ["s-filter-12", 12]].forEach(function(pair) {
                document.getElementById(pair[0]).disabled = pair[1] === activeMonths;
            });
        }

        window.filterStats = function(months) {
            activeMonths = months;
            paintFilters();
            loadRows();
        };

        window.filterStatsRuleset = function(ruleset) {
            activeRuleset = ruleset || "";
            loadRows();
        };

        apiGet("/api/rulesets").then(function(list) {
            var sel = document.getElementById("s-ruleset-filter");
            list.forEach(function(r) {
                var opt = document.createElement("option");
                opt.value = r.name;
                opt.textContent = r.name;
                sel.appendChild(opt);
            });
        }).catch(function() {});

        function loadRows() {
            var url = "/api/stats?profileId=" + encodeURIComponent(profile.id) +
                (activeMonths ? "&sinceMonths=" + activeMonths : "") +
                (activeRuleset ? "&ruleset=" + encodeURIComponent(activeRuleset) : "");
            apiGet(url).then(function(rows) {
                var el = document.getElementById("s-rows");
                if (rows.length === 0) {
                    el.innerHTML = '<p class="home-sub">Ingen afsluttede spil i denne periode.</p>';
                    return;
                }
                el.innerHTML = rows.map(function(r) {
                    var total = r.wins + r.losses + r.draws;
                    var last = new Date(r.lastPlayed).toLocaleDateString("da-DK", { day: "numeric", month: "short", year: "numeric" });
                    return '<div class="stats-row"><div><div class="stats-name">' + esc(r.opponentName) + '</div>' +
                        '<div class="stats-record">' + total + ' spil · sidst spillet ' + last + '</div></div>' +
                        '<div class="stats-wl"><span class="stats-wins">' + r.wins + ' V</span>' +
                        '<span class="stats-losses">' + r.losses + ' T</span>' +
                        (r.draws > 0 ? '<span class="stats-draws">' + r.draws + ' U</span>' : '') + '</div></div>';
                }).join("");
            }).catch(function() {});
        }

        var lookup = urlProfileId
            ? apiGet("/api/profile/" + encodeURIComponent(urlProfileId))
            : (deviceToken ? apiGet("/api/profile?deviceToken=" + encodeURIComponent(deviceToken)) : Promise.reject());

        lookup.then(function(p) {
            profile = p;
            isOwnProfile = !urlProfileId || (deviceToken && p.deviceToken === deviceToken);
            document.getElementById("s-loading").style.display = "none";
            document.getElementById("s-content").style.display = "";
            document.getElementById("s-hero").innerHTML = heroBadgeHtml(p.avatar, p.name);
            document.getElementById("s-who").textContent = isOwnProfile ? "Dine opgør mod dine modstandere" : "Opgør mod deres modstandere";
            document.getElementById("s-edit-toggle").style.display = isOwnProfile ? "" : "none";
            paintFilters();
            loadRows();
            if (isOwnProfile) loadMyTeams();
        }).catch(function() {
            document.getElementById("s-loading").style.display = "none";
            document.getElementById("s-empty").style.display = "";
        });

        // Any team you're a member of - custom name (e.g. "The Fighters") is
        // shared by every member and editable by any of them, since this is
        // a family score tracker rather than something needing real
        // ownership/permissions.
        function loadMyTeams() {
            apiGet("/api/teams/mine?deviceToken=" + encodeURIComponent(deviceToken)).then(function(teams) {
                if (!teams.length) return;
                document.getElementById("s-teams-section").style.display = "";
                document.getElementById("s-teams-rows").innerHTML = teams.map(function(t) {
                    return '<div class="stats-row"><div style="flex:1">' +
                        '<input class="home-input" data-team-id="' + t.id + '" value="' + esc(t.name || "") + '" placeholder="' + esc(t.memberNames) + '" maxlength="30">' +
                        '<div class="stats-record">' + esc(t.memberNames) + '</div></div>' +
                        '<button type="button" class="btn-ghost" data-save-team="' + t.id + '">Gem</button></div>';
                }).join("");
                document.getElementById("s-teams-rows").querySelectorAll("[data-save-team]").forEach(function(btn) {
                    btn.onclick = function() {
                        var id = btn.getAttribute("data-save-team");
                        var input = document.querySelector('[data-team-id="' + id + '"]');
                        apiPost("/api/teams/" + id + "/name", { deviceToken: deviceToken, name: input.value.trim() })
                            .catch(function() { alert("Kunne ikke gemme holdnavn"); });
                    };
                });
            }).catch(function() {});
        }
    }

    function initLeaderboard() {
        var activeMonths = null;

        function paintFilters() {
            [["l-filter-all", null], ["l-filter-1", 1], ["l-filter-6", 6], ["l-filter-12", 12]].forEach(function(pair) {
                document.getElementById(pair[0]).disabled = pair[1] === activeMonths;
            });
        }

        window.filterLeaderboard = function(months) {
            activeMonths = months;
            paintFilters();
            loadBoards();
        };

        function renderBoard(rulesetName, rows, isTeamMode) {
            var medals = ["🥇", "🥈", "🥉"];
            var body = rows.length === 0
                ? '<div class="home-sub">Ingen ranked spil endnu.</div>'
                : rows.map(function(r, i) {
                    var rank = medals[i] || ("#" + (i + 1));
                    // Teams have no single avatar/profile page to link to - a
                    // team is a pairing, not a person - so the row is static.
                    var nameLine = isTeamMode
                        ? '<div class="stats-name">' + rank + ' ' + esc(r.name) + '</div><div class="stats-record">' + esc(r.memberNames) + '</div>'
                        : '<div class="stats-name">' + rank + ' ' + avatarHtml(r.avatar) + ' ' + esc(r.name) + '</div>' +
                          '<div class="stats-record">' + r.gamesPlayed + ' spil · ' + r.winRate + '% vundet</div>';
                    var row = '<div>' + nameLine + '</div>' +
                        '<div class="stats-wl"><span class="stats-wins">' + r.wins + ' V</span>' +
                        '<span class="stats-losses">' + r.losses + ' T</span>' +
                        (r.draws > 0 ? '<span class="stats-draws">' + r.draws + ' U</span>' : '') + '</div>';
                    return isTeamMode
                        ? '<div class="stats-row">' + row + '</div>'
                        : '<a class="stats-row" href="/stats?profileId=' + encodeURIComponent(r.profileId) + '">' + row + '</a>';
                }).join("");
            return '<div class="leaderboard-section"><div class="home-section-inner-label">' + esc(rulesetName) + (isTeamMode ? " · hold" : "") + '</div>' + body + '</div>';
        }

        function loadBoards() {
            document.getElementById("l-loading").style.display = "";
            var boardsEl = document.getElementById("l-boards");
            var since = activeMonths ? "sinceMonths=" + activeMonths : "";
            apiGet("/api/rulesets").then(function(rulesets) {
                return Promise.all(rulesets.map(function(r) {
                    var url = (r.isTeamMode ? "/api/leaderboard/teams?" : "/api/leaderboard?") +
                        since + (since ? "&" : "") + "ruleset=" + encodeURIComponent(r.name);
                    return apiGet(url).then(function(rows) { return { name: r.name, rows: rows, isTeamMode: r.isTeamMode }; });
                })).then(function(boards) {
                    document.getElementById("l-loading").style.display = "none";
                    boardsEl.innerHTML = boards.map(function(b) { return renderBoard(b.name, b.rows, b.isTeamMode); }).join("");
                });
            }).catch(function() {
                document.getElementById("l-loading").style.display = "none";
            });
        }

        paintFilters();
        loadBoards();
    }

    // ── Dispatch on load ─────────────────────────────────────────────────
    document.addEventListener("DOMContentLoaded", function() {
        var pageData = document.getElementById("page-data");
        if (!pageData) return;
        var page = pageData.getAttribute("data-page");
        if (page === "home") initHome();
        else if (page === "game") initGame(pageData.getAttribute("data-code"));
        else if (page === "stats") initStats();
        else if (page === "leaderboard") initLeaderboard();
    });
})();
