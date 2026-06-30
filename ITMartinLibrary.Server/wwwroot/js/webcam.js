window.webcam = {

    video: null,
    stream: null,
    _cssZoom: 1,
    _hwZoom: false,

    async start() {
        try {
            // Poll until video element appears — Blazor Server can invoke JS before DOM commits
            this.video = null;
            for (var attempt = 0; attempt < 30; attempt++) {
                this.video = document.getElementById("video");
                if (this.video) break;
                await new Promise(function(r) { setTimeout(r, 50); });
            }
            if (!this.video)
                throw new Error("Video element not found after 1.5s");

            this.stop();

            // Try progressively simpler constraints — old devices reject high-res or strict facingMode
            var constraintSets = [
                { video: { facingMode: { ideal: "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false },
                { video: { facingMode: "environment", width: { ideal: 1280 } }, audio: false },
                { video: { facingMode: "environment" }, audio: false },
                { video: true, audio: false }
            ];

            this.stream = null;
            var lastError = null;
            for (var i = 0; i < constraintSets.length; i++) {
                try {
                    this.stream = await this._getUserMedia(constraintSets[i]);
                    break;
                } catch (e) {
                    lastError = e;
                    console.warn("Camera attempt " + i + " failed, trying simpler constraints", e.name, e.message);
                }
            }
            if (!this.stream) throw lastError || new Error("Could not access camera");

            // Optional enhancements — use old-style null checks, not ?. (Chrome 63 compat)
            var track = this.stream.getVideoTracks()[0];
            if (track) {
                var capabilities = track.getCapabilities ? track.getCapabilities() : null;
                var settings = track.getSettings ? track.getSettings() : null;
                console.log("TRACK CAPABILITIES", capabilities);
                console.log("TRACK SETTINGS", settings);

                // Reset zoom to minimum on devices that default to a cropped/zoomed view (old Huawei)
                if (capabilities && capabilities.zoom && capabilities.zoom.min != null) {
                    try { await track.applyConstraints({ advanced: [{ zoom: capabilities.zoom.min }] }); console.log("ZOOM reset to", capabilities.zoom.min); } catch (e) {}
                }
                if (capabilities && capabilities.torch) {
                    try { await track.applyConstraints({ advanced: [{ torch: true }] }); console.log("TORCH ON"); } catch (e) {}
                }
                if (capabilities && capabilities.focusMode) {
                    try { await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] }); } catch (e) {}
                }
            }

            this.video.srcObject = this.stream;
            this.video.autoplay  = true;
            this.video.muted     = true;
            this.video.playsInline = true;
            // Set as DOM attributes too — needed on some old Android WebViews
            this.video.setAttribute("autoplay", "");
            this.video.setAttribute("muted", "");
            this.video.setAttribute("playsinline", "");
            this.video.setAttribute("webkit-playsinline", "");

            try { await this.video.play(); } catch(e) { console.warn("video.play() rejected, stream still attached", e.name); }
            console.log("CAMERA READY", this.video.videoWidth, "x", this.video.videoHeight);
        } catch (err) {
            console.error("CAMERA FAILED", err.name, err.message);
            throw err;
        }
    },

    // Normalises getUserMedia across modern and legacy APIs (old Huawei Android WebView)
    _getUserMedia: function(constraints) {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            return navigator.mediaDevices.getUserMedia(constraints);
        }
        var legacyGUM = navigator.getUserMedia ||
                        navigator.webkitGetUserMedia ||
                        navigator.mozGetUserMedia;
        if (legacyGUM) {
            return new Promise(function(resolve, reject) {
                legacyGUM.call(navigator, constraints, resolve, reject);
            });
        }
        return Promise.reject(new Error("getUserMedia not supported on this device"));
    },

    async setZoom(level) {
        this._cssZoom = level;
        var track = this.stream && this.stream.getVideoTracks()[0];
        if (track) {
            var caps = track.getCapabilities ? track.getCapabilities() : null;
            if (caps && caps.zoom && caps.zoom.max > 1) {
                var hwLevel = Math.min(Math.max(level, caps.zoom.min), caps.zoom.max);
                try {
                    await track.applyConstraints({ advanced: [{ zoom: hwLevel }] });
                    this._hwZoom = true;
                    if (this.video) this.video.style.transform = '';
                    console.log("HW ZOOM", hwLevel);
                    return;
                } catch(e) { console.warn("hw zoom failed", e); }
            }
        }
        this._hwZoom = false;
        if (this.video) {
            if (level <= 1) {
                this.video.style.transform = '';
                this.video.style.transformOrigin = '';
            } else {
                this.video.style.transform = 'scale(' + level + ')';
                this.video.style.transformOrigin = 'center center';
            }
        }
        console.log("CSS ZOOM", level);
    },

    async capture() {
        if (!this.video || !this.stream)
            throw new Error("Camera not started");

        // Brief pause for autofocus to settle
        await new Promise(function(x) { setTimeout(x, 500); });

        var rawW = this.video.videoWidth;
        var rawH = this.video.videoHeight;
        if (!rawW || !rawH) throw new Error("Video not ready");

        // When using CSS zoom (no hardware), crop the centre of the frame to match what user sees
        var srcX = 0, srcY = 0, srcW = rawW, srcH = rawH;
        if (!this._hwZoom && this._cssZoom > 1) {
            var z = this._cssZoom;
            srcW = Math.round(rawW / z);
            srcH = Math.round(rawH / z);
            srcX = Math.round((rawW - srcW) / 2);
            srcY = Math.round((rawH - srcH) / 2);
        }

        // iPhone in portrait mode gives rawH > rawW — rotate 90° clockwise so books read left-to-right
        var isPortrait = rawH > rawW;

        // Cap at 1536px longest side — enough for book cover text, keeps payload under 1MB
        var MAX = 1536;
        var w = srcW, h = srcH;
        if (w > MAX || h > MAX) {
            var ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }

        var canvas = document.createElement("canvas");
        var ctx = canvas.getContext("2d");

        if (isPortrait) {
            // Swap dims and rotate 90° clockwise
            canvas.width  = h;
            canvas.height = w;
            ctx.translate(h, 0);
            ctx.rotate(Math.PI / 2);
            ctx.drawImage(this.video, srcX, srcY, srcW, srcH, 0, 0, w, h);
            console.log("CAPTURED (rotated)", h, "x", w);
        } else {
            canvas.width  = w;
            canvas.height = h;
            ctx.drawImage(this.video, srcX, srcY, srcW, srcH, 0, 0, w, h);
            console.log("CAPTURED", w, "x", h);
        }

        return { image: canvas.toDataURL("image/jpeg", 0.88) };
    },

    stop: function() {
        try {
            if (!this.stream) return;
            this.stream.getTracks().forEach(function(t) { t.stop(); });
            this.stream = null;
            console.log("CAMERA STOPPED");
        } catch (e) {
            console.error("STOP CAMERA FAILED", e);
        }
    },

    speak: function(text) {
        if (!window.speechSynthesis) return;
        try {
            var u = new SpeechSynthesisUtterance(text);
            u.lang = 'da-DK';
            u.rate = 0.9;
            speechSynthesis.cancel();
            speechSynthesis.speak(u);
        } catch (e) {}
    },

    flash: function() {
        var wrap = document.querySelector('.shelf-camera-wrap');
        if (!wrap) return;
        var f = document.createElement('div');
        f.style.cssText = 'position:absolute;top:0;right:0;bottom:0;left:0;background:white;pointer-events:none;z-index:20;opacity:0.8;transition:opacity .35s ease-out;';
        wrap.appendChild(f);
        // double rAF ensures browser paints before transition starts
        requestAnimationFrame(function() {
            requestAnimationFrame(function() { f.style.opacity = '0'; });
        });
        setTimeout(function() { if (f.parentNode) f.remove(); }, 400);
    }
};

