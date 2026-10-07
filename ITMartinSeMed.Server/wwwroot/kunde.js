// Se med - the customer's side. Joins Martin's session with the code, shares the whole screen
// plus the microphone over WebRTC, and plays Martin's voice. The server only relays the handshake.
(function () {
    'use strict';
    var $ = function (id) { return document.getElementById(id); };
    var ICE = [{ urls: ['stun:stun.cloudflare.com:3478', 'stun:stun.l.google.com:19302'] }];
    var ws = null, pc = null, screen = null, mic = null, videoSender = null, step = 'trin-kode', kode = null, ping = null, forsoeg = 0;

    function show(s) {
        step = s;
        ['trin-kode', 'trin-del', 'trin-deler', 'trin-slut'].forEach(function (id) { $(id).hidden = id !== s; });
    }
    function send(o) { if (ws && ws.readyState === 1) ws.send(JSON.stringify(o)); }
    function fejl(text) { $('kode-fejl').textContent = text; $('kode-fejl').hidden = !text; }

    if (!(navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia)) {
        $('kun-pc').hidden = false;
        $('kode-form').hidden = true;
        return;
    }

    var kodeFelt = $('kode');
    var fra = new URLSearchParams(location.search).get('kode');
    if (fra) kodeFelt.value = fra;

    $('kode-form').addEventListener('submit', function (e) {
        e.preventDefault();
        var code = kodeFelt.value.replace(/\D/g, '');
        if (code.length !== 6) { fejl('Koden har 6 tal. Prøv igen.'); return; }
        fejl('');
        connect(code);
    });

    var nejTekst = {
        ukendt: 'Den kode kender vi ikke. Tjek tallene, eller bed Martin om en ny.',
        optaget: 'Den kode er allerede i brug. Bed Martin om en ny.',
        udloebet: 'Koden er for gammel. Bed Martin om en ny.',
        ventlidt: 'For mange forkerte koder. Vent 10 minutter, og prøv igen.'
    };

    function connect(code) {
        kode = code;
        ws = new WebSocket((location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + '/ws?rolle=kunde&kode=' + code);
        ws.onmessage = async function (ev) {
            var m = JSON.parse(ev.data);
            if (m.t === 'ok') {
                forsoeg = 0;
                // Back after a dropped line while sharing: keep sharing, only redo the picture if it broke.
                if (step === 'trin-deler' && screen && screen.active) {
                    $('forbinder').textContent = '';
                    if (!pc || pc.connectionState !== 'connected') await ring(screen.getVideoTracks()[0]);
                }
                else show('trin-del');
            }
            // The server may not have noticed her old line is dead yet ("optaget"), so keep trying a little.
            else if (m.t === 'nej' && step !== 'trin-kode' && m.hvorfor === 'optaget' && ++forsoeg < 10) { }
            else if (m.t === 'nej' && step !== 'trin-kode') afslut('Forbindelsen kunne ikke komme tilbage. Bed Martin om en ny kode.');
            else if (m.t === 'nej') fejl(nejTekst[m.hvorfor] || 'Det virkede ikke. Prøv igen.');
            else if (m.t === 'answer' && pc) await pc.setRemoteDescription(m.sdp);
            else if (m.t === 'ice' && pc && m.c) { try { await pc.addIceCandidate(m.c); } catch (e) { } }
            else if (m.t === 'slut') afslut('Martin har afsluttet. Tak for i dag!');
            else if (m.t === 'martin-ud') $('forbinder').textContent = 'Martin er lige røget af – bliv på siden, han kommer tilbage.';
            else if (m.t === 'martin-ind') {
                $('forbinder').textContent = '';
                if (step === 'trin-deler' && screen && screen.active && (!pc || pc.connectionState !== 'connected'))
                    await ring(screen.getVideoTracks()[0]);
            }
        };
        clearInterval(ping);
        ping = setInterval(function () { send({ t: 'ping' }); }, 25000);   // Cloudflare drops a quiet line after ~100 s
        ws.onclose = function () {
            clearInterval(ping);
            if (step !== 'trin-del' && step !== 'trin-deler') return;
            // Her own line dropped: try again with the same code instead of sending her back to the start.
            $('forbinder').textContent = 'Forbindelsen røg – forbinder igen …';
            setTimeout(function () { if (step === 'trin-del' || step === 'trin-deler') connect(kode); }, 3000);
        };
    }

    $('del').addEventListener('click', async function () {
        $('del-fejl').hidden = true;
        try {
            screen = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: 15 }, audio: false });
        } catch (e) { $('del-fejl').hidden = false; return; }
        var track = screen.getVideoTracks()[0];
        track.contentHint = 'detail';   // sharp text beats smooth motion when Martin reads a menu out loud
        track.onended = function () {   // the browser's own "Stop sharing" bar
            if (step !== 'trin-deler') return;
            $('del').textContent = '🖥️ Del skærm igen';
            show('trin-del');
        };

        if (!mic) {
            try { mic = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true } }); }
            catch (e) { mic = null; }
        }
        $('kan-hoere').hidden = !mic;
        $('mik').hidden = !mic;

        if (pc && videoSender) {          // sharing again in the same session
            await videoSender.replaceTrack(track);
            show('trin-deler');
            return;
        }

        await ring(track);
        show('trin-deler');
    });

    // Starts (or restarts) the picture + voice to Martin with the shared screen track.
    async function ring(track) {
        if (pc) pc.close();
        pc = new RTCPeerConnection({ iceServers: ICE });
        pc.onicecandidate = function (e) { if (e.candidate) send({ t: 'ice', c: e.candidate }); };
        pc.ontrack = function (e) {
            if (e.track.kind !== 'audio') return;
            $('lyd').srcObject = new MediaStream([e.track]);
            $('lyd').play().catch(function () { });
        };
        pc.onconnectionstatechange = function () {
            if (!pc) return;
            var s = pc.connectionState;
            $('forbinder').textContent = s === 'connected' ? '' :
                s === 'failed' ? 'Billedet kan ikke komme igennem til Martin. Ring til ham – så finder I en anden vej.' : 'Forbinder …';
        };
        videoSender = pc.addTrack(track, screen);
        if (mic) pc.addTrack(mic.getAudioTracks()[0], mic);
        else pc.addTransceiver('audio', { direction: 'recvonly' });   // she can still hear Martin
        await pc.setLocalDescription(await pc.createOffer());
        send({ t: 'offer', sdp: pc.localDescription });
    }

    $('mik').addEventListener('click', function () {
        if (!mic) return;
        var t = mic.getAudioTracks()[0];
        t.enabled = !t.enabled;
        this.textContent = t.enabled ? '🎤 Slå mikrofonen fra' : '🔇 Slå mikrofonen til igen';
    });

    $('stop').addEventListener('click', function () { afslut('Tak! Martin kan ikke længere se din skærm.'); });

    function afslut(text) {
        if (step === 'trin-slut') return;
        show('trin-slut');
        $('slut-tekst').textContent = text;
        [screen, mic].forEach(function (s) { if (s) s.getTracks().forEach(function (t) { t.stop(); }); });
        if (pc) pc.close();
        if (ws) ws.close();
        pc = null; ws = null;
    }
})();
