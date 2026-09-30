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
        if (!r.ok) { const e = new Error('media ' + r.status); e.status = r.status; throw e; }
        await pc.setRemoteDescription({ type: 'answer', sdp: await r.text() });
        return r.headers.get('Location');
    }

    // ── viewer player ──
    const players = {};
    // path = MediaMTX path: 'live' (Martin) or 'gaest' (the viewer who has the floor); cb = the .NET method told the state
    async function play(id, onState, path = 'live', cb = 'OnPlayer') {
        stop(id);
        const video = document.getElementById(id); if (!video) return;
        const p = players[id] = { pc: null, hls: null, retry: null };
        // no video yet (Martin not sending, or he just reloaded the studio): wait and try again - never a silent black box
        const again = (ms) => { if (players[id] === p) p.retry = setTimeout(() => { if (players[id] === p) play(id, onState, path, cb); }, ms); };
        const say = s => { try { onState && onState.invokeMethodAsync(cb, s); } catch { } };
        say('forbinder');
        try {
            const pc = p.pc = new RTCPeerConnection({ iceServers: ice });
            pc.addTransceiver('video', { direction: 'recvonly' });
            pc.addTransceiver('audio', { direction: 'recvonly' });
            const stream = new MediaStream();
            pc.ontrack = e => { stream.addTrack(e.track); video.srcObject = stream; video.play().catch(() => { }); };
            await negotiate(pc, `/media/${path}/whep`);
            const ok = await new Promise(res => {
                const t = setTimeout(() => res(false), 7000);
                pc.addEventListener('connectionstatechange', () => {
                    if (pc.connectionState === 'connected') { clearTimeout(t); res(true); }
                    if (pc.connectionState === 'failed') { clearTimeout(t); res(false); }
                });
            });
            if (ok) {
                say('direkte');
                // the video stops later (Martin stopped or reloaded): back to waiting
                pc.addEventListener('connectionstatechange', () => {
                    if (['failed', 'closed', 'disconnected'].includes(pc.connectionState) && players[id] === p) { say('venter'); again(3000); }
                });
                return;
            }
        } catch (e) {
            console.log('[kig] WebRTC', e);
            if (e.status === 404) { say('venter'); again(5000); return; }
        }
        // Fallback: HLS through the site (a few seconds behind, works on every network)
        if (p.pc) { p.pc.close(); p.pc = null; }
        video.srcObject = null;
        const src = `/hls/${path}/index.m3u8`;
        if (video.canPlayType('application/vnd.apple.mpegurl')) video.src = src;
        else if (window.Hls && Hls.isSupported()) { p.hls = new Hls({ lowLatencyMode: true }); p.hls.loadSource(src); p.hls.attachMedia(video); }
        else { say('fejl'); return; }
        video.play().catch(() => { });
        say('hls');
    }
    function stop(id) {
        const p = players[id]; if (!p) return;
        delete players[id];
        clearTimeout(p.retry);
        if (p.pc) p.pc.close();
        if (p.hls) p.hls.destroy();
        const v = document.getElementById(id); if (v) { v.srcObject = null; v.removeAttribute('src'); v.load(); }
    }
    function unmute(id) { const v = document.getElementById(id); if (v) { v.muted = false; v.play().catch(() => { }); } }

    // ── the viewer who has the floor: microphone (and camera if they want) to path "gaest" ──
    let guest = null, guestUrl = null;
    async function guestStart(token, withCam) {
        guestStop();
        const media = await navigator.mediaDevices.getUserMedia({
            audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
            video: withCam ? { height: { ideal: 480 }, frameRate: { ideal: 24 }, facingMode: 'user' } : false,
        });
        const pc = new RTCPeerConnection({ iceServers: ice });
        media.getTracks().forEach(t => pc.addTransceiver(t, { direction: 'sendonly', streams: [media] }));
        const h264 = (RTCRtpSender.getCapabilities('video')?.codecs || []).filter(c => /h264/i.test(c.mimeType));
        const rest = (RTCRtpSender.getCapabilities('video')?.codecs || []).filter(c => !/h264/i.test(c.mimeType));
        pc.getTransceivers().filter(t => t.sender.track?.kind === 'video').forEach(t => { try { t.setCodecPreferences([...h264, ...rest]); } catch { } });
        guest = { pc, media };
        guestUrl = await negotiate(pc, '/media/gaest/whip?t=' + encodeURIComponent(token));
        return 'ok';
    }
    function guestStop() {
        if (guestUrl) fetch(guestUrl, { method: 'DELETE' }).catch(() => { });
        if (guest) { guest.pc.close(); guest.media.getTracks().forEach(t => t.stop()); }
        guest = null; guestUrl = null;
    }
    function guestMute(on) { guest?.media.getAudioTracks().forEach(t => t.enabled = !on); }

    // ── studio: camera, microphone, publish ──
    let cam = null, pub = null, pubUrl = null;   // cam = camera + microphone (the mic is always the sound)
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
        previewId = id;
        camVideo.srcObject = cam; camVideo.play().catch(() => { });   // the corner follows a camera switch
        await showMode();
        const s = cam.getVideoTracks()[0]?.getSettings() || {};
        return `${s.width || '?'}×${s.height || '?'} · ${Math.round(s.frameRate || 0)} fps`;
    }

    // ── what viewers see: "kamera", "skaerm" (the PC screen) or "begge" (screen with the camera in a corner) ──
    let mode = 'kamera', screen = null, previewId = 'preview', onModeLost = null;
    let canvas = null, canvasTrack = null, ticker = null;
    const screenVideo = document.createElement('video'), camVideo = document.createElement('video');
    [screenVideo, camVideo].forEach(v => { v.muted = true; v.playsInline = true; });

    // A worker ticks the drawing: Chrome throttles timers in a hidden tab, and while sharing the screen
    // Martin is in another window - a worker's timer keeps running at full speed.
    function startTicker(draw) {
        stopTicker();
        const src = 'let t=setInterval(()=>postMessage(0),33);onmessage=()=>clearInterval(t);';
        ticker = new Worker(URL.createObjectURL(new Blob([src], { type: 'text/javascript' })));
        ticker.onmessage = draw;
    }
    function stopTicker() { if (ticker) { ticker.postMessage(0); ticker.terminate(); ticker = null; } }

    function composite() {
        const st = screen.getVideoTracks()[0].getSettings();
        canvas = canvas || document.createElement('canvas');
        canvas.width = Math.min(st.width || 1920, 1920);
        canvas.height = Math.round(canvas.width * (st.height || 1080) / (st.width || 1920));
        const g = canvas.getContext('2d');
        screenVideo.srcObject = screen; screenVideo.play().catch(() => { });
        camVideo.srcObject = cam; camVideo.play().catch(() => { });
        startTicker(() => {
            const W = canvas.width, H = canvas.height;
            g.drawImage(screenVideo, 0, 0, W, H);
            if (cam && camVideo.videoWidth) {
                // camera bottom-right, a quarter of the width, rounded, white edge
                const w = Math.round(W * 0.24), h = Math.round(w * camVideo.videoHeight / camVideo.videoWidth);
                const x = W - w - Math.round(W * 0.02), y = H - h - Math.round(W * 0.02), r = Math.round(w * 0.06);
                g.save(); g.beginPath(); g.roundRect(x, y, w, h, r); g.clip();
                g.drawImage(camVideo, x, y, w, h); g.restore();
                g.lineWidth = Math.max(3, W / 400); g.strokeStyle = 'rgba(255,255,255,.9)';
                g.beginPath(); g.roundRect(x, y, w, h, r); g.stroke();
            }
        });
        canvasTrack = canvas.captureStream(30).getVideoTracks()[0];
        return canvasTrack;
    }

    function outVideo() {
        if (mode === 'kamera') return cam?.getVideoTracks()[0] || null;
        if (!screen) return null;
        if (mode === 'skaerm') return screen.getVideoTracks()[0];
        return canvasTrack || composite();
    }

    async function showMode() {
        const track = outVideo();
        const v = document.getElementById(previewId);
        if (v) { v.srcObject = track ? new MediaStream([track]) : null; v.muted = true; v.play().catch(() => { }); }
        const tr = pub?.getTransceivers().find(t => t.receiver.track?.kind === 'video');
        if (tr && track) await tr.sender.replaceTrack(track);   // switch while live - no new connection
        const au = pub?.getTransceivers().find(t => t.receiver.track?.kind === 'audio');
        const mic = cam?.getAudioTracks()[0];
        if (au && mic && au.sender.track !== mic) await au.sender.replaceTrack(mic);   // a new microphone too
    }

    // Returns the mode that is now on (screen sharing can be cancelled in Chrome's picker).
    async function setMode(m, dotnet) {
        onModeLost = dotnet || onModeLost;
        if (m !== 'kamera' && !screen) {
            try {
                screen = await navigator.mediaDevices.getDisplayMedia({ video: { frameRate: { ideal: 30 }, width: { max: 1920 } }, audio: false });
            } catch { return mode; }
            screen.getVideoTracks()[0].addEventListener('ended', async () => {
                // "Stop sharing" in Chrome's bar: back to the camera
                screen = null; stopTicker(); canvasTrack = null;
                mode = 'kamera'; await showMode();
                try { onModeLost && onModeLost.invokeMethodAsync('OnModeLost'); } catch { }
            });
        }
        if (m !== 'begge') { stopTicker(); if (canvasTrack) canvasTrack.stop(); canvasTrack = null; }
        if (m === 'kamera' && screen) { screen.getTracks().forEach(t => t.stop()); screen = null; }
        mode = m;
        await showMode();
        return mode;
    }

    async function publish(bitrateKbps, dotnet) {
        onModeLost = dotnet || onModeLost;   // the studio hears when the outgoing video drops
        const video = outVideo();
        if (!video) throw new Error('Tænd kameraet først');
        unpublish();
        const pc = pub = new RTCPeerConnection({ iceServers: ice });
        const out = new MediaStream([video, ...(cam ? cam.getAudioTracks() : [])]);
        out.getTracks().forEach(t => pc.addTransceiver(t, { direction: 'sendonly', streams: [out] }));
        // H.264 first: HLS (the fallback for viewers) cannot carry VP8/VP9
        const h264 = (RTCRtpSender.getCapabilities('video')?.codecs || []).filter(c => /h264/i.test(c.mimeType));
        const rest = (RTCRtpSender.getCapabilities('video')?.codecs || []).filter(c => !/h264/i.test(c.mimeType));
        pc.getTransceivers().filter(t => t.sender.track?.kind === 'video').forEach(t => { try { t.setCodecPreferences([...h264, ...rest]); } catch { } });
        pubUrl = await negotiate(pc, '/media/live/whip');
        const sender = pc.getSenders().find(s => s.track?.kind === 'video');
        if (sender) {
            const p = sender.getParameters(); p.encodings = p.encodings?.length ? p.encodings : [{}];
            p.encodings[0].maxBitrate = (bitrateKbps || 4000) * 1000;
            p.degradationPreference = 'maintain-resolution';   // code on screen must stay sharp - drop frames instead
            sender.setParameters(p).catch(() => { });
        }
        return new Promise((ok, fail) => {
            const t = setTimeout(() => fail(new Error('Ingen forbindelse til videoserveren')), 10000);
            pc.addEventListener('connectionstatechange', () => {
                if (pc.connectionState === 'connected') { clearTimeout(t); ok('live'); }
                if (pc.connectionState === 'failed') { clearTimeout(t); fail(new Error('Forbindelsen fejlede')); }
                if (pc.connectionState === 'failed' && pub === pc) { try { onModeLost && onModeLost.invokeMethodAsync('OnVideoLost'); } catch { } }
            });
        });
    }
    function unpublish() {
        if (pubUrl) fetch(pubUrl, { method: 'DELETE' }).catch(() => { });
        if (pub) pub.close();
        pub = null; pubUrl = null;
    }
    function publishing() { return !!pub && pub.connectionState === 'connected'; }
    // Refreshing or closing the studio while live cuts the video - let Chrome ask first.
    window.addEventListener('beforeunload', e => { if (publishing()) { e.preventDefault(); e.returnValue = ''; } });
    function cameraOff(id) {
        if (cam) cam.getTracks().forEach(t => t.stop());
        if (screen) screen.getTracks().forEach(t => t.stop());
        cam = null; screen = null; stopTicker(); canvasTrack = null; mode = 'kamera';
        const v = document.getElementById(id); if (v) v.srcObject = null;
    }

    // ── reactions float up over the video ──
    function float(stageId, emoji) {
        const stage = document.getElementById(stageId); if (!stage) return;
        const e = document.createElement('span');
        e.className = 'kig-float'; e.textContent = emoji;
        e.style.left = (10 + Math.random() * 80) + '%';
        e.style.setProperty('--drift', (Math.random() * 60 - 30) + 'px');
        stage.appendChild(e); setTimeout(() => e.remove(), 2600);
    }

    return { viewer, setName, play, stop, unmute, guestStart, guestStop, guestMute, devices, preview, setMode, publish, unpublish, publishing, cameraOff, float };
})();

// Studio "Inviter seere": copy the link / the system share menu (Windows, phones).
window.kigShare = {
    copy: async (text) => { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } },
    canShare: () => !!navigator.share,
    share: async (title, text, url) => { try { await navigator.share({ title, text, url }); return true; } catch { return false; } },
};
