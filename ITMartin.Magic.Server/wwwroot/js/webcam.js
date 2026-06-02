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
                            }
                        },

                        audio: false
                    });
            const track =
                this.stream
                    .getVideoTracks()[0];

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

        const canvas =
            document.createElement("canvas");

        const ctx =
            canvas.getContext("2d");

        const width =
            this.video.videoWidth;

        const height =
            this.video.videoHeight;

        canvas.width = width;
        canvas.height = height;

        // =====================================
        // DRAW FRAME
        // =====================================

        ctx.drawImage(
            this.video,
            0,
            0,
            width,
            height);

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
                    0.98)
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