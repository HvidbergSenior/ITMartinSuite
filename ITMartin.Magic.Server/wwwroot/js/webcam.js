window.webcam = {

    video: null,

    async start() {

        this.video =
            document.getElementById("video");

        const stream =
            await navigator.mediaDevices.getUserMedia({
                video: true
            });

        this.video.srcObject = stream;
    },

    capture() {

        const canvas =
            document.createElement("canvas");

        canvas.width =
            this.video.videoWidth;

        canvas.height =
            this.video.videoHeight;

        const ctx =
            canvas.getContext("2d");

        ctx.drawImage(
            this.video,
            0,
            0);

        return canvas.toDataURL(
            "image/jpeg");
    }
};