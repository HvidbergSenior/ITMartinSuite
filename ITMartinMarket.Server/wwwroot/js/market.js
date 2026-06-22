window.marketApp = {
    getName: () => localStorage.getItem('market_name') ?? '',
    setName: (name) => localStorage.setItem('market_name', name),

    requestNotifyPermission: async () => {
        if ('Notification' in window && Notification.permission === 'default')
            await Notification.requestPermission();
    },

    notify: (title, body) => {
        if ('Notification' in window && Notification.permission === 'granted')
            new Notification(title, { body, icon: '/favicon.ico' });
    },

    capturePhoto: async (videoId) => {
        const video = document.getElementById(videoId);
        let w = video.videoWidth, h = video.videoHeight;
        const MAX = 1024;
        if (w > MAX || h > MAX) {
            const ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }
        const canvas = document.createElement('canvas');
        canvas.width = w; canvas.height = h;
        canvas.getContext('2d').drawImage(video, 0, 0, w, h);
        return canvas.toDataURL('image/jpeg', 0.85).replace('data:image/jpeg;base64,', '');
    },

    startCamera: async (videoId) => {
        const video = document.getElementById(videoId);
        const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
        video.srcObject = stream;
        await video.play();
    },

    stopCamera: (videoId) => {
        const video = document.getElementById(videoId);
        if (video?.srcObject) {
            video.srcObject.getTracks().forEach(t => t.stop());
            video.srcObject = null;
        }
    }
};
