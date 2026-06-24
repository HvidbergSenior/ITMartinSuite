window.webcam = {

    video: null,
    stream: null,

    async start() {
        try {
            this.video = document.getElementById("video");

            if (!this.video)
                throw new Error("VIDEO ELEMENT NOT FOUND");

            this.stop();

            var constraintSets = [
                { video: { facingMode: { ideal: "environment" }, width: { ideal: 2560 }, height: { ideal: 1440 } }, audio: false },
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

            var track = this.stream.getVideoTracks()[0];
            if (track) {
                var capabilities = track.getCapabilities ? track.getCapabilities() : null;
                if (capabilities && capabilities.torch) {
                    try { await track.applyConstraints({ advanced: [{ torch: true }] }); } catch (e) {}
                }
                if (capabilities && capabilities.focusMode) {
                    try { await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] }); } catch (e) {}
                }
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
            console.log("CAMERA READY", this.video.videoWidth, this.video.videoHeight);
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

        await new Promise(x => setTimeout(x, 300));

        const videoRect = this.video.getBoundingClientRect();
        const guide = document.querySelector(".scanner-guide");
        const guideRect = guide.getBoundingClientRect();

        const scaleX = this.video.videoWidth / videoRect.width;
        const scaleY = this.video.videoHeight / videoRect.height;

        const cropX = Math.round((guideRect.left - videoRect.left) * scaleX);
        const cropY = Math.round((guideRect.top - videoRect.top) * scaleY);
        const cropW = Math.round(guideRect.width * scaleX);
        const cropH = Math.round(guideRect.height * scaleY);

        // Cap crop at 1280px longest side to keep payload manageable
        const MAX = 1280;
        let dw = cropW, dh = cropH;
        if (dw > MAX || dh > MAX) {
            const ratio = Math.min(MAX / dw, MAX / dh);
            dw = Math.round(dw * ratio);
            dh = Math.round(dh * ratio);
        }

        const canvas = document.createElement("canvas");
        canvas.width  = dw;
        canvas.height = dh;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(this.video, cropX, cropY, cropW, cropH, 0, 0, dw, dh);

        console.log("CAPTURED", dw, dh);

        return { image: canvas.toDataURL("image/jpeg", 0.88) };
    },

    stop() {
        try {
            if (!this.stream) return;
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
            console.log("CAMERA STOPPED");
        } catch (e) {
            console.error("STOP CAMERA FAILED", e);
        }
    }
};
