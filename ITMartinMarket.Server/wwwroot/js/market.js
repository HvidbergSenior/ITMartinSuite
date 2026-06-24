window.marketApp = {
    getName: function() { return localStorage.getItem('market_name') || ''; },
    setName: function(name) { localStorage.setItem('market_name', name); },

    requestNotifyPermission: async function() {
        if ('Notification' in window && Notification.permission === 'default')
            await Notification.requestPermission();
    },

    notify: function(title, body) {
        if ('Notification' in window && Notification.permission === 'granted')
            new Notification(title, { body: body, icon: '/favicon.ico' });
    },

    capturePhoto: async function(videoId) {
        var video = document.getElementById(videoId);
        var w = video.videoWidth, h = video.videoHeight;
        var MAX = 1024;
        if (w > MAX || h > MAX) {
            var ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }
        var canvas = document.createElement('canvas');
        canvas.width = w; canvas.height = h;
        canvas.getContext('2d').drawImage(video, 0, 0, w, h);
        return canvas.toDataURL('image/jpeg', 0.85).replace('data:image/jpeg;base64,', '');
    },

    startCamera: async function(videoId) {
        var video = document.getElementById(videoId);
        var stream;
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        } else {
            var legacyGUM = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
            stream = await new Promise(function(resolve, reject) {
                legacyGUM.call(navigator, { video: { facingMode: 'environment' } }, resolve, reject);
            });
        }
        video.srcObject = stream;
        video.setAttribute('playsinline', '');
        video.setAttribute('webkit-playsinline', '');
        await video.play();
    },

    stopCamera: function(videoId) {
        var video = document.getElementById(videoId);
        if (video && video.srcObject) {
            video.srcObject.getTracks().forEach(function(t) { t.stop(); });
            video.srcObject = null;
        }
    }
};
