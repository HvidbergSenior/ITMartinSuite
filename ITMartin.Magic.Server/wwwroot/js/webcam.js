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
        // AUTOFOCUS WAIT
        // =====================================

        await new Promise(x =>
            setTimeout(x, 300));

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

        const guideWidth =
            Math.round(
                guideRect.width *
                scaleX);

        const guideHeight =
            Math.round(
                guideRect.height *
                scaleY);
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

            guideX,
            guideY,
            guideWidth,
            guideHeight,

            0,
            0,
            guideWidth,
            guideHeight);

        // =====================================
// UPSCALE FOR OCR
// =====================================

        const scale = 4;

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
        // DEBUG PREVIEW
        // =====================================

        const old =
            document.getElementById(
                "debug-canvas");

        if (old) {
            old.remove();
        }

        canvas.id =
            "debug-canvas";

        canvas.style.position =
            "fixed";

        canvas.style.right =
            "12px";

        canvas.style.bottom =
            "12px";

        canvas.style.width =
            "140px";

        canvas.style.border =
            "3px solid lime";

        canvas.style.borderRadius =
            "12px";

        canvas.style.zIndex =
            "999999";
        console.log(
            "GUIDE",
            guideX,
            guideY,
            guideWidth,
            guideHeight);

        console.log(
            "UPSCALED",
            canvas.width,
            canvas.height);
        document.body.appendChild(
            canvas);

        console.log(
            "CAPTURED:",
            width,
            height);

        // =====================================
        // RETURN JPEG
        // =====================================
        console.log(
            "VIDEO ELEMENT:",
            this.video.videoWidth,
            this.video.videoHeight);
        return {

            image:
                canvas.toDataURL(
                    "image/jpeg",
                    1.0)
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