// =========================================
// FILE:
// wwwroot/js/webcam.js
// STEP 2 DEBUG
// =========================================

window.webcam = {

    video: null,
    stream: null,

    // =========================================
// FILE:
// wwwroot/js/webcam.js
// REPLACE regions
// =========================================

    regions: {

        title: {
            x: 0.05,
            y: 0.02,
            w: 0.85,
            h: 0.10,
            color: "#ff3b3b",
            label: "TITLE OCR"
        },

        bottom: {
            x: 0.08,
            y: 0.78,
            w: 0.78,
            h: 0.10,
            color: "#00e5ff",
            label: "BOTTOM TEXT OCR"
        },

        pt: {
            x: 0.82,
            y: 0.83,
            w: 0.12,
            h: 0.08,
            color: "#00ff44",
            label: "P/T OCR"
        }
    },

    async start() {

        this.video =
            document.getElementById("video");

        await navigator.mediaDevices.getUserMedia({
            video: true
        });

        const devices =
            await navigator.mediaDevices
                .enumerateDevices();

        const videoDevices =
            devices.filter(x =>
                x.kind === "videoinput");

        let selected =
            videoDevices.find(x =>
                x.label.toLowerCase().includes("logitech") ||
                x.label.toLowerCase().includes("brio") ||
                x.label.toLowerCase().includes("usb"));

        if (!selected)
        {
            selected = videoDevices[0];
        }

        console.log(
            "SELECTED CAMERA:",
            selected);

        this.stream =
            await navigator.mediaDevices
                .getUserMedia({

                    video: {

                        deviceId: {
                            exact: selected.deviceId
                        },

                        width: {
                            ideal: 1920
                        },

                        height: {
                            ideal: 1080
                        },

                        frameRate: {
                            ideal: 30
                        }
                    },

                    audio: false
                });

        this.video.srcObject =
            this.stream;

        this.video.setAttribute(
            "playsinline",
            true);

        await this.video.play();

        await new Promise(x =>
            setTimeout(x, 1200));
    },

    async capture() {

        const frame =
            this.captureFrame();

        const cardCanvas =
            frame.canvas;

        // =====================================
        // DEBUG
        // =====================================

        console.log(
            "CARD CANVAS SIZE:",
            cardCanvas.width,
            cardCanvas.height);

        const testCtx =
            cardCanvas.getContext("2d");

        const sample =
            testCtx.getImageData(
                0,
                0,
                10,
                10);

        console.log(
            "FIRST PIXEL:",
            sample.data[0],
            sample.data[1],
            sample.data[2]);

        // =====================================
        // SHOW CANVAS
        // =====================================

        const old =
            document.getElementById(
                "debug-canvas");

        if (old)
        {
            old.remove();
        }

        cardCanvas.id =
            "debug-canvas";

        cardCanvas.style.position =
            "fixed";

        cardCanvas.style.right =
            "10px";

        cardCanvas.style.bottom =
            "10px";

        cardCanvas.style.width =
            "240px";

        cardCanvas.style.border =
            "3px solid red";

        cardCanvas.style.zIndex =
            "999999";

        document.body.appendChild(
            cardCanvas);

        // =====================================
        // FINGERPRINT
        // =====================================

        const fingerprint =
            this.buildFingerprint(
                cardCanvas);

        return {

            image:
                cardCanvas.toDataURL(
                    "image/jpeg",
                    0.96),

            title:
                this.extractRegion(
                    cardCanvas,
                    this.regions.title),

            bottom:
                this.extractRegion(
                    cardCanvas,
                    this.regions.bottom),

            pt:
                this.extractRegion(
                    cardCanvas,
                    this.regions.pt),

            fingerprint
        };
    },

    captureFrame() {

        const canvas =
            document.createElement(
                "canvas");

        const ctx =
            canvas.getContext("2d");

        // FINAL NORMALIZED CARD SIZE

        canvas.width = 1200;
        canvas.height = 1680;

        // =====================================
        // GUIDE RECT
        // =====================================

        const overlay =
            document.querySelector(
                ".scanner-guide");

        const videoRect =
            this.video.getBoundingClientRect();

        const guideRect =
            overlay.getBoundingClientRect();

        // =====================================
        // REAL VIDEO SIZE
        // =====================================

        const realWidth =
            this.video.videoWidth;

        const realHeight =
            this.video.videoHeight;

        // =====================================
        // DISPLAYED VIDEO SIZE
        // =====================================

        const displayedWidth =
            videoRect.width;

        const displayedHeight =
            videoRect.height;

        // =====================================
        // SCALE
        // =====================================

        const scaleX =
            realWidth / displayedWidth;

        const scaleY =
            realHeight / displayedHeight;

        // =====================================
        // RELATIVE GUIDE POSITION
        // =====================================

        const relativeX =
            guideRect.left - videoRect.left;

        const relativeY =
            guideRect.top - videoRect.top;

        // =====================================
        // SOURCE RECT
        // =====================================

        const sx =
            relativeX * scaleX;

        const sy =
            relativeY * scaleY;

        const sw =
            guideRect.width * scaleX;

        const sh =
            guideRect.height * scaleY;

        console.log("VIDEO RECT:", videoRect);
        console.log("GUIDE RECT:", guideRect);

        console.log(
            "SOURCE:",
            sx,
            sy,
            sw,
            sh);

        // =====================================
        // DRAW
        // =====================================

        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = "high";

        ctx.drawImage(
            this.video,
            sx,
            sy,
            sw,
            sh,
            0,
            0,
            canvas.width,
            canvas.height);

        this.drawDebugRegions(
            ctx,
            canvas);

        return {
            canvas,
            ctx
        };
    },

    extractRegion(
        sourceCanvas,
        region)
    {
        const cropCanvas =
            document.createElement(
                "canvas");

        const ctx =
            cropCanvas.getContext("2d");

        const sx =
            sourceCanvas.width * region.x;

        const sy =
            sourceCanvas.height * region.y;

        const sw =
            sourceCanvas.width * region.w;

        const sh =
            sourceCanvas.height * region.h;

        console.log(
            "CROP:",
            sx,
            sy,
            sw,
            sh);

        cropCanvas.width = sw;
        cropCanvas.height = sh;

        ctx.drawImage(
            sourceCanvas,
            sx,
            sy,
            sw,
            sh,
            0,
            0,
            sw,
            sh);

        return cropCanvas.toDataURL(
            "image/jpeg",
            0.96);
    },

    // =========================================
// FILE:
// wwwroot/js/webcam.js
// REPLACE drawDebugRegions
// =========================================

    drawDebugRegions(
        ctx,
        canvas)
    {
        Object.values(this.regions)
            .forEach(region => {

                const x =
                    canvas.width * region.x;

                const y =
                    canvas.height * region.y;

                const w =
                    canvas.width * region.w;

                const h =
                    canvas.height * region.h;

                // =================================
                // OUTER GLOW
                // =================================

                ctx.shadowColor =
                    region.color;

                ctx.shadowBlur = 25;

                // =================================
                // BORDER
                // =================================

                ctx.strokeStyle =
                    region.color;

                ctx.lineWidth = 6;

                ctx.strokeRect(
                    x,
                    y,
                    w,
                    h);

                // =================================
                // LABEL BG
                // =================================

                ctx.shadowBlur = 0;

                ctx.fillStyle =
                    "rgba(0,0,0,.75)";

                ctx.fillRect(
                    x,
                    y - 36,
                    180,
                    30);

                // =================================
                // LABEL
                // =================================

                ctx.fillStyle =
                    region.color;

                ctx.font =
                    "bold 22px Arial";

                ctx.fillText(
                    region.label,
                    x + 10,
                    y - 12);
            });
    },

    buildFingerprint(canvas) {

        console.log(
            "WHITE BORDER DETECT");

        console.log(
            "CANVAS:",
            canvas.width,
            canvas.height);

        return {

            whiteBorder:
                this.detectWhiteBorder(
                    canvas),

            oldFrame:
                this.detectOldFrame(
                    canvas),

            borderBrightness:
                this.measureBorderBrightness(
                    canvas)
        };
    },

    detectWhiteBorder(canvas) {

        const ctx =
            canvas.getContext("2d");

        const image =
            ctx.getImageData(
                0,
                0,
                40,
                canvas.height);

        let bright = 0;
        let total = 0;

        for (let i = 0; i < image.data.length; i += 4)
        {
            const avg =
                (
                    image.data[i] +
                    image.data[i + 1] +
                    image.data[i + 2]
                ) / 3;

            if (avg > 190)
            {
                bright++;
            }

            total++;
        }

        const result =
            bright / total > 0.60;

        console.log(
            "WHITE BORDER RESULT:",
            result);

        return result;
    },

    detectOldFrame(canvas) {

        return true;
    },

    measureBorderBrightness(canvas) {

        const ctx =
            canvas.getContext("2d");

        const image =
            ctx.getImageData(
                0,
                0,
                40,
                canvas.height);

        let total = 0;

        for (let i = 0; i < image.data.length; i += 4)
        {
            total +=
                (
                    image.data[i] +
                    image.data[i + 1] +
                    image.data[i + 2]
                ) / 3;
        }

        const brightness =
            total /
            (image.data.length / 4);

        console.log(
            "BORDER BRIGHTNESS:",
            brightness);

        return brightness;
    },

    stop() {

        if (!this.stream)
        {
            return;
        }

        this.stream
            .getTracks()
            .forEach(x => x.stop());

        this.stream = null;
    }
};