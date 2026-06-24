window.finditCamera = {

    video: null,
    stream: null,

    async start() {
        try {
            this.video = document.getElementById("video");

            if (!this.video)
                throw new Error("VIDEO ELEMENT NOT FOUND");

            this.stop();

            var constraintSets = [
                { video: { facingMode: { ideal: "environment" }, width: { ideal: 1280, max: 1920 }, height: { ideal: 960, max: 1440 } }, audio: false },
                { video: { facingMode: "environment" }, audio: false },
                { video: true, audio: false }
            ];

            this.stream = null;
            var lastError = null;
            for (var i = 0; i < constraintSets.length; i++) {
                try {
                    this.stream = await this._getUserMedia(constraintSets[i]);
                    break;
                } catch (e) {
                    lastError = e;
                    console.warn("Camera attempt " + i + " failed", e.name, e.message);
                }
            }
            if (!this.stream) throw lastError || new Error("Could not access camera");

            // Force minimum zoom (wide angle) if supported
            var track = this.stream.getVideoTracks()[0];
            if (track) {
                try { await track.applyConstraints({ advanced: [{ zoom: 1 }] }); } catch (e) {}
            }

            this.video.srcObject = this.stream;
            this.video.autoplay = true;
            this.video.muted = true;
            this.video.playsInline = true;
            this.video.setAttribute("autoplay", "");
            this.video.setAttribute("muted", "");
            this.video.setAttribute("playsinline", "");
            this.video.setAttribute("webkit-playsinline", "");

            await this.video.play();
        } catch (err) {
            console.error("CAMERA FAILED", err.name, err.message);
            throw err;
        }
    },

    _getUserMedia: function(constraints) {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            return navigator.mediaDevices.getUserMedia(constraints);
        }
        var legacyGUM = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
        if (legacyGUM) {
            return new Promise(function(resolve, reject) {
                legacyGUM.call(navigator, constraints, resolve, reject);
            });
        }
        return Promise.reject(new Error("getUserMedia not supported on this device"));
    },

    async capture() {
        if (!this.video || !this.stream)
            throw new Error("Camera not started");

        await new Promise(function(x) { setTimeout(x, 300); });

        var w = this.video.videoWidth;
        var h = this.video.videoHeight;

        if (!w || !h)
            throw new Error("Video not ready");

        var MAX = 1024;
        if (w > MAX || h > MAX) {
            var ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }

        var canvas = document.createElement("canvas");
        canvas.width  = w;
        canvas.height = h;
        canvas.getContext("2d").drawImage(this.video, 0, 0, w, h);

        return { image: canvas.toDataURL("image/jpeg", 0.82) };
    },

    stop: function() {
        try {
            if (!this.stream) return;
            this.stream.getTracks().forEach(function(t) { t.stop(); });
            this.stream = null;
        } catch (e) {}
    }
};
