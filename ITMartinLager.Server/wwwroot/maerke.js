// Fjern prismærket on Lager's item page (user 2026-11-16: "and the farvening" - it was only in Priser): drag a box over the
// price sticker on the photo; the box is handed to Blazor as fractions of the photo (left, top, right, bottom).
window.lagerMaerke = {
    start: function (imgId, boxId, dotnet) {
        var img = document.getElementById(imgId), box = document.getElementById(boxId);
        if (!img || !box) return;
        var from = null, area = null;
        function at(e) {
            var r = img.getBoundingClientRect();
            return { x: Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)), y: Math.min(1, Math.max(0, (e.clientY - r.top) / r.height)) };
        }
        function draw() {
            var w = img.clientWidth, h = img.clientHeight;
            box.style.left = (img.offsetLeft + area.l * w) + 'px'; box.style.top = (img.offsetTop + area.t * h) + 'px';
            box.style.width = ((area.r - area.l) * w) + 'px'; box.style.height = ((area.b - area.t) * h) + 'px';
            box.hidden = false;
        }
        function down(e) { e.preventDefault(); from = at(e); img.setPointerCapture(e.pointerId); }
        function move(e) {
            if (!from) return; var p = at(e);
            area = { l: Math.min(from.x, p.x), t: Math.min(from.y, p.y), r: Math.max(from.x, p.x), b: Math.max(from.y, p.y) };
            draw();
        }
        function up() {
            if (!from) return; from = null;
            if (area && area.r - area.l > 0.005 && area.b - area.t > 0.005) dotnet.invokeMethodAsync('MarkDrawn', area.l, area.t, area.r, area.b);
        }
        img.onpointerdown = down; img.onpointermove = move; img.onpointerup = up;
        img.style.touchAction = 'none'; img.style.cursor = 'crosshair';
    },
    stop: function (imgId, boxId) {
        var img = document.getElementById(imgId), box = document.getElementById(boxId);
        if (img) { img.onpointerdown = img.onpointermove = img.onpointerup = null; img.style.touchAction = ''; img.style.cursor = ''; }
        if (box) box.hidden = true;
    }
};
