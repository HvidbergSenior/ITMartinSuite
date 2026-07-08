window.webcam = {

    video: null,
    stream: null,
    isStarting: false,

    // =========================================
    // START CAMERA
    // =========================================

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
                console.log("TRACK SETTINGS", track.getSettings ? track.getSettings() : null);
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

    // =========================================
    // CAPTURE
    // =========================================

    async capture() {

       
        if (!this.video ||
            !this.stream) {

            throw new Error(
                "Camera not started");
        }

        // =====================================
        // CANVAS
        // =====================================

        const width =
            this.video.videoWidth;

        const height =
            this.video.videoHeight;

// =====================================
// GUIDE RECTANGLE
// =====================================

        const videoRect =
            this.video.getBoundingClientRect();

        const guide =
            document.querySelector(".scanner-guide");

        const guideRect =
            guide.getBoundingClientRect();

        const scaleX =
            this.video.videoWidth /
            videoRect.width;

        const scaleY =
            this.video.videoHeight /
            videoRect.height;

        const guideX =
            Math.round(
                (guideRect.left - videoRect.left) *
                scaleX);

        const guideY =
            Math.round(
                (guideRect.top - videoRect.top) *
                scaleY);

        let guideWidth =
            Math.round(
                guideRect.width *
                scaleX);

        let guideHeight =
            Math.round(
                guideRect.height *
                scaleY);

        // Safety net: on some mobile browsers (notably iOS Safari) video.videoWidth/
        // videoHeight can still be 0 right after autoplay starts, which collapses the
        // whole crop to 0x0 and silently produces a blank image every time. Fall back
        // to capturing the full video frame rather than an unusable empty crop.
        let guideXSafe = guideX;
        let guideYSafe = guideY;
        if (!width || !height || !guideWidth || !guideHeight ||
            !isFinite(guideWidth) || !isFinite(guideHeight)) {
            console.warn("webcam.capture: invalid guide crop, falling back to full frame", { width, height, guideWidth, guideHeight });
            guideXSafe = 0;
            guideYSafe = 0;
            guideWidth = width || this.video.videoWidth || 1280;
            guideHeight = height || this.video.videoHeight || 720;
        }
// =====================================
// CROP CARD
// =====================================

        const cropCanvas =
            document.createElement("canvas");

        const cropCtx =
            cropCanvas.getContext("2d");

        cropCanvas.width =
            Math.round(guideWidth);

        cropCanvas.height =
            Math.round(guideHeight);

        cropCtx.drawImage(
            this.video,

            guideXSafe,
            guideYSafe,
            guideWidth,
            guideHeight,

            0,
            0,
            guideWidth,
            guideHeight);

        // =====================================
// UPSCALE FOR OCR
// =====================================

        // Upscaling doesn't add real detail (just interpolated pixels) and was costing
        // several seconds of canvas work on mobile for no recognition benefit - keep it
        // minimal, only for genuinely small crops.
        const scale = guideWidth > 500 ? 1 : 2;

        const canvas =
            document.createElement("canvas");

        const ctx =
            canvas.getContext("2d");

        canvas.width =
            guideWidth * scale;

        canvas.height =
            guideHeight * scale;

        ctx.imageSmoothingEnabled =
            true;

        ctx.imageSmoothingQuality =
            "high";

        ctx.drawImage(
            cropCanvas,
            0,
            0,
            canvas.width,
            canvas.height);
        // =====================================
        // RETURN JPEG
        // =====================================
        return {

            image:
                canvas.toDataURL(
                    "image/jpeg",
                    0.88)
        };
    },

    // =========================================
    // STOP CAMERA
    // =========================================

    stop() {

        try {

            if (!this.stream) {
                return;
            }

            this.stream
                .getTracks()
                .forEach(x => x.stop());

            this.stream = null;

            console.log(
                "CAMERA STOPPED");
        }
        catch (e) {

            console.error(
                "STOP CAMERA FAILED",
                e);
        }
    },
    async scan() {

        const capture =
            await this.capture();

        const response =
            await fetch(
                "/api/magic/scan-capture",
                {
                    method: "POST",
                    headers: {
                        "Content-Type":
                            "application/json"
                    },
                    body: JSON.stringify({
                        image: capture.image
                    })
                });

        return await response.json();
    }
};

window.downloadFile = (filename, mimeType, base64) => {
    const a = document.createElement('a');
    a.href = `data:${mimeType};base64,${base64}`;
    a.download = filename;
    a.click();
};