window.musicHelper = (function () {

    var _recorder = null;
    var _chunks = [];
    var _stream = null;
    var _hasVideo = false;

    async function startRecording(videoElementId, useVideo) {
        _chunks = [];
        _hasVideo = false;

        var constraints = useVideo
            ? { audio: true, video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } } }
            : { audio: true, video: false };

        try {
            _stream = await navigator.mediaDevices.getUserMedia(constraints);
        } catch (e) {
            if (useVideo) {
                // Fall back to audio-only
                _stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
                useVideo = false;
            } else {
                throw e;
            }
        }

        _hasVideo = useVideo;

        if (useVideo && videoElementId) {
            var vid = document.getElementById(videoElementId);
            if (vid) { vid.srcObject = _stream; }
        }

        var mime = _hasVideo
            ? (MediaRecorder.isTypeSupported('video/webm;codecs=vp9,opus') ? 'video/webm;codecs=vp9,opus'
             : MediaRecorder.isTypeSupported('video/webm') ? 'video/webm' : '')
            : (MediaRecorder.isTypeSupported('audio/webm') ? 'audio/webm' : '');

        _recorder = new MediaRecorder(_stream, mime ? { mimeType: mime } : {});
        _recorder.ondataavailable = function (e) {
            if (e.data && e.data.size > 0) _chunks.push(e.data);
        };
        _recorder.start(500);

        return _hasVideo;
    }

    function stopRecording() {
        return new Promise(function (resolve) {
            if (!_recorder) { resolve({ data: '', hasVideo: false }); return; }
            var rec = _recorder;
            var wasVideo = _hasVideo;
            _recorder = null;
            _hasVideo = false;

            rec.onstop = function () {
                var mime = wasVideo ? 'video/webm' : 'audio/webm';
                var blob = new Blob(_chunks, { type: mime });
                _chunks = [];
                if (_stream) { _stream.getTracks().forEach(function (t) { t.stop(); }); _stream = null; }
                if (blob.size === 0) { resolve({ data: '', hasVideo: false }); return; }

                var reader = new FileReader();
                reader.onload = function () {
                    resolve({ data: reader.result.split(',')[1], hasVideo: wasVideo });
                };
                reader.readAsDataURL(blob);
            };
            rec.stop();
        });
    }

    function playRecording(b64, hasVideo) {
        var mime = hasVideo ? 'video/webm' : 'audio/webm';
        var src = 'data:' + mime + ';base64,' + b64;

        // Remove any existing floating player
        var old = document.getElementById('mh-player');
        if (old) old.remove();

        var el = document.createElement(hasVideo ? 'video' : 'audio');
        el.id = 'mh-player';
        el.src = src;
        el.controls = true;
        el.autoplay = true;

        if (hasVideo) {
            el.style.cssText = 'position:fixed;bottom:5rem;left:50%;transform:translateX(-50%);' +
                'width:min(360px,90vw);border-radius:12px;z-index:9999;box-shadow:0 8px 32px rgba(0,0,0,.5);background:#000;';
            var close = document.createElement('button');
            close.textContent = '✕';
            close.style.cssText = 'position:fixed;bottom:calc(5rem + min(202px,50vw));left:calc(50% + min(160px,43vw));' +
                'transform:translateX(-50%);z-index:10000;background:#333;color:#fff;border:none;border-radius:50%;' +
                'width:32px;height:32px;cursor:pointer;font-size:1rem;';
            close.onclick = function () { el.remove(); close.remove(); };
            document.body.appendChild(close);
            el.onended = function () { el.remove(); close.remove(); };
        }

        document.body.appendChild(el);
        if (!hasVideo) el.play().catch(function (e) { console.error('Playback fejl', e); });
    }

    return { startRecording: startRecording, stopRecording: stopRecording, playRecording: playRecording };
})();
