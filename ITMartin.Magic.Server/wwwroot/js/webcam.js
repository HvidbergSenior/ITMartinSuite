window.webcam = {

    video: null,
    stream: null,

    // =========================================
    // START CAMERA
    // =========================================

    async start() {

        this.video =
            document.getElementById("video");

        if (this.stream)
        {
            this.stop();
        }

        // =====================================
        // GET CAMERA
        // =====================================

        this.stream =
            await navigator.mediaDevices
                .getUserMedia({

                    video: {

                        facingMode: {
                            ideal: "environment"
                        },

                        width: {
                            ideal: 1920
                        },

                        height: {
                            ideal: 1080
                        },

                        focusMode: "continuous",

                        aspectRatio: {
                            ideal: 1.7777777778
                        }
                    },

                    audio: false
                });

        // =====================================
        // VIDEO ELEMENT
        // =====================================

        this.video.srcObject =
            this.stream;

        this.video.setAttribute(
            "playsinline",
            true);

        this.video.setAttribute(
            "webkit-playsinline",
            true);

        this.video.muted = true;

        await this.video.play();

        // =====================================
        // CAMERA INFO
        // =====================================

        const track =
            this.stream.getVideoTracks()[0];

        const settings =
            track.getSettings();

        console.log(
            "CAMERA SETTINGS:",
            settings);

        try
        {
            const capabilities =
                track.getCapabilities();

            console.log(
                "CAMERA CAPABILITIES:",
                capabilities);
        }
        catch
        {
            console.log(
                "Capabilities not supported");
        }

        console.log(
            "VIDEO SIZE:",
            this.video.videoWidth,
            this.video.videoHeight);

        // =====================================
        // AUTOFOCUS SETTLE
        // =====================================

        await new Promise(x =>
            setTimeout(x, 1200));

        console.log("CAMERA READY");

        // =====================================
        // TAP TO FOCUS
        // =====================================

        this.video.onclick =
            async () =>
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

                    console.log("FOCUS TRIGGERED");
                }
                catch (e)
                {
                    console.log(
                        "Focus trigger unsupported",
                        e);
                }
            };
    },

    // =========================================
    // CAPTURE
    // =========================================

    async capture() {

        // =====================================
        // WAIT FOR FOCUS
        // =====================================

        await new Promise(x =>
            setTimeout(x, 400));

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
        // DRAW FULL FRAME
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

        if (old)
        {
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
        // JPEG EXPORT
        // =====================================

        return {

            image:
                canvas.toDataURL(
                    "image/jpeg",
                    0.92)
        };
    },

    // =========================================
    // STOP
    // =========================================

    stop() {

        if (!this.stream)
        {
            return;
        }

        this.stream
            .getTracks()
            .forEach(x => x.stop());

        this.stream = null;

        console.log("CAMERA STOPPED");
    }
};