window.barcodeScanner = {
    stream: null,
    animFrame: null,
    lastCode: null,
    lastTime: 0,

    async start(videoId, dotNetRef) {
        var video = document.getElementById(videoId);
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

    scan: function(video, dotNetRef) {
        if (!('BarcodeDetector' in window)) {
            console.warn('BarcodeDetector not supported');
            return;
        }
        var detector = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'code_128', 'upc_a'] });
        var self = this;
        var loop = async function() {
            if (!self.stream) return;
            try {
                var barcodes = await detector.detect(video);
                if (barcodes.length > 0) {
                    var code = barcodes[0].rawValue;
                    var now = Date.now();
                    if (code !== self.lastCode || now - self.lastTime > 3000) {
                        self.lastCode = code;
                        self.lastTime = now;
                        await dotNetRef.invokeMethodAsync('OnBarcodeDetected', code);
                    }
                }
            } catch (e) {}
            self.animFrame = requestAnimationFrame(loop);
        };
        this.animFrame = requestAnimationFrame(loop);
    },

    stop: function() {
        if (this.animFrame) cancelAnimationFrame(this.animFrame);
        this.animFrame = null;
        if (this.stream) { this.stream.getTracks().forEach(function(t) { t.stop(); }); this.stream = null; }
        this.lastCode = null;
    }
};

window.downloadFile = function(filename, mimeType, base64) {
    var a = document.createElement('a');
    a.href = 'data:' + mimeType + ';base64,' + base64;
    a.download = filename;
    a.click();
};
