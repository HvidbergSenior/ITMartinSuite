window.webcam = {

    video: null,
    stream: null,

    async start() {

        this.video =
            document.getElementById("video");

        if (this.stream)
        {
            this.stop();
        }

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
                        }
                    },

                    audio: false
                });

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

        console.log(
            "VIDEO:",
            this.video.videoWidth,
            this.video.videoHeight);

        await new Promise(x =>
            setTimeout(x, 1200));
    },

    async capture() {

        await new Promise(x =>
            setTimeout(x, 250));

        const frame =
            this.captureFrame();

        return {

            image:
                frame.canvas.toDataURL(
                    "image/jpeg",
                    1.0)
        };
    },

    captureFrame() {

        const canvas =
            document.createElement(
                "canvas");

        const ctx =
            canvas.getContext("2d");

        canvas.width = 1200;
        canvas.height = 1680;

        const guide =
            document.querySelector(
                ".scanner-guide");

        const videoRect =
            this.video.getBoundingClientRect();

        const guideRect =
            guide.getBoundingClientRect();

        const realWidth =
            this.video.videoWidth;

        const realHeight =
            this.video.videoHeight;

        // =====================================
        // REAL RENDERED VIDEO AREA
        // =====================================

        const videoAspect =
            realWidth / realHeight;

        const elementAspect =
            videoRect.width / videoRect.height;

        let renderedWidth;
        let renderedHeight;

        let offsetX = 0;
        let offsetY = 0;

        if (videoAspect > elementAspect)
        {
            renderedWidth =
                videoRect.width;

            renderedHeight =
                renderedWidth / videoAspect;

            offsetY =
                (videoRect.height - renderedHeight) / 2;
        }
        else
        {
            renderedHeight =
                videoRect.height;

            renderedWidth =
                renderedHeight * videoAspect;

            offsetX =
                (videoRect.width - renderedWidth) / 2;
        }

        // =====================================
        // SCALE
        // =====================================

        const scaleX =
            realWidth / renderedWidth;

        const scaleY =
            realHeight / renderedHeight;

        // =====================================
        // GUIDE POSITION
        // =====================================

        const relativeX =
            guideRect.left -
            videoRect.left -
            offsetX;

        const relativeY =
            guideRect.top -
            videoRect.top -
            offsetY;

        const sx =
            relativeX * scaleX;

        const sy =
            relativeY * scaleY;

        const sw =
            guideRect.width * scaleX;

        const sh =
            guideRect.height * scaleY;

        console.log({
            realWidth,
            realHeight,
            renderedWidth,
            renderedHeight,
            sx,
            sy,
            sw,
            sh
        });

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

        // =====================================
        // DEBUG BORDER
        // =====================================

        ctx.strokeStyle =
            "#00ff99";

        ctx.lineWidth = 10;

        ctx.strokeRect(
            0,
            0,
            canvas.width,
            canvas.height);

        return {
            canvas,
            ctx
        };
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