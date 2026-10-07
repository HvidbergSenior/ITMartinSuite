// Se med - Martin's side. Opens a session (the server hands out the 6-digit code), answers the
// customer's WebRTC offer with his own microphone, and shows her screen.
(function () {
    'use strict';
    var $ = function (id) { return document.getElementById(id); };
    var ICE = [{ urls: ['stun:stun.cloudflare.com:3478', 'stun:stun.l.google.com:19302'] }];
    var ws = null, pc = null, mic = null, kode = null, afsluttet = false, ping = null;

    function send(o) { if (ws && ws.readyState === 1) ws.send(JSON.stringify(o)); }
    function status(html) { $('status').innerHTML = html; }

    $('start').addEventListener('click', async function () {
        try { mic = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } }); }
        catch (e) { mic = null; }
        $('m-start').hidden = true;
        $('m-aktiv').hidden = false;
        $('mik').hidden = !mic;
        $('slut').hidden = false;
        afsluttet = false;
        forbind();
    });

    // Opens (or after a dropped line re-opens) the session. The server keeps it for 10 minutes,
    // so her screen and the code survive a short break on Martin's side.
    function forbind() {
        ws = new WebSocket((location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + '/ws?rolle=martin' + (kode ? '&kode=' + kode : ''));
        ws.onmessage = async function (ev) {
            var m = JSON.parse(ev.data);
            if (m.t === 'kode') {
                var ny = kode && kode !== m.kode;
                kode = m.kode;
                $('kodevis').textContent = m.kode.slice(0, 3) + ' ' + m.kode.slice(3);
                if (!ny && pc && pc.connectionState === 'connected') status('Du ser kundens skærm. Tal bare – hun kan høre dig.');
                else status((ny ? '<b>Den gamle session var udløbet – her er en ny kode.</b> ' : '') +
                    'Læs koden op. Kunden åbner <b>semed.itmartin.dk</b>.' + (mic ? '' : ' <b>Ingen mikrofon</b> – kunden kan ikke høre dig.'));
            }
            else if (m.t === 'kunde-ind') { if (!pc) status('Kunden er forbundet – venter på, at hun trykker <b>Del skærm</b>.'); }
            else if (m.t === 'kunde-ud') status('Kunden er røget af. Vent – hun kommer selv tilbage, eller hun skriver koden <b>' + kode.slice(0, 3) + ' ' + kode.slice(3) + '</b> igen.');
            else if (m.t === 'offer') await svar(m.sdp);
            else if (m.t === 'ice' && pc && m.c) { try { await pc.addIceCandidate(m.c); } catch (e) { } }
        };
        clearInterval(ping);
        ping = setInterval(function () { send({ t: 'ping' }); }, 25000);   // Cloudflare drops a quiet line after ~100 s
        ws.onclose = function () {
            clearInterval(ping);
            if (!afsluttet) {
                status('Forbindelsen røg – forbinder igen …');
                setTimeout(forbind, 2000);
                return;
            }
            luk();
            kode = null;
            status('Sessionen er slut. Tryk Start for en ny kode.');
            $('kodevis').textContent = '––– –––';
            $('slut').hidden = true;
            $('m-start').hidden = false;
        };
    }

    async function svar(sdp) {
        luk();
        pc = new RTCPeerConnection({ iceServers: ICE });
        pc.onicecandidate = function (e) { if (e.candidate) send({ t: 'ice', c: e.candidate }); };
        pc.ontrack = function (e) {
            if (e.track.kind === 'video') {
                $('skaerm').srcObject = new MediaStream([e.track]);
                status('Du ser kundens skærm. Tal bare – hun kan høre dig.');
            } else {
                $('lyd').srcObject = new MediaStream([e.track]);
                $('lyd').play().catch(function () { });
            }
        };
        pc.onconnectionstatechange = function () {
            if (pc && pc.connectionState === 'failed')
                status('Billedet kan ikke komme igennem (netværket hos kunden). Brug Windows’ <b>Hurtig hjælp</b> i stedet.');
        };
        await pc.setRemoteDescription(sdp);
        var audio = pc.getTransceivers().find(function (t) { return t.receiver.track && t.receiver.track.kind === 'audio'; });
        if (audio && mic) {
            await audio.sender.replaceTrack(mic.getAudioTracks()[0]);
            audio.direction = 'sendrecv';   // becomes sendonly by itself when she has no microphone
        }
        await pc.setLocalDescription(await pc.createAnswer());
        send({ t: 'answer', sdp: pc.localDescription });
    }

    function luk() {
        if (pc) pc.close();
        pc = null;
        $('skaerm').srcObject = null;
    }

    $('mik').addEventListener('click', function () {
        if (!mic) return;
        var t = mic.getAudioTracks()[0];
        t.enabled = !t.enabled;
        this.textContent = t.enabled ? '🎤 Slå min mikrofon fra' : '🔇 Slå min mikrofon til igen';
    });
    $('fuld').addEventListener('click', function () { if ($('skaerm').requestFullscreen) $('skaerm').requestFullscreen(); });
    $('slut').addEventListener('click', function () {
        if (mic) mic.getTracks().forEach(function (t) { t.stop(); });
        mic = null;
        afsluttet = true;
        send({ t: 'slut' });
        if (ws) ws.close();
    });
})();
