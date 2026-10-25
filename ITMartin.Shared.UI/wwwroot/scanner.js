// Camera barcode reader (copied from Bibliotek, 2026-10-05). Uses the browser's own
// BarcodeDetector (Android Chrome) and falls back to ZXing loaded on
// demand elsewhere (iOS Safari, desktop). Reports the first EAN/UPC seen to OnBarcode -
// or, with { qr: true, videoId }, the first QR code to OnQr (Vareinfo: scan the box label, 2026-10-21).
window.scanner = {
    video: null,
    stream: null,
    running: false,
    zxingReader: null,

    async start(dotnetRef, opts) {
        this.stop();
        this.running = true;
        this.qr = !!(opts && opts.qr);
        var videoId = (opts && opts.videoId) || "scan-video";

        for (var attempt = 0; attempt < 30 && !document.getElementById(videoId); attempt++)
            await new Promise(function (r) { setTimeout(r, 50); });
        this.video = document.getElementById(videoId);
        if (!this.video) throw new Error("Video element not found");

        var constraintSets = [
            { video: { facingMode: { ideal: "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false },
            { video: { facingMode: "environment" }, audio: false },
            { video: true, audio: false }
        ];
        var lastError = null;
        for (var i = 0; i < constraintSets.length && !this.stream; i++) {
            try { this.stream = await navigator.mediaDevices.getUserMedia(constraintSets[i]); }
            catch (e) { lastError = e; }
        }
        if (!this.stream) throw lastError || new Error("Ingen adgang til kameraet");

        // iPhone Safari plays a camera without a tap only when the muted PROPERTY is set (2026-10-25).
        this.video.muted = true; this.video.autoplay = true; this.video.playsInline = true;
        this.video.setAttribute("muted", "");
        this.video.setAttribute("playsinline", "true");
        this.video.srcObject = this.stream;
        await this.video.play();

        var track = this.stream.getVideoTracks()[0];
        var caps = track && track.getCapabilities ? track.getCapabilities() : null;
        if (caps && caps.focusMode) { try { await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] }); } catch (e) { } }

        if ("BarcodeDetector" in window) {
            this._nativeLoop(dotnetRef);
        } else {
            await this._zxing(dotnetRef);
        }
    },

    async _nativeLoop(dotnetRef) {
        var detector = new BarcodeDetector({ formats: this.qr ? ["qr_code"] : ["ean_13", "ean_8", "upc_a", "upc_e"] });
        var method = this.qr ? "OnQr" : "OnBarcode";
        var self = this;
        var tick = async function () {
            if (!self.running) return;
            try {
                if (self.video.readyState >= 2) {
                    var codes = await detector.detect(self.video);
                    if (codes.length > 0) {
                        self.stop();
                        await dotnetRef.invokeMethodAsync(method, codes[0].rawValue);
                        return;
                    }
                }
            } catch (e) { console.warn("detect failed", e); }
            setTimeout(tick, 150);
        };
        tick();
    },

    async _zxing(dotnetRef) {
        if (!window.ZXing) {
            await new Promise(function (resolve, reject) {
                var s = document.createElement("script");
                s.src = "https://cdn.jsdelivr.net/npm/@zxing/library@0.21.3/umd/index.min.js";
                s.onload = resolve; s.onerror = reject;
                document.head.appendChild(s);
            });
        }
        var hints = new Map();
        hints.set(ZXing.DecodeHintType.POSSIBLE_FORMATS, this.qr ? [ZXing.BarcodeFormat.QR_CODE] : [
            ZXing.BarcodeFormat.EAN_13, ZXing.BarcodeFormat.EAN_8, ZXing.BarcodeFormat.UPC_A, ZXing.BarcodeFormat.UPC_E]);
        var method = this.qr ? "OnQr" : "OnBarcode";
        this.zxingReader = new ZXing.BrowserMultiFormatReader(hints, 200);
        var self = this;
        this.zxingReader.decodeFromStream(this.stream, this.video, function (result, err) {
            if (result && self.running) {
                self.stop();
                dotnetRef.invokeMethodAsync(method, result.getText());
            }
        });
    },

    stop() {
        this.running = false;
        if (this.zxingReader) { try { this.zxingReader.reset(); } catch (e) { } this.zxingReader = null; }
        if (this.stream) { this.stream.getTracks().forEach(function (t) { t.stop(); }); this.stream = null; }
        if (this.video) { this.video.srcObject = null; }
    },

    beep() {
        try {
            var ctx = new (window.AudioContext || window.webkitAudioContext)();
            var o = ctx.createOscillator(); var g = ctx.createGain();
            o.frequency.value = 1200; o.connect(g); g.connect(ctx.destination);
            g.gain.value = 0.1; o.start(); o.stop(ctx.currentTime + 0.12);
        } catch (e) { }
    }
};
