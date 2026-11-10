// Dødsbo og loft (2026-11-10): sager -> one sag -> photo per thing -> "Vurdér de nye" (one AI call for up to 8) -> own price.
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    function sync() { var b = $('themeToggleBtn'); if (b) b.textContent = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'Mørk' : 'Lys'; }
    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('#themeToggleBtn')) return;
        var next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('vurdering_theme', next); } catch (err) { }
        sync();
    });
    sync();

    if (!$('db-list')) return;   // the login page

    var sag = null;
    function msg(t, bad) { var m = $('db-msg'); m.textContent = t || ''; m.classList.toggle('is-bad', !!bad); }

    function api(path, opts) {
        return fetch('/api/dodsbo' + path, opts).then(function (r) {
            return r.json().catch(function () { return {}; }).then(function (j) {
                if (!r.ok) throw new Error(j.fejl || 'Det gik galt – prøv igen.');
                return j;
            });
        });
    }
    function post(path, body) {
        return api(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}) });
    }

    // ── the list ──
    function list() {
        $('db-sag').hidden = true; $('db-list').hidden = false; sag = null;
        try { localStorage.removeItem('dodsbo_sag'); } catch (e) { }
        api('').then(function (all) {
            $('db-sager').innerHTML = all.length === 0 ? '<p class="k-help">Ingen sager endnu. Lav den første herunder.</p>'
                : all.map(function (s) {
                    return '<button type="button" class="db-sag-btn" data-sag="' + esc(s.id) + '"><b>' + esc(s.name) + '</b><span>' + s.ting + ' ting'
                        + (s.venter ? ' · ' + s.venter + ' venter' : '') + (s.vaerdi ? ' · ' + esc(s.vaerdi) : '') + '</span></button>';
                }).join('');
        }).catch(function (e) { $('db-sager').innerHTML = '<p>' + esc(e.message) + '</p>'; });
    }
    $('db-sager').addEventListener('click', function (e) { var b = e.target.closest('[data-sag]'); if (b) open(b.getAttribute('data-sag')); });
    $('db-make').addEventListener('click', function () {
        post('', { navn: $('db-new').value }).then(function (r) { $('db-new').value = ''; open(r.id); })
            .catch(function (e) { alertBox(e.message); });
    });
    function alertBox(t) { $('db-sager').insertAdjacentHTML('afterbegin', '<p class="vu-msg is-bad">' + esc(t) + '</p>'); }
    $('db-back').addEventListener('click', list);

    // ── one sag ──
    function open(id) {
        api('/' + encodeURIComponent(id)).then(function (s) {
            sag = s; $('db-list').hidden = true; $('db-sag').hidden = false;
            try { localStorage.setItem('dodsbo_sag', s.id); } catch (e) { }
            draw();
        }).catch(function () { list(); });
    }
    function draw() {
        $('db-name').textContent = sag.name;
        $('db-total').textContent = sag.vaerdi ? 'Det, der er værd at sælge, er samlet cirka ' + sag.vaerdi : 'Ingen vurderet endnu.';
        $('db-value').textContent = sag.venter ? 'Vurdér de nye (' + sag.venter + ')' : 'Vurdér de nye';
        $('db-value').disabled = !sag.venter;
        $('db-ting').innerHTML = sag.ting.map(card).join('') || '<p class="k-help">Ingen billeder endnu. Tryk Tag billede af en ting.</p>';
    }
    function card(t) {
        var photo = '<img class="db-photo" loading="lazy" alt="Billede af tingen" src="/api/dodsbo/' + encodeURIComponent(sag.id) + '/foto/' + encodeURIComponent(t.id) + '">';
        if (t.venter) {
            return '<div class="db-card">' + photo + '<div><p><b>Venter på at blive vurderet</b></p>' + (t.hint ? '<p class="k-help">' + esc(t.hint) + '</p>' : '')
                + '<button type="button" class="k-btn k-btn-sm k-btn-ghost" data-remove="' + esc(t.id) + '">Fjern</button></div></div>';
        }
        var q = encodeURIComponent(t.searchWords || t.what);
        var price = t.egen ? '<b>Din pris: ' + t.egen.toLocaleString('da-DK') + ' kr.</b>' + (t.pris ? ' <span class="k-help">(skøn ' + esc(t.pris) + ')</span>' : '')
            : (t.pris ? '<b>Skøn: ' + esc(t.pris) + '</b>' : '<b>Ingen pris – spørg en ekspert</b>');
        return '<div class="db-card">' + photo + '<div>'
            + '<h3 class="db-what">' + esc(t.what) + '</h3>'
            + '<p>' + price + '</p>'
            + '<p><span class="k-tag db-dom" data-dom="' + esc(t.domKode) + '">' + esc(t.dom) + '</span>' + (t.where ? ' · sælges bedst på <b>' + esc(t.where) + '</b>' : '') + '</p>'
            + (t.reason ? '<p class="k-help">' + esc(t.reason) + '</p>' : '')
            + (t.details ? '<p class="k-help"><b>Kig efter:</b> ' + esc(t.details) + '</p>' : '')
            + '<div class="db-actions">'
            + '<a class="k-btn k-btn-sm" target="_blank" rel="noopener" href="https://www.dba.dk/soeg/?soeg=' + q + '">Søg på DBA</a>'
            + '<a class="k-btn k-btn-sm" target="_blank" rel="noopener" href="https://www.lauritz.com/da/soeg?q=' + q + '">Søg på Lauritz</a></div>'
            + '<details class="db-own"><summary>Ret prisen selv</summary>'
            + '<div class="vu-own-row"><input class="k-input" inputmode="numeric" placeholder="kr." value="' + (t.egen || '') + '" data-kr="' + esc(t.id) + '">'
            + '<button type="button" class="k-btn k-btn-sm k-btn-primary" data-own="' + esc(t.id) + '">Gem</button></div>'
            + '<input class="k-input k-mt" maxlength="300" placeholder="Note – fx solgt til nabo" value="' + esc(t.ownNote) + '" data-note="' + esc(t.id) + '">'
            + '<button type="button" class="k-btn k-btn-sm k-btn-ghost k-mt" data-remove="' + esc(t.id) + '">Fjern tingen</button></details>'
            + '</div></div>';
    }
    $('db-ting').addEventListener('click', function (e) {
        var own = e.target.closest('[data-own]'), rm = e.target.closest('[data-remove]');
        if (own) {
            var id = own.getAttribute('data-own');
            post('/' + sag.id + '/ting/' + id + '/egen', { kr: document.querySelector('[data-kr="' + id + '"]').value, note: document.querySelector('[data-note="' + id + '"]').value })
                .then(function (r) { msg(r.besked); open(sag.id); }).catch(function (er) { msg(er.message, true); });
        } else if (rm) {
            post('/' + sag.id + '/ting/' + rm.getAttribute('data-remove') + '/fjern').then(function (r) { msg(r.besked); open(sag.id); })
                .catch(function (er) { msg(er.message, true); });
        }
    });

    // The phone shrinks the photo to 1600 px before it is sent - small for the server and for the AI.
    function shrink(file) {
        return new Promise(function (ok, bad) {
            var img = new Image(), url = URL.createObjectURL(file);
            img.onload = function () {
                var s = Math.min(1, 1600 / Math.max(img.width, img.height)), c = document.createElement('canvas');
                c.width = Math.round(img.width * s); c.height = Math.round(img.height * s);
                c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
                URL.revokeObjectURL(url);
                c.toBlob(function (b) { b ? ok(b) : bad(new Error('Billedet kunne ikke læses.')); }, 'image/jpeg', 0.85);
            };
            img.onerror = function () { URL.revokeObjectURL(url); bad(new Error('Billedet kunne ikke læses.')); };
            img.src = url;
        });
    }
    $('db-photo').addEventListener('change', function (e) {
        var files = Array.prototype.slice.call(e.target.files || []);
        e.target.value = '';
        if (!files.length || !sag) return;
        msg('Gemmer ' + files.length + ' billede' + (files.length > 1 ? 'r' : '') + ' …');
        files.reduce(function (p, f) {
            return p.then(function () { return shrink(f); }).then(function (blob) {
                var fd = new FormData(); fd.append('foto', blob, 'ting.jpg'); fd.append('hint', $('db-hint').value);
                return api('/' + sag.id + '/foto', { method: 'POST', body: fd });
            });
        }, Promise.resolve()).then(function () {
            $('db-hint').value = ''; msg('Gemt. Tag det næste billede – eller tryk Vurdér de nye.'); open(sag.id);
        }).catch(function (er) { msg(er.message, true); open(sag.id); });
    });
    $('db-value').addEventListener('click', function () {
        if (!sag) return;
        $('db-value').disabled = true; msg('Vurderer … det tager typisk et halvt minut.');
        post('/' + sag.id + '/vurder').then(function (r) { msg(r.besked); open(sag.id); })
            .catch(function (er) { msg(er.message, true); $('db-value').disabled = false; });
    });

    var last = null;
    try { last = localStorage.getItem('dodsbo_sag'); } catch (e) { }
    if (last) open(last); else list();
})();
