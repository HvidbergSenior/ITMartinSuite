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
                    width:  { ideal: 4096 },
                    height: { ideal: 2160 },
                    frameRate: { ideal: 30 }
                },
                audio: false
            });

            const track = this.stream.getVideoTracks()[0];
            const capabilities = track.getCapabilities?.();
            const settings = track.getSettings?.();
            console.log("TRACK CAPABILITIES", capabilities);
            console.log("TRACK SETTINGS", settings);

            if (capabilities?.torch) {
                try {
                    await track.applyConstraints({ advanced: [{ torch: true }] });
                    console.log("TORCH ON");
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
            console.log("CAMERA READY", this.video.videoWidth, "x", this.video.videoHeight);
        } catch (err) {
            console.error("CAMERA FAILED", err);
            throw err;
        }
    },

    async capture() {
        if (!this.video || !this.stream)
            throw new Error("Camera not started");

        // Brief pause for autofocus to settle
        await new Promise(x => setTimeout(x, 500));

        const canvas = document.createElement("canvas");
        canvas.width  = this.video.videoWidth;
        canvas.height = this.video.videoHeight;

        const ctx = canvas.getContext("2d");
        ctx.drawImage(this.video, 0, 0, canvas.width, canvas.height);

        console.log("CAPTURED", canvas.width, "x", canvas.height);

        return { image: canvas.toDataURL("image/jpeg", 0.95) };
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

window.barcodeScanner = {
    stream: null,
    animFrame: null,
    lastCode: null,
    lastTime: 0,

    async start(videoId, dotNetRef) {
        const video = document.getElementById(videoId);
        if (!video) return;
        try {
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: 'environment', width: { ideal: 1280 } }
            });
            video.srcObject = this.stream;
            await video.play();
            this.scan(video, dotNetRef);
        } catch (e) { console.error('barcodeScanner.start', e); }
    },

    scan(video, dotNetRef) {
        if (!('BarcodeDetector' in window)) {
            console.warn('BarcodeDetector not supported');
            return;
        }
        const detector = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'code_128', 'upc_a'] });
        const loop = async () => {
            if (!this.stream) return;
            try {
                const barcodes = await detector.detect(video);
                if (barcodes.length > 0) {
                    const code = barcodes[0].rawValue;
                    const now = Date.now();
                    if (code !== this.lastCode || now - this.lastTime > 3000) {
                        this.lastCode = code;
                        this.lastTime = now;
                        await dotNetRef.invokeMethodAsync('OnBarcodeDetected', code);
                    }
                }
            } catch {}
            this.animFrame = requestAnimationFrame(loop);
        };
        this.animFrame = requestAnimationFrame(loop);
    },

    stop() {
        if (this.animFrame) cancelAnimationFrame(this.animFrame);
        this.animFrame = null;
        if (this.stream) { this.stream.getTracks().forEach(t => t.stop()); this.stream = null; }
        this.lastCode = null;
    }
};

window.downloadFile = (filename, mimeType, base64) => {
    const a = document.createElement('a');
    a.href = `data:${mimeType};base64,${base64}`;
    a.download = filename;
    a.click();
};
