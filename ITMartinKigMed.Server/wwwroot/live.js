// Kig med: the camera + "Gå live" in the studio (WHIP), the player for viewers (WHEP, falls back to HLS),
// floating reactions and the viewer's key/name. The video goes through this site: /media/... and /hls/...
window.kig = (() => {
    const ice = [{ urls: 'stun:stun.l.google.com:19302' }];

    // ── viewer identity: a random key per phone/PC + the name they typed ──
    function viewer() {
        let key = '', name = '';
        try {
            key = localStorage.getItem('kig.key') || '';
            if (!key) { key = crypto.randomUUID().replace(/-/g, ''); localStorage.setItem('kig.key', key); }
            name = localStorage.getItem('kig.name') || '';
        } catch { key = key || Math.random().toString(36).slice(2); }
        return { key, name };
    }
    function setName(n) { try { localStorage.setItem('kig.name', n); } catch { } }

    // Offer -> wait for ICE (max 2 s, no trickle) -> POST the SDP -> answer.
    async function negotiate(pc, url) {
        await pc.setLocalDescription(await pc.createOffer());
        await new Promise(ok => {
            if (pc.iceGatheringState === 'complete') return ok();
            const t = setTimeout(ok, 2000);
            pc.addEventListener('icegatheringstatechange', () => { if (pc.iceGatheringState === 'complete') { clearTimeout(t); ok(); } });
        });
        const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/sdp' }, body: pc.localDescription.sdp });
        if (!r.ok) throw new Error('media ' + r.status);
        await pc.setRemoteDescription({ type: 'answer', sdp: await r.text() });
        return r.headers.get('Location');
    }

    // ── viewer player ──
    const players = {};
    async function play(id, onState) {
        stop(id);
        const video = document.getElementById(id); if (!video) return;
        const p = players[id] = { pc: null, hls: null };
        const say = s => { try { onState && onState.invokeMethodAsync('OnPlayer', s); } catch { } };
        say('forbinder');
        try {
            const pc = p.pc = new RTCPeerConnection({ iceServers: ice });
            pc.addTransceiver('video', { direction: 'recvonly' });
            pc.addTransceiver('audio', { direction: 'recvonly' });
            const stream = new MediaStream();
            pc.ontrack = e => { stream.addTrack(e.track); video.srcObject = stream; video.play().catch(() => { }); };
            await negotiate(pc, '/media/live/whep');
            const ok = await new Promise(res => {
                const t = setTimeout(() => res(false), 7000);
                pc.addEventListener('connectionstatechange', () => {
                    if (pc.connectionState === 'connected') { clearTimeout(t); res(true); }
                    if (pc.connectionState === 'failed') { clearTimeout(t); res(false); }
                });
            });
            if (ok) { say('direkte'); return; }
        } catch (e) { console.log('[kig] WebRTC', e); }
        // Fallback: HLS through the site (a few seconds behind, works on every network)
        if (p.pc) { p.pc.close(); p.pc = null; }
        video.srcObject = null;
        const src = '/hls/live/index.m3u8';
        if (video.canPlayType('application/vnd.apple.mpegurl')) video.src = src;
        else if (window.Hls && Hls.isSupported()) { p.hls = new Hls({ lowLatencyMode: true }); p.hls.loadSource(src); p.hls.attachMedia(video); }
        else { say('fejl'); return; }
        video.play().catch(() => { });
        say('hls');
    }
    function stop(id) {
        const p = players[id]; if (!p) return;
        if (p.pc) p.pc.close();
        if (p.hls) p.hls.destroy();
        const v = document.getElementById(id); if (v) { v.srcObject = null; v.removeAttribute('src'); v.load(); }
        delete players[id];
    }
    function unmute(id) { const v = document.getElementById(id); if (v) { v.muted = false; v.play().catch(() => { }); } }

    // ── studio: camera, microphone, publish ──
    let cam = null, pub = null, pubUrl = null;
    async function devices() {
        try { (await navigator.mediaDevices.getUserMedia({ video: true, audio: true })).getTracks().forEach(t => t.stop()); } catch { }
        const all = await navigator.mediaDevices.enumerateDevices();
        return all.filter(d => d.kind === 'videoinput' || d.kind === 'audioinput')
            .map(d => ({ kind: d.kind, id: d.deviceId, label: d.label || (d.kind === 'videoinput' ? 'Kamera' : 'Mikrofon') }));
    }
    async function preview(id, camId, micId, height) {
        if (cam) cam.getTracks().forEach(t => t.stop());
        cam = await navigator.mediaDevices.getUserMedia({
            video: { deviceId: camId ? { exact: camId } : undefined, height: { ideal: height || 1080 }, frameRate: { ideal: 30 } },
            audio: { deviceId: micId ? { exact: micId } : undefined, echoCancellation: true, noiseSuppression: true },
        });
        const v = document.getElementById(id); v.srcObject = cam; v.muted = true; v.play().catch(() => { });
        const s = cam.getVideoTracks()[0]?.getSettings() || {};
        return `${s.width || '?'}×${s.height || '?'} · ${Math.round(s.frameRate || 0)} fps`;
    }
    async function publish(bitrateKbps) {
        if (!cam) throw new Error('Tænd kameraet først');
        unpublish();
        const pc = pub = new RTCPeerConnection({ iceServers: ice });
        cam.getTracks().forEach(t => pc.addTransceiver(t, { direction: 'sendonly', streams: [cam] }));
        // H.264 first: HLS (the fallback for viewers) cannot carry VP8/VP9
        const h264 = (RTCRtpSender.getCapabilities('video')?.codecs || []).filter(c => /h264/i.test(c.mimeType));
        const rest = (RTCRtpSender.getCapabilities('video')?.codecs || []).filter(c => !/h264/i.test(c.mimeType));
        pc.getTransceivers().filter(t => t.sender.track?.kind === 'video').forEach(t => { try { t.setCodecPreferences([...h264, ...rest]); } catch { } });
        pubUrl = await negotiate(pc, '/media/live/whip');
        const sender = pc.getSenders().find(s => s.track?.kind === 'video');
        if (sender) { const p = sender.getParameters(); p.encodings = p.encodings?.length ? p.encodings : [{}]; p.encodings[0].maxBitrate = (bitrateKbps || 4000) * 1000; sender.setParameters(p).catch(() => { }); }
        return new Promise((ok, fail) => {
            const t = setTimeout(() => fail(new Error('Ingen forbindelse til videoserveren')), 10000);
            pc.addEventListener('connectionstatechange', () => {
                if (pc.connectionState === 'connected') { clearTimeout(t); ok('live'); }
                if (pc.connectionState === 'failed') { clearTimeout(t); fail(new Error('Forbindelsen fejlede')); }
            });
        });
    }
    function unpublish() {
        if (pubUrl) fetch(pubUrl, { method: 'DELETE' }).catch(() => { });
        if (pub) pub.close();
        pub = null; pubUrl = null;
    }
    function publishing() { return !!pub && pub.connectionState === 'connected'; }
    function cameraOff(id) { if (cam) cam.getTracks().forEach(t => t.stop()); cam = null; const v = document.getElementById(id); if (v) v.srcObject = null; }

    // ── reactions float up over the video ──
    function float(stageId, emoji) {
        const stage = document.getElementById(stageId); if (!stage) return;
        const e = document.createElement('span');
        e.className = 'kig-float'; e.textContent = emoji;
        e.style.left = (10 + Math.random() * 80) + '%';
        e.style.setProperty('--drift', (Math.random() * 60 - 30) + 'px');
        stage.appendChild(e); setTimeout(() => e.remove(), 2600);
    }

    return { viewer, setName, play, stop, unmute, devices, preview, publish, unpublish, publishing, cameraOff, float };
})();
