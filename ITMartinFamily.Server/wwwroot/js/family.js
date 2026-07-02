window.familyApp = {
    getSession:   function(slug) { return localStorage.getItem('family_session_' + slug) || ''; },
    setSession:   function(slug, id) { localStorage.setItem('family_session_' + slug, id); localStorage.setItem('family_last_slug', slug); },
    clearSession: function(slug) { localStorage.removeItem('family_session_' + slug); localStorage.removeItem('family_last_slug'); },
    getLastSlug:  function() { return localStorage.getItem('family_last_slug') || ''; },
    scrollChat:   function() { var el = document.getElementById('chat-messages'); if (el) el.scrollTop = el.scrollHeight; },

    capturePhoto: async function() {
        var video = document.getElementById('task-video');
        if (!video) throw new Error('Video element not found');

        // Wait up to 2s for the video stream to report dimensions
        var waited = 0;
        while ((!video.videoWidth || !video.videoHeight) && waited < 2000) {
            await new Promise(function(r) { setTimeout(r, 100); });
            waited += 100;
        }

        var w = video.videoWidth, h = video.videoHeight;
        if (!w || !h) throw new Error('Camera not ready — try again');

        var MAX = 1024;
        if (w > MAX || h > MAX) {
            var ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }
        // Brief pause for autofocus to settle
        await new Promise(function(r) { setTimeout(r, 300); });

        var canvas = document.createElement('canvas');
        canvas.width = w; canvas.height = h;
        canvas.getContext('2d').drawImage(video, 0, 0, w, h);
        return canvas.toDataURL('image/jpeg', 0.82).replace('data:image/jpeg;base64,', '');
    },

    startCamera: async function(videoId) {
        // Poll for DOM element — Blazor may not have rendered it yet
        var video = null;
        for (var attempt = 0; attempt < 30; attempt++) {
            video = document.getElementById(videoId);
            if (video) break;
            await new Promise(function(r) { setTimeout(r, 50); });
        }
        if (!video) throw new Error('Video element not found after 1.5s');

        // Set attributes before srcObject — required on iOS Safari
        video.setAttribute('playsinline', '');
        video.setAttribute('webkit-playsinline', '');
        video.setAttribute('muted', '');
        video.muted = true;

        var constraints = [
            { video: { facingMode: { ideal: 'environment' } }, audio: false },
            { video: { facingMode: 'environment' }, audio: false },
            { video: true, audio: false }
        ];

        var stream = null;
        var lastErr = null;
        for (var i = 0; i < constraints.length; i++) {
            try {
                if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
                    stream = await navigator.mediaDevices.getUserMedia(constraints[i]);
                } else {
                    var legacyGUM = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
                    stream = await new Promise(function(resolve, reject) {
                        legacyGUM.call(navigator, constraints[i], resolve, reject);
                    });
                }
                break;
            } catch(e) { lastErr = e; }
        }
        if (!stream) throw lastErr || new Error('Could not start camera');

        video.srcObject = stream;
        try { await video.play(); } catch(e) { /* autoplay may be blocked; stream still attached */ }
    },

    stopCamera: function(videoId) {
        var video = document.getElementById(videoId);
        if (video && video.srcObject) {
            video.srcObject.getTracks().forEach(function(t) { t.stop(); });
            video.srcObject = null;
        }
    },

    registerServiceWorker: async function() {
        if (!('serviceWorker' in navigator)) return;
        try { await navigator.serviceWorker.register('/sw.js'); } catch(e) {}
    },

    requestPushPermission: async function() {
        if (!('Notification' in window)) return 'not-supported';
        if (Notification.permission !== 'default') return Notification.permission;
        return await Notification.requestPermission();
    },

    subscribeForPush: async function(publicKey, familyId, memberName) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;
        try {
            var reg = await navigator.serviceWorker.ready;
            var existing = await reg.pushManager.getSubscription();
            var sub = existing || await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: familyApp._urlBase64ToUint8Array(publicKey)
            });
            var json = sub.toJSON();
            await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    familyId: familyId,
                    memberName: memberName,
                    endpoint: json.endpoint,
                    p256dh: json.keys.p256dh,
                    auth: json.keys.auth
                })
            });
        } catch(e) {}
    },

    _urlBase64ToUint8Array: function(base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var raw = atob(base64);
        var arr = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) arr[i] = raw.charCodeAt(i);
        return arr;
    }
};
