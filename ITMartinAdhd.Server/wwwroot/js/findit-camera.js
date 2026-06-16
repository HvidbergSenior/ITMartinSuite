window.finditCamera = {

    video: null,
    stream: null,

    async start() {
        try {
            this.video = document.getElementById("video");

            if (!this.video)
                throw new Error("VIDEO ELEMENT NOT FOUND");

            this.stop();

            this.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" } },
                audio: false
            });

            // Force minimum zoom (wide angle) if supported
            const track = this.stream.getVideoTracks()[0];
            try {
                await track.applyConstraints({ advanced: [{ zoom: 1 }] });
            } catch {}

            this.video.srcObject = this.stream;
            this.video.autoplay = true;
            this.video.muted = true;
            this.video.playsInline = true;

            await this.video.play();
        } catch (err) {
            console.error("CAMERA FAILED", err);
            throw err;
        }
    },

    async capture() {
        if (!this.video || !this.stream)
            throw new Error("Camera not started");

        await new Promise(x => setTimeout(x, 300));

        const canvas = document.createElement("canvas");
        canvas.width  = this.video.videoWidth;
        canvas.height = this.video.videoHeight;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(this.video, 0, 0, canvas.width, canvas.height);

        return { image: canvas.toDataURL("image/jpeg", 0.92) };
    },

    stop() {
        try {
            if (!this.stream) return;
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        } catch (e) {}
    }
};
