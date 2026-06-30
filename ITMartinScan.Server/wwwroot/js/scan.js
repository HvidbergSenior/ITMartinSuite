window.scan = {
    stream: null,

    async startCamera(videoId) {
        // Poll until element appears — Blazor Server can invoke JS before DOM commits
        let video = null;
        for (let i = 0; i < 30; i++) {
            video = document.getElementById(videoId);
            if (video) break;
            await new Promise(r => setTimeout(r, 50));
        }
        if (!video) { console.error('scan: video element not found'); return; }

        // Stop any existing stream before starting a new one
        if (this.stream) {
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        }

        try {
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } },
                audio: false
            });
        } catch (e) {
            console.warn('Rear camera failed, trying any camera:', e);
            try {
                this.stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
            } catch (e2) {
                console.error('No camera available:', e2);
                return;
            }
        }

        video.srcObject = this.stream;
        try { await video.play(); } catch(e) { console.warn('video.play() rejected:', e.name); }
    },

    capture(videoId) {
        const video = document.getElementById(videoId);
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth || 1280;
        canvas.height = video.videoHeight || 720;
        canvas.getContext('2d').drawImage(video, 0, 0);
        return canvas.toDataURL('image/jpeg', 0.88).split(',')[1];
    },

    stopCamera() {
        if (this.stream) {
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        }
    },

    flash() {
        var f = document.createElement('div');
        f.style.cssText = 'position:fixed;inset:0;background:white;pointer-events:none;z-index:99;opacity:0.7;transition:opacity .3s ease-out;';
        document.body.appendChild(f);
        requestAnimationFrame(function() {
            requestAnimationFrame(function() { f.style.opacity = '0'; });
        });
        setTimeout(function() { if (f.parentNode) f.remove(); }, 350);
    },

    setZoom(wrapperId, scale) {
        var el = document.getElementById(wrapperId);
        if (el) el.style.transform = 'scale(' + scale + ')';
    },

    freezeFrame(videoId, canvasId) {
        var video = document.getElementById(videoId);
        var canvas = document.getElementById(canvasId);
        if (!video || !canvas) return;
        canvas.width = video.videoWidth || video.offsetWidth;
        canvas.height = video.videoHeight || video.offsetHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
        video.style.display = 'none';
        canvas.style.display = 'block';
    },

    resumeCamera(videoId, canvasId) {
        var video = document.getElementById(videoId);
        var canvas = document.getElementById(canvasId);
        if (canvas) canvas.style.display = 'none';
        if (video) video.style.display = 'block';
    }
};
