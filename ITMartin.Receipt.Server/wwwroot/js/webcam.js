window.webcam = {

    video: null,
    stream: null,

    async start() {
        try {
            this.video = document.getElementById("video");

            if (!this.video)
                throw new Error("VIDEO ELEMENT NOT FOUND");

            this.stop();

            this.stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: { ideal: "environment" },
                    width: { ideal: 2560 },
                    height: { ideal: 1440 }
                },
                audio: false
            });

            const track = this.stream.getVideoTracks()[0];
            const capabilities = track.getCapabilities?.();

            if (capabilities?.torch) {
                try {
                    await track.applyConstraints({ advanced: [{ torch: true }] });
                } catch {}
            }

            if (capabilities?.focusMode) {
                try {
                    await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] });
                } catch {}
            }

            this.video.srcObject = this.stream;
            this.video.autoplay = true;
            this.video.muted = true;
            this.video.playsInline = true;

            await this.video.play();
            console.log("CAMERA READY", this.video.videoWidth, this.video.videoHeight);
        } catch (err) {
            console.error("CAMERA FAILED", err);
            throw err;
        }
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

        const canvas = document.createElement("canvas");
        canvas.width = cropW;
        canvas.height = cropH;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(this.video, cropX, cropY, cropW, cropH, 0, 0, cropW, cropH);

        console.log("CAPTURED", cropW, cropH);

        return { image: canvas.toDataURL("image/jpeg", 0.92) };
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
