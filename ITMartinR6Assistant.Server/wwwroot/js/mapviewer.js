// Zoom/pan/ping for the full-screen map viewer. Runs entirely in the browser so dragging and
// zooming don't round-trip to the Blazor server. The stage element holds the floor image and markers;
// a transform on it does the zoom and pan, and a single click drops the "you are here" ping.
window.mapViewer = {
    init(viewport) {
        if (!viewport || viewport._mv) return;
        const stage = viewport.querySelector('.mv-stage');
        const ping = viewport.querySelector('.mv-ping');
        const s = { scale: 1, x: 0, y: 0, drag: null, moved: false, pinch: null };
        viewport._mv = s;

        const apply = () => { stage.style.transform = `translate(${s.x}px, ${s.y}px) scale(${s.scale})`; };
        const zoomAt = (factor, cx, cy) => {
            const r = viewport.getBoundingClientRect();
            const px = cx - r.left, py = cy - r.top;
            const next = Math.min(6, Math.max(1, s.scale * factor));
            s.x = px - (px - s.x) * (next / s.scale);
            s.y = py - (py - s.y) * (next / s.scale);
            s.scale = next;
            if (s.scale === 1) { s.x = 0; s.y = 0; }
            apply();
        };
        viewport._zoom = (f) => { const r = viewport.getBoundingClientRect(); zoomAt(f, r.left + r.width / 2, r.top + r.height / 2); };
        viewport._reset = () => { s.scale = 1; s.x = 0; s.y = 0; apply(); ping.style.display = 'none'; };

        viewport.addEventListener('wheel', e => { e.preventDefault(); zoomAt(e.deltaY < 0 ? 1.2 : 1 / 1.2, e.clientX, e.clientY); }, { passive: false });
        viewport.addEventListener('pointerdown', e => { s.drag = { x: e.clientX - s.x, y: e.clientY - s.y }; s.moved = false; viewport.setPointerCapture(e.pointerId); });
        viewport.addEventListener('pointermove', e => {
            if (!s.drag || s.pinch) return;
            const nx = e.clientX - s.drag.x, ny = e.clientY - s.drag.y;
            if (Math.abs(nx - s.x) + Math.abs(ny - s.y) > 3) s.moved = true;
            if (s.scale > 1) { s.x = nx; s.y = ny; apply(); }
        });
        viewport.addEventListener('pointerup', e => {
            const wasDrag = s.moved;
            s.drag = null;
            if (wasDrag) return;
            // A tap (no drag) drops the ping at that spot on the image.
            const img = stage.querySelector('img');
            const r = img.getBoundingClientRect();
            const fx = (e.clientX - r.left) / r.width, fy = (e.clientY - r.top) / r.height;
            if (fx < 0 || fx > 1 || fy < 0 || fy > 1) return;
            // In "Skriv rum" mode the tap picks a spot for a room name; Blazor shows the name box.
            if (viewport._naming) viewport._naming.invokeMethodAsync('RoomTap', fx * 100, fy * 100);
            ping.style.left = (fx * 100) + '%';
            ping.style.top = (fy * 100) + '%';
            ping.style.display = 'block';
        });
        viewport.addEventListener('dblclick', () => viewport._reset());

        // Two-finger pinch zoom on phones and tablets.
        viewport.addEventListener('touchstart', e => {
            if (e.touches.length === 2) {
                const [a, b] = e.touches;
                s.pinch = { d: Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY) };
            }
        }, { passive: true });
        viewport.addEventListener('touchmove', e => {
            if (e.touches.length !== 2 || !s.pinch) return;
            e.preventDefault();
            const [a, b] = e.touches;
            const d = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
            zoomAt(d / s.pinch.d, (a.clientX + b.clientX) / 2, (a.clientY + b.clientY) / 2);
            s.pinch.d = d;
        }, { passive: false });
        viewport.addEventListener('touchend', e => { if (e.touches.length < 2) s.pinch = null; });
    },
    zoom(viewport, factor) { viewport && viewport._zoom && viewport._zoom(factor); },
    setNaming(viewport, dotnet) { if (viewport) viewport._naming = dotnet; },
    reset(viewport) { viewport && viewport._reset && viewport._reset(); },
};
