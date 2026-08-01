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

    // Sound + vibration scale with the SIZE of a point change, not just its
    // direction - a 1-point nudge is a joke, a 50-point swing is an event.
    // Loss (positive=false) uses a falling sawtooth "hit"; gain uses a
    // rising triangle "heal" - both share the same magnitude tiers so a
    // -20 and a +20 feel equally significant, just tonally opposite.
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

        for (var i = 0; i < tier.layers; i++) {
            (function(i) {
                setTimeout(function() {
                    if (positive) tone(380 + i * 60, 950 + i * 120, tier.dur, "triangle", tier.gain);
                    else tone(720 - i * 50, 90 - i * 8, tier.dur, "sawtooth", tier.gain);
                }, i * 30);
            })(i);
        }
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

        var state = {
            avatar: "?",
            color: localStorage.getItem("profile_color") || COLORS[0],
            rulesets: [],
            selectedRulesetId: null,
            selectedRuleset: null,
            startingPoints: 50
        };

        document.getElementById("h-name").value = localStorage.getItem("profile_name") || "";

        function paintAvatar() { state.avatar = paintAvatarPreview("h-avatar-preview", document.getElementById("h-name").value, state.color); }
        function paintColors() { renderColorGrid("h-color-grid", COLORS, state.color, [], function(c) { state.color = c; paintColors(); paintAvatar(); }); }
        paintAvatar();
        paintColors();

        window.onNameInput = function() {
            document.getElementById("h-btn-ruleset").disabled = !document.getElementById("h-name").value.trim();
            paintAvatar();
        };
        window.onNameInput();

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
            localStorage.setItem("profile_name", name);
            localStorage.setItem("profile_avatar", state.avatar);
            localStorage.setItem("profile_color", state.color);

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
            apiPost("/api/sessions", { rulesetId: state.selectedRulesetId, startingPoints: state.startingPoints })
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
                    localStorage.setItem("profile_name", name);
                    localStorage.setItem("profile_avatar", state.avatar);
                    localStorage.setItem("profile_color", state.color);
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

        var profileName = localStorage.getItem("profile_name") || "";

        var state = {
            session: null,
            me: null,
            profileId: null,
            joinAvatar: initialsFromName(profileName),
            joinColor: localStorage.getItem("profile_color") || COLORS[0],
            pollTimer: null,
            winnerShown: false,
            shotCombo: [],
            shootTargetId: null,
            shotSign: -1  // -1 = damage an opponent (default), +1 = heal/gain for myself
        };

        var profileAvatar = state.joinAvatar;

        apiPost("/api/profile", { deviceToken: deviceToken, name: profileName, avatar: profileAvatar })
            .then(function(profile) { state.profileId = profile.id; })
            .catch(function() {})
            .then(loadSession);

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
            localStorage.setItem("profile_name", name);
            localStorage.setItem("profile_avatar", avatar);
            localStorage.setItem("profile_color", color);
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
                return '<div class="invite-player-row"><span class="color-dot" style="background:' + p.color + '"></span>' + esc(p.avatar) + ' ' + esc(p.name) + '</div>';
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
                player.points = Math.max(state.session.minPoints, Math.min(state.session.maxPoints, player.points + delta));
                var actualDelta = player.points - before;
                renderHero();
                renderOpponents({});
                if (actualDelta !== 0) {
                    var up = actualDelta > 0;
                    starrealms.vibrateForAmount(actualDelta);
                    starrealms.playImpact(actualDelta, up);
                    if (player.points <= state.session.minPoints) starrealms.playEliminate();
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
            var eliminated = me.points <= s.minPoints;
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

            el.innerHTML =
                '<div class="hero-top">' +
                    '<div class="hero-avatar" style="background:' + me.color + '">' + (eliminated ? "💀" : esc(me.avatar)) + '</div>' +
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
                                        '<span class="target-chip-avatar" style="background:' + p.color + '">' + esc(p.avatar) + '</span>' + esc(p.name) +
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
                var dead = p.points <= s.minPoints;
                var pulseCls = "";
                if (prevPoints[p.id] !== undefined && prevPoints[p.id] !== p.points) {
                    pulseCls = p.points > prevPoints[p.id] ? " score-row--up" : " score-row--down";
                }
                var teamLabel = "";
                if (s.isTeamMode && p.team !== null && p.team !== undefined) {
                    var mine = state.me && s.players.find(function(x) { return x.id === state.me.id; });
                    teamLabel = '<span class="opp-team">Hold ' + (p.team + 1) + (mine && mine.team === p.team ? " (dit)" : "") + '</span>';
                }
                return '<div class="opp-card' + pulseCls + (dead ? " opp-card--dead" : "") + '" data-opp-id="' + p.id + '" style="border-color:' + p.color + '">' +
                    '<div class="opp-avatar" style="background:' + p.color + '">' + (dead ? "💀" : esc(p.avatar)) + '</div>' +
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
                    if (p.points <= state.session.minPoints) starrealms.playEliminate();
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
                s.players.forEach(function(p) { if (p.points > s.minPoints) aliveTeams[p.team] = true; });
                var teams = Object.keys(aliveTeams);
                if (teams.length !== 1) return;
                var teamNum = parseInt(teams[0], 10);
                var teammate = s.players.find(function(p) { return p.team === teamNum; });
                winnerName = "Hold " + (teamNum + 1);
                winnerColor = teammate ? teammate.color : "#fff";
                winnerIds = s.players.filter(function(p) { return p.team === teamNum; }).map(function(p) { return p.id; });
            } else {
                var alive = s.players.filter(function(p) { return p.points > s.minPoints; });
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
                ctx.fillText(p.avatar || "?", 96, y + 62);

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
        var deviceToken = localStorage.getItem("device_id");
        var profile = null;
        var activeMonths = null;

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

        function loadRows() {
            var url = "/api/stats?profileId=" + encodeURIComponent(profile.id) + (activeMonths ? "&sinceMonths=" + activeMonths : "");
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

        if (!deviceToken) {
            document.getElementById("s-loading").style.display = "none";
            document.getElementById("s-empty").style.display = "";
            return;
        }

        apiGet("/api/profile?deviceToken=" + encodeURIComponent(deviceToken)).then(function(p) {
            profile = p;
            document.getElementById("s-loading").style.display = "none";
            document.getElementById("s-content").style.display = "";
            document.getElementById("s-who").textContent = p.avatar + " " + p.name + " · head-to-head mod dine modstandere";
            paintFilters();
            loadRows();
        }).catch(function() {
            document.getElementById("s-loading").style.display = "none";
            document.getElementById("s-empty").style.display = "";
        });
    }

    // ── Dispatch on load ─────────────────────────────────────────────────
    document.addEventListener("DOMContentLoaded", function() {
        var pageData = document.getElementById("page-data");
        if (!pageData) return;
        var page = pageData.getAttribute("data-page");
        if (page === "home") initHome();
        else if (page === "game") initGame(pageData.getAttribute("data-code"));
        else if (page === "stats") initStats();
    });
})();
