window.webcam = {

    video: null,
    stream: null,

    start: function() {
        var self = this;
        return new Promise(function(resolve, reject) {
            try {
                self.video = document.getElementById("video");
                if (!self.video) { reject(new Error("VIDEO ELEMENT NOT FOUND")); return; }

                self.stop();

                var constraintSets = [
                    { video: { facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false },
                    { video: { facingMode: "environment" }, audio: false },
                    { video: true, audio: false }
                ];

                var tryNext = function(index) {
                    if (index >= constraintSets.length) {
                        reject(new Error("Could not access camera"));
                        return;
                    }
                    self._getUserMedia(constraintSets[index]).then(function(stream) {
                        self.stream = stream;

                        self.video.srcObject = stream;
                        self.video.setAttribute("autoplay", "");
                        self.video.setAttribute("muted", "");
                        self.video.setAttribute("playsinline", "");
                        self.video.setAttribute("webkit-playsinline", "");
                        self.video.autoplay = true;
                        self.video.muted = true;
                        self.video.playsInline = true;

                        self.video.play().then(function() {
                            console.log("CAMERA READY", self.video.videoWidth, self.video.videoHeight);
                            resolve();
                        }).catch(function(e) {
                            console.warn("play() failed", e.message);
                            resolve();
                        });
                    }).catch(function(e) {
                        console.warn("Camera attempt " + index + " failed", e.name, e.message);
                        tryNext(index + 1);
                    });
                };

                tryNext(0);
            } catch(e) {
                reject(e);
            }
        });
    },

    _getUserMedia: function(constraints) {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            return navigator.mediaDevices.getUserMedia(constraints);
        }
        var legacyGUM = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
        if (legacyGUM) {
            return new Promise(function(resolve, reject) {
                legacyGUM.call(navigator, constraints, resolve, reject);
            });
        }
        return Promise.reject(new Error("getUserMedia not supported on this device"));
    },

    capture: function() {
        var self = this;
        return new Promise(function(resolve, reject) {
            if (!self.video || !self.stream) {
                reject(new Error("Camera not started"));
                return;
            }

            setTimeout(function() {
                try {
                    var canvas = document.createElement("canvas");
                    var w = self.video.videoWidth;
                    var h = self.video.videoHeight;

                    var MAX = 1920;
                    if (w > MAX || h > MAX) {
                        var ratio = Math.min(MAX / w, MAX / h);
                        w = Math.round(w * ratio);
                        h = Math.round(h * ratio);
                    }

                    canvas.width = w;
                    canvas.height = h;

                    var ctx = canvas.getContext("2d");
                    ctx.drawImage(self.video, 0, 0, w, h);

                    console.log("CAPTURED", w, h);
                    resolve({ image: canvas.toDataURL("image/jpeg", 0.88) });
                } catch(e) {
                    reject(e);
                }
            }, 400);
        });
    },

    stop: function() {
        try {
            if (!this.stream) return;
            var tracks = this.stream.getTracks();
            for (var i = 0; i < tracks.length; i++) {
                tracks[i].stop();
            }
            this.stream = null;
            console.log("CAMERA STOPPED");
        } catch(e) {
            console.error("STOP CAMERA FAILED", e);
        }
    }
};
