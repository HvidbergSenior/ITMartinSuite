window.webcam = {

    video: null,
    stream: null,
    isStarting: false,

    // =========================================
    // START CAMERA
    // =========================================

    async start() {

        try {

            this.video =
                document.getElementById("video");
            
            if (!this.video) {

                throw new Error(
                    "VIDEO ELEMENT NOT FOUND");
            }

            this.stop();

            this.stream =
                await navigator.mediaDevices
                    .getUserMedia({

                        video: {
                            facingMode: {
                                ideal: "environment"
                            },
                            width: {
                                ideal: 2560
                            },
                            height: {
                                ideal: 1440
                            }
                        },

                        audio: false
                    });
            const track =
                this.stream
                    .getVideoTracks()[0];

            const capabilities =
                track.getCapabilities?.();

            if (capabilities?.torch)
            {
                try
                {
                    await track.applyConstraints({
                        advanced: [
                            {
                                torch: true
                            }
                        ]
                    });
                }
                catch
                {
                }
            }
            if (capabilities?.focusMode)
            {
                try
                {
                    await track.applyConstraints({
                        advanced: [
                            {
                                focusMode: "continuous"
                            }
                        ]
                    });
                }
                catch
                {
                }
            }

            console.log(
                "TRACK SETTINGS",
                track.getSettings());
            this.video.srcObject =
                this.stream;

            this.video.autoplay = true;
            this.video.muted = true;
            this.video.playsInline = true;

            await this.video.play();
            console.log(
                "VIDEO SIZE",
                this.video.videoWidth,
                this.video.videoHeight);
            console.log(
                "CAMERA READY");
            
        }
        catch (err) {

            console.error(
                "CAMERA FAILED",
                err);

            throw err;
        }
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