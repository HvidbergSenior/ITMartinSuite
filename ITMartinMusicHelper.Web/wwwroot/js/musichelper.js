window.musicHelper = (function() {

    var _recorder = null;
    var _chunks = [];
    var _stream = null;

    function startRecording() {
        _chunks = [];
        return navigator.mediaDevices.getUserMedia({ audio: true, video: false })
            .then(function(stream) {
                _stream = stream;
                var mime = MediaRecorder.isTypeSupported("audio/webm") ? "audio/webm" : "";
                _recorder = new MediaRecorder(stream, mime ? { mimeType: mime } : {});
                _recorder.ondataavailable = function(e) {
                    if (e.data && e.data.size > 0) _chunks.push(e.data);
                };
                _recorder.start(500);
            })
            .catch(function(err) {
                console.error("Mic fejl", err.name, err.message);
                throw err;
            });
    }

    function stopRecording() {
        return new Promise(function(resolve) {
            if (!_recorder) { resolve(""); return; }
            var rec = _recorder;
            _recorder = null;
            rec.onstop = function() {
                var blob = new Blob(_chunks, { type: "audio/webm" });
                _chunks = [];
                if (_stream) { _stream.getTracks().forEach(function(t) { t.stop(); }); _stream = null; }
                if (blob.size === 0) { resolve(""); return; }
                var reader = new FileReader();
                reader.onload = function() {
                    var b64 = reader.result.split(",")[1];
                    resolve(b64);
                };
                reader.readAsDataURL(blob);
            };
            rec.stop();
        });
    }

    function playBase64Audio(b64) {
        var audio = new Audio("data:audio/webm;base64," + b64);
        audio.play().catch(function(e) { console.error("Playback fejl", e); });
    }

    return { startRecording: startRecording, stopRecording: stopRecording, playBase64Audio: playBase64Audio };
})();
