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
                video: {
                    facingMode: { ideal: "environment" },
                    width:  { ideal: 1280, max: 1920 },
                    height: { ideal: 960,  max: 1440 }
                },
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

        let w = this.video.videoWidth;
        let h = this.video.videoHeight;

        if (!w || !h)
            throw new Error("Video not ready");

        // Limit to 1024px on the longest side to keep payload small
        const MAX = 1024;
        if (w > MAX || h > MAX) {
            const ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }

        const canvas = document.createElement("canvas");
        canvas.width  = w;
        canvas.height = h;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(this.video, 0, 0, w, h);

        return { image: canvas.toDataURL("image/jpeg", 0.82) };
    },

    stop() {
        try {
            if (!this.stream) return;
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        } catch (e) {}
    }
};
