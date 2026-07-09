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

    function playHit() { tone(700, 120, 0.18, "sawtooth", 0.12); }
    function playHeal() { tone(400, 900, 0.22, "triangle", 0.12); }
    function playEliminate() { tone(300, 60, 0.6, "sawtooth", 0.18); }
    function playWinner() {
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

// ─────────────────────────────────────────────────────────────────────────
// App logic: no Blazor Server interactivity, no SignalR - the Cloudflare
// Tunnel this app is served through kills long-lived connections, so every
// page here is static SSR + REST fetch + polling instead.
// ─────────────────────────────────────────────────────────────────────────

(function() {
    var AVATARS = ["🚀", "👽", "🛸", "⭐", "🔥", "💀", "👑", "🐉", "🦾", "🎯", "🛡️", "⚔️"];

    function esc(s) {
        var d = document.createElement("div");
        d.textContent = s == null ? "" : String(s);
        return d.innerHTML;
    }

    function apiGet(url) {
        return fetch(url).then(function(r) {
            if (!r.ok) return r.text().then(function(t) { throw new Error(t || r.statusText); });
            return r.status === 204 ? null : r.json();
        });
    }

    function apiPost(url, body) {
        return fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body || {})
        }).then(function(r) {
            if (!r.ok) return r.text().then(function(t) { throw new Error(t || r.statusText); });
            return r.status === 204 ? null : r.json();
        });
    }

    function ensureLocalId(key) {
        var v = localStorage.getItem(key);
        if (!v) {
            v = (crypto.randomUUID ? crypto.randomUUID() : (Date.now() + "-" + Math.random())).replace(/-/g, "");
            localStorage.setItem(key, v);
        }
        return v;
    }

    function renderAvatarGrid(containerId, selected, onSelect) {
        var el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = AVATARS.map(function(a) {
            var active = a === selected ? " avatar-btn--active" : "";
            return '<button class="avatar-btn' + active + '" data-avatar="' + esc(a) + '">' + a + '</button>';
        }).join("");
        el.querySelectorAll(".avatar-btn").forEach(function(btn) {
            btn.onclick = function() { onSelect(btn.getAttribute("data-avatar")); };
        });
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
        var state = {
            avatar: localStorage.getItem("profile_avatar") || AVATARS[0],
            color: localStorage.getItem("profile_color") || COLORS[0],
            rulesets: [],
            selectedRulesetId: null,
            selectedRuleset: null,
            startingPoints: 50
        };

        document.getElementById("h-name").value = localStorage.getItem("profile_name") || "";

        function paintAvatars() { renderAvatarGrid("h-avatar-grid", state.avatar, function(a) { state.avatar = a; paintAvatars(); }); }
        function paintColors() { renderColorGrid("h-color-grid", COLORS, state.color, [], function(c) { state.color = c; paintColors(); }); }
        paintAvatars();
        paintColors();

        window.onNameInput = function() {
            document.getElementById("h-btn-ruleset").disabled = !document.getElementById("h-name").value.trim();
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
            var el = document.getElementById("h-points-row");
            var options = [50, 55, 60, 65, 70, 75];
            function paint() {
                el.innerHTML = options.map(function(p) {
                    var active = p === state.startingPoints ? " points-btn--active" : "";
                    return '<button class="points-btn' + active + '" data-pts="' + p + '">' + p + '</button>';
                }).join("");
                el.querySelectorAll(".points-btn").forEach(function(btn) {
                    btn.onclick = function() { state.startingPoints = parseInt(btn.getAttribute("data-pts"), 10); paint(); };
                });
            }
            paint();
            window.showStep(2);
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

        var state = {
            session: null,
            me: null,
            profileId: null,
            joinAvatar: localStorage.getItem("profile_avatar") || AVATARS[0],
            joinColor: localStorage.getItem("profile_color") || COLORS[0],
            tab: "score",
            pollTimer: null,
            winnerShown: false,
            ships: null,
            selectedShipFaction: ""
        };

        var profileName = localStorage.getItem("profile_name") || "";
        var profileAvatar = localStorage.getItem("profile_avatar") || "";

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

                showMainGame();
            }).catch(function(err) {
                document.getElementById("g-loading").textContent = "Spil ikke fundet.";
            });
        }

        function showNeedsName(session) {
            document.getElementById("n-ruleset-name").textContent = session.rulesetName;
            function paintAvatars() {
                renderAvatarGrid("n-avatar-grid", state.joinAvatar, function(a) { state.joinAvatar = a; paintAvatars(); });
            }
            paintAvatars();
            var takenColors = session.players.map(function(p) { return p.color; });
            function paintColors() {
                renderColorGrid("n-color-grid", COLORS, state.joinColor, takenColors, function(c) { state.joinColor = c; paintColors(); });
            }
            paintColors();

            document.getElementById("g-needs-name").style.display = "";
            window.onJoinNameInput = function() {
                document.getElementById("n-btn-join").disabled = !document.getElementById("n-name").value.trim();
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
                document.getElementById("g-invite").style.display = "none";
                showMainGame();
            };
        }

        function showMainGame() {
            document.getElementById("g-main").style.display = "";
            window.showTab = function(name) {
                state.tab = name;
                ["score", "log", "ai"].forEach(function(t) {
                    document.getElementById("tab-" + t).style.display = t === name ? "" : "none";
                    document.getElementById("tab-btn-" + t).classList.toggle("tab-btn--active", t === name);
                });
                if (name === "ai" && !state.ships) loadShips();
            };
            window.showTab("score");

            window.toggleRules = function() {
                var panel = document.getElementById("g-rules-panel");
                var show = panel.style.display === "none";
                panel.style.display = show ? "" : "none";
                document.getElementById("g-rules-toggle").textContent = (show ? "📜 Skjul regler" : "📜 Se regler for dette spil");
            };

            window.adjustPoints = function(delta) {
                if (!state.me) return;
                apiPost("/api/sessions/" + encodeURIComponent(code) + "/adjust", { playerId: state.me.id, delta: delta })
                    .then(refreshState).catch(function(err) { alert(err.message || "Kunne ikke opdatere"); });
            };

            window.endTurn = function() {
                if (!state.me) return;
                apiPost("/api/sessions/" + encodeURIComponent(code) + "/turn", { playerId: state.me.id })
                    .then(refreshState).catch(function(err) { alert(err.message || "Det er ikke din tur"); });
            };

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

                renderTurnBanner();
                renderHero();
                renderOpponents(prevPoints);
                renderRules();
                renderLog();
                checkWinner();
            }).catch(function() { /* transient network hiccup - next poll will retry */ });
        }

        function renderTurnBanner() {
            var s = state.session;
            var banner = document.getElementById("g-turn-banner");
            var mine = state.me && s.currentTurnPlayerId === state.me.id;
            banner.className = "turn-banner" + (mine ? " turn-banner--mine" : "");
            if (mine) {
                banner.innerHTML = "<span>🎯 Det er din tur!</span>";
            } else {
                var tp = s.players.find(function(p) { return p.id === s.currentTurnPlayerId; });
                banner.innerHTML = "<span>⏳ " + (tp ? esc(tp.avatar) + " " + esc(tp.name) + "'s tur" : "Venter…") + "</span>";
            }
        }

        function renderHero() {
            if (!state.me) return;
            var s = state.session;
            var me = s.players.find(function(p) { return p.id === state.me.id; });
            if (!me) return;
            var eliminated = me.points <= s.minPoints;
            var el = document.getElementById("g-hero-card");
            el.className = "hero-card";
            el.style.borderColor = me.color;
            el.innerHTML =
                '<div class="hero-top">' +
                    '<div class="hero-avatar" style="background:' + me.color + '">' + (eliminated ? "💀" : esc(me.avatar)) + '</div>' +
                    '<div class="hero-name">' + esc(me.name) + ' <span class="hero-you">(dig)</span></div>' +
                '</div>' +
                '<div class="hero-points" style="color:' + me.color + '">' + me.points + '</div>' +
                '<div class="hero-buttons">' +
                    '<button class="pt-btn pt-btn--down pt-btn--lg" onclick="adjustPoints(-5)">-5</button>' +
                    '<button class="pt-btn pt-btn--down pt-btn--lg" onclick="adjustPoints(-1)">-1</button>' +
                    '<button class="pt-btn pt-btn--up pt-btn--lg" onclick="adjustPoints(1)">+1</button>' +
                    '<button class="pt-btn pt-btn--up pt-btn--lg" onclick="adjustPoints(5)">+5</button>' +
                '</div>' +
                (s.currentTurnPlayerId === me.id ? '<button class="btn-secondary mt-2" onclick="endTurn()">✅ Afslut min tur →</button>' : '');
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
                return '<div class="opp-card' + pulseCls + (p.id === s.currentTurnPlayerId ? " opp-card--turn" : "") + (dead ? " opp-card--dead" : "") + '" style="border-color:' + p.color + '">' +
                    '<div class="opp-avatar" style="background:' + p.color + '">' + (dead ? "💀" : esc(p.avatar)) + '</div>' +
                    '<div class="opp-info"><div class="opp-name">' + esc(p.name) + teamLabel + '</div>' +
                    '<div class="opp-color-label"><span class="color-dot" style="background:' + p.color + '"></span>farve</div></div>' +
                    '<div class="opp-points" style="color:' + p.color + '">' + p.points + '</div>' +
                    (p.id === s.currentTurnPlayerId ? '<div class="opp-turn-badge">🎯</div>' : '') +
                    '</div>';
            }).join("");

            others.forEach(function(p) {
                if (prevPoints[p.id] !== undefined && prevPoints[p.id] !== p.points) {
                    starrealms.vibrate(20);
                    if (p.points > prevPoints[p.id]) starrealms.playHeal(); else starrealms.playHit();
                    if (p.points <= state.session.minPoints) starrealms.playEliminate();
                }
            });
        }

        function renderRules() {
            document.getElementById("g-rules-title").textContent = state.session.rulesetName;
            document.getElementById("g-rules-desc").textContent = state.session.rulesetDescription;
        }

        function renderLog() {
            var el = document.getElementById("g-log-panel");
            var events = state.session.events || [];
            if (events.length === 0) {
                el.innerHTML = '<p class="log-empty">Ingen hændelser endnu — pointændringer vises her, så alle kan se hvad der skete.</p>';
                return;
            }
            el.innerHTML = events.map(function(e) {
                var t = new Date(e.createdAt);
                var time = t.toLocaleTimeString("da-DK", { hour: "2-digit", minute: "2-digit" });
                return '<div class="log-row">' +
                    '<span class="log-avatar">' + esc(e.playerAvatar) + '</span>' +
                    '<span class="log-text"><strong>' + esc(e.playerName) + '</strong> ' + (e.delta >= 0 ? "fik" : "mistede") + ' ' + Math.abs(e.delta) + ' point <span class="log-muted">(nu ' + e.resultingPoints + ')</span></span>' +
                    '<span class="log-time">' + time + '</span>' +
                    '</div>';
            }).join("");
        }

        function checkWinner() {
            var s = state.session;
            if (!s.isCompleted) { state.winnerShown = false; return; }
            if (state.winnerShown) return;

            var winnerName, winnerColor;
            if (s.isTeamMode) {
                var aliveTeams = {};
                s.players.forEach(function(p) { if (p.points > s.minPoints) aliveTeams[p.team] = true; });
                var teams = Object.keys(aliveTeams);
                if (teams.length !== 1) return;
                var teamNum = parseInt(teams[0], 10);
                var teammate = s.players.find(function(p) { return p.team === teamNum; });
                winnerName = "Hold " + (teamNum + 1);
                winnerColor = teammate ? teammate.color : "#fff";
            } else {
                var alive = s.players.filter(function(p) { return p.points > s.minPoints; });
                if (alive.length !== 1) return;
                winnerName = alive[0].name;
                winnerColor = alive[0].color;
            }

            state.winnerShown = true;
            document.getElementById("g-winner-card").style.borderColor = winnerColor;
            var nameEl = document.getElementById("g-winner-name");
            nameEl.style.color = winnerColor;
            nameEl.textContent = winnerName + " vinder!";
            document.getElementById("g-winner").style.display = "";
            starrealms.playWinner();
            starrealms.confetti();
        }

        // ── AI tab ──────────────────────────────────────────────────────

        function loadShips() {
            apiGet("/api/ships").then(function(data) {
                state.ships = data;
                var select = document.getElementById("ai-ship-select");
                select.innerHTML = data.factions.map(function(fac) {
                    var opts = data.ships.filter(function(s) { return s.faction === fac.name; })
                        .map(function(s) { return '<option value="' + esc(s.name) + '" data-faction="' + esc(fac.name) + '">' + esc(s.name) + '</option>'; })
                        .join("");
                    return '<optgroup label="' + esc(fac.icon + " " + fac.name) + '">' + opts + '</optgroup>';
                }).join("");
            }).catch(function() {});
        }

        window.filterShips = function() {
            if (!state.ships) return;
            var q = document.getElementById("ai-ship-search").value.toLowerCase();
            var select = document.getElementById("ai-ship-select");
            select.innerHTML = state.ships.factions.map(function(fac) {
                var opts = state.ships.ships.filter(function(s) {
                    return s.faction === fac.name && (!q || s.name.toLowerCase().indexOf(q) !== -1);
                }).map(function(s) { return '<option value="' + esc(s.name) + '" data-faction="' + esc(fac.name) + '">' + esc(s.name) + '</option>'; }).join("");
                return opts ? '<optgroup label="' + esc(fac.icon + " " + fac.name) + '">' + opts + '</optgroup>' : "";
            }).join("");
        };

        window.getShipHint = function() {
            var select = document.getElementById("ai-ship-select");
            var name = select.value;
            if (!name) return;
            var faction = select.selectedOptions[0] ? select.selectedOptions[0].getAttribute("data-faction") : "";
            var btn = document.getElementById("ai-hint-btn");
            btn.disabled = true;
            btn.textContent = "Tænker…";
            var resultEl = document.getElementById("ai-hint-result");
            resultEl.style.display = "none";
            apiPost("/api/ai/hint", { shipName: name, faction: faction }).then(function(r) {
                resultEl.textContent = r.text || "Intet svar";
                resultEl.style.display = "";
            }).catch(function(err) {
                resultEl.textContent = "Fejl: " + err.message;
                resultEl.style.display = "";
            }).finally(function() {
                btn.disabled = false;
                btn.textContent = "💡 Få combo-tip";
            });
        };

        window.onTradeRowSelected = function() {
            var input = document.getElementById("ai-traderow-file");
            var file = input.files && input.files[0];
            if (!file) return;
            document.getElementById("ai-traderow-loading").style.display = "";
            var resultEl = document.getElementById("ai-traderow-result");
            resultEl.style.display = "none";

            var fd = new FormData();
            fd.append("file", file);
            fetch("/api/ai/traderow", { method: "POST", body: fd })
                .then(function(r) { return r.json(); })
                .then(function(r) {
                    resultEl.textContent = r.text || "Intet svar";
                    resultEl.style.display = "";
                }).catch(function(err) {
                    resultEl.textContent = "Fejl: " + err.message;
                    resultEl.style.display = "";
                }).finally(function() {
                    document.getElementById("ai-traderow-loading").style.display = "none";
                });
        };
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
