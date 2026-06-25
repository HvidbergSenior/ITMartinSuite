window.scan = {
    stream: null,

    async startCamera(videoId) {
        const video = document.getElementById(videoId);
        if (!video) return;
        try {
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } },
                audio: false
            });
            video.srcObject = this.stream;
        } catch (e) {
            console.warn('Rear camera failed, trying any camera:', e);
            try {
                this.stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
                video.srcObject = this.stream;
            } catch (e2) {
                console.error('No camera available:', e2);
            }
        }
    },

    capture(videoId) {
        const video = document.getElementById(videoId);
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth || 1280;
        canvas.height = video.videoHeight || 720;
        canvas.getContext('2d').drawImage(video, 0, 0);
        return canvas.toDataURL('image/jpeg', 0.88).split(',')[1];
    },

    stopCamera() {
        if (this.stream) {
            this.stream.getTracks().forEach(t => t.stop());
            this.stream = null;
        }
    }
};
