window.marketApp = {
    getName: () => localStorage.getItem('market_name') ?? '',
    setName: (name) => localStorage.setItem('market_name', name),

    capturePhoto: async (videoId) => {
        const video = document.getElementById(videoId);
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
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
