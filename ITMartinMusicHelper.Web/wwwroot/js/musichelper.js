window.musicHelper = (function () {

    var _recorder = null;
    var _chunks = [];
    var _stream = null;
    var _hasVideo = false;

    // Recordings stored in JS memory — never sent to server
    var _recordings = [];   // { url: string, hasVideo: bool }

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
                try {
                    _stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
                    useVideo = false;
                } catch (e2) {
                    throw e2;
                }
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
            if (!_recorder) { resolve(-1); return; }
            var rec = _recorder;
            var wasVideo = _hasVideo;
            _recorder = null;
            _hasVideo = false;

            rec.onstop = function () {
                var mime = wasVideo ? 'video/webm' : 'audio/webm';
                var blob = new Blob(_chunks, { type: mime });
                _chunks = [];
                if (_stream) { _stream.getTracks().forEach(function (t) { t.stop(); }); _stream = null; }
                if (blob.size === 0) { resolve(-1); return; }

                // Store in JS memory, return only the index — no data sent over SignalR
                var url = URL.createObjectURL(blob);
                var idx = _recordings.push({ url: url, hasVideo: wasVideo }) - 1;
                resolve(idx);
            };
            rec.stop();
        });
    }

    function playRecording(index) {
        var rec = _recordings[index];
        if (!rec) return;

        var old = document.getElementById('mh-player');
        if (old) old.remove();
        var oldClose = document.getElementById('mh-player-close');
        if (oldClose) oldClose.remove();

        var el = document.createElement(rec.hasVideo ? 'video' : 'audio');
        el.id = 'mh-player';
        el.src = rec.url;
        el.controls = true;
        el.autoplay = true;

        if (rec.hasVideo) {
            el.style.cssText = 'position:fixed;bottom:5rem;left:50%;transform:translateX(-50%);' +
                'width:min(360px,90vw);border-radius:12px;z-index:9999;box-shadow:0 8px 32px rgba(0,0,0,.5);background:#000;';
            var close = document.createElement('button');
            close.id = 'mh-player-close';
            close.textContent = '✕';
            close.style.cssText = 'position:fixed;bottom:calc(5rem + min(202px,50vw));left:calc(50% + min(160px,43vw));' +
                'transform:translateX(-50%);z-index:10000;background:#333;color:#fff;border:none;border-radius:50%;' +
                'width:32px;height:32px;cursor:pointer;font-size:1rem;';
            close.onclick = function () { el.remove(); close.remove(); };
            document.body.appendChild(close);
            el.onended = function () { el.remove(); close.remove(); };
        }

        if (!rec.hasVideo) {
            el.style.cssText = 'position:fixed;bottom:5rem;left:50%;transform:translateX(-50%);' +
                'width:min(360px,90vw);z-index:9999;background:#1a1a28;border-radius:12px;';
            var closeA = document.createElement('button');
            closeA.id = 'mh-player-close';
            closeA.textContent = '✕';
            closeA.style.cssText = 'position:fixed;bottom:calc(5rem + 54px);left:calc(50% + min(160px,43vw));' +
                'transform:translateX(-50%);z-index:10000;background:#333;color:#fff;border:none;border-radius:50%;' +
                'width:32px;height:32px;cursor:pointer;font-size:1rem;';
            closeA.onclick = function () { el.remove(); closeA.remove(); };
            document.body.appendChild(closeA);
            el.onended = function () { el.remove(); closeA.remove(); };
        }

        document.body.appendChild(el);
        el.play().catch(function (e) { console.error('Playback fejl', e); });
    }

    function deleteRecording(index) {
        var rec = _recordings[index];
        if (rec) { URL.revokeObjectURL(rec.url); _recordings[index] = null; }
    }

    return { startRecording: startRecording, stopRecording: stopRecording, playRecording: playRecording, deleteRecording: deleteRecording };
})();
