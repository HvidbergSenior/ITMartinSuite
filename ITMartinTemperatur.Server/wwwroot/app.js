// Temperatur - the page: asks the phone for its position, then /api/temperatur, and shows the nearest measured
// station big, the others in the chosen radius under it. Refreshes itself every 5 minutes while open.
(function () {
    'use strict';
    var km = 25, last = null, timer = null;
    try { km = parseInt(localStorage.getItem('tp-km') || '25', 10) || 25; } catch (e) { }

    function $(id) { return document.getElementById(id); }

    function fmtDeg(v) { return (Math.round(v * 10) / 10).toFixed(1).replace('.', ',') + '°'; }

    function ago(iso) {
        var min = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
        if (min < 1) return 'målt lige nu';
        if (min < 60) return 'målt for ' + min + ' min siden';
        return 'målt kl. ' + new Date(iso).toLocaleTimeString('da-DK', { hour: '2-digit', minute: '2-digit' });
    }

    function dist(k) { return k < 1 ? 'under 1 km væk' : (Math.round(k * 10) / 10).toString().replace('.', ',') + ' km væk'; }

    function msg(text) { var m = $('tp-msg'); m.textContent = text || ''; m.hidden = !text; }

    function markRadius() {
        document.querySelectorAll('.tp-radius [data-km]').forEach(function (b) {
            var on = parseInt(b.getAttribute('data-km'), 10) === km;
            b.classList.toggle('is-on', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
    }

    function show(d) {
        var list = $('tp-list'), card = $('tp-list-card');
        list.innerHTML = '';
        if (!d.her) {
            $('tp-deg').textContent = '–';
            $('tp-where').textContent = 'Ingen temperatur lige nu.';
            $('tp-when').textContent = '';
            msg('Vejrtjenesten svarer ikke. Prøv igen om lidt.');
            card.hidden = true;
            return;
        }
        $('tp-deg').textContent = fmtDeg(d.her.grader);
        if (d.maalt) {
            $('tp-where').textContent = d.her.station + ' · ' + dist(d.her.km);
            $('tp-when').textContent = ago(d.her.maalt) + ' · ' + d.her.kilde;
            // A far station measures the countryside, not your street - say so, so the number is not read as wrong.
            msg(d.her.km > 10
                ? 'Det nærmeste termometer er ' + Math.round(d.her.km) + ' km væk. I en by er det ofte lidt varmere end på stationen.'
                : '');
        } else {
            $('tp-where').innerHTML = '';
            var t = document.createElement('span'); t.textContent = 'Beregnet for dit sted';
            var b = document.createElement('div'); b.className = 'tp-calc'; b.textContent = 'Ikke målt – fra en vejrmodel';
            $('tp-where').append(t, b);
            $('tp-when').textContent = '';
            msg('Der er ingen vejrstation inden for ' + d.radius + ' km. Vælg en større afstand for at få en målt temperatur.');
        }
        var cmp = $('tp-compare');
        if (d.maalt && d.vejrApps !== null && d.vejrApps !== undefined) {
            var f = d.forskel, word = f === 0 ? 'præcis det samme som' : (Math.abs(f).toFixed(1).replace('.', ',') + '° ' + (f > 0 ? 'varmere end' : 'koldere end'));
            $('tp-compare-text').textContent = 'Vejr-apps siger ' + fmtDeg(d.vejrApps) + ' her. Termometeret på ' + d.her.station + ' viser ' + fmtDeg(d.her.grader) + ' – det er ' + word + ' vejr-apps.';
            cmp.hidden = false;
        } else {
            cmp.hidden = true;
        }
        var others = (d.stationer || []).slice(1);
        others.forEach(function (s) {
            var li = document.createElement('li');
            var deg = document.createElement('span'); deg.className = 'tp-l-deg'; deg.textContent = fmtDeg(s.grader);
            var name = document.createElement('span'); name.className = 'tp-l-name'; name.textContent = s.station;
            var sub = document.createElement('span'); sub.className = 'tp-l-sub'; sub.textContent = dist(s.km) + ' · ' + ago(s.maalt);
            name.appendChild(sub);
            li.append(deg, name);
            list.appendChild(li);
        });
        card.hidden = others.length === 0;
    }

    function load() {
        if (!last) return;
        msg('Henter …');
        fetch('/api/temperatur?lat=' + last.lat + '&lon=' + last.lon + '&km=' + km)
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(show)
            .catch(function () { msg('Kunne ikke hente temperaturen. Tjek forbindelsen, og prøv igen.'); });
    }

    // A town chosen by name instead of the device's position.
    function useTown(name, lat, lon) {
        last = { lat: lat, lon: lon };
        var p = $('tp-place'); p.textContent = 'Viser: ' + name; p.hidden = false;
        $('tp-hits').innerHTML = '';
        load();
        clearInterval(timer);
        timer = setInterval(load, 5 * 60 * 1000);
    }

    function search(e) {
        e.preventDefault();
        var q = $('tp-town').value.trim(), hits = $('tp-hits');
        if (!q) return;
        hits.textContent = 'Søger …';
        fetch('/api/steder?navn=' + encodeURIComponent(q))
            .then(function (r) { return r.json(); })
            .then(function (list) {
                hits.innerHTML = '';
                if (!list.length) { hits.textContent = 'Fandt ingen by med det navn. Prøv at stave det lidt anderledes.'; return; }
                list.forEach(function (t) {
                    var b = document.createElement('button');
                    b.type = 'button'; b.className = 'k-btn k-btn-sm'; b.textContent = t.navn;
                    b.addEventListener('click', function () { useTown(t.navn, t.lat, t.lon); });
                    hits.appendChild(b);
                });
            })
            .catch(function () { hits.textContent = 'Søgningen virker ikke lige nu. Prøv igen om lidt.'; });
    }

    function locate() {
        if (!navigator.geolocation) { msg('Din browser kan ikke give positionen.'); return; }
        msg('Finder din position …');
        navigator.geolocation.getCurrentPosition(function (p) {
            last = { lat: p.coords.latitude.toFixed(4), lon: p.coords.longitude.toFixed(4) };
            var pl = $('tp-place'); pl.textContent = 'Viser: der hvor du er'; pl.hidden = false;
            load();
            clearInterval(timer);
            timer = setInterval(load, 5 * 60 * 1000);
        }, function (e) {
            msg(e.code === 1
                ? 'Siden fik ikke lov til at bruge din position. Tryk på hængelåsen ved adressen, tillad Placering, og prøv igen.'
                : 'Din position kunne ikke findes. Skriv en by herunder i stedet.');
            $('tp-town').focus();
        }, { enableHighAccuracy: false, timeout: 15000, maximumAge: 10 * 60 * 1000 });
    }

    document.addEventListener('DOMContentLoaded', function () {
        markRadius();
        $('tp-find').addEventListener('click', locate);
        $('tp-search').addEventListener('submit', search);
        document.querySelectorAll('.tp-radius [data-km]').forEach(function (b) {
            b.addEventListener('click', function () {
                km = parseInt(b.getAttribute('data-km'), 10);
                try { localStorage.setItem('tp-km', String(km)); } catch (e) { }
                markRadius();
                load();
            });
        });
        // Allowed before: find it straight away.
        if (navigator.permissions && navigator.permissions.query) {
            navigator.permissions.query({ name: 'geolocation' }).then(function (s) { if (s.state === 'granted') locate(); }).catch(function () { });
        }
    });
})();
