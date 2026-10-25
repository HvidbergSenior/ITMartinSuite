// Kolibri Læs - the book reader. The server sends each page as a picture (2026-10-25: drawing a print PDF in the
// browser with pdf.js froze the tab), so turning a page is as light as showing a photo - also on a phone.
// Two pages side by side when the screen is wide (a spread starts on an even page, like a printed book: 1 alone, then
// 2-3, 4-5 ...), one on a phone. Arrows, arrow keys and a swipe turn the page; the page is saved a moment after turning.
window.book = {
    base: "", saveUrl: "", pages: 0, page: 1, saveTimer: null,

    two() { return window.innerWidth >= 900 && window.innerWidth > window.innerHeight; },

    // The first page of the spread that holds this page.
    start(p) { return this.two() && p > 1 ? (p % 2 === 0 ? p : p - 1) : p; },

    // The picture width that fits the screen: 800 (phone), 1200 (PC) or 1800 (big screen).
    width() {
        var w = window.innerWidth * Math.min(window.devicePixelRatio || 1, 2) / (this.two() ? 2 : 1);
        return w <= 800 ? 800 : w <= 1200 ? 1200 : 1800;
    },

    url(n) { return this.base + n + ".jpg?b=" + this.width(); },

    open(base, saveUrl, page, pages) {
        this.base = base; this.saveUrl = saveUrl; this.pages = pages;
        this.page = Math.min(Math.max(1, page), pages);
        this.keys = (e) => { if (e.key === "ArrowRight" || e.key === "PageDown") this.next(); if (e.key === "ArrowLeft" || e.key === "PageUp") this.prev(); };
        this.resize = () => this.draw();
        document.addEventListener("keydown", this.keys);
        window.addEventListener("resize", this.resize);
        var spread = document.getElementById("ls-spread"), x0 = null;
        spread.addEventListener("touchstart", (e) => { x0 = e.touches[0].clientX; }, { passive: true });
        spread.addEventListener("touchend", (e) => {
            if (x0 === null) return;
            var dx = e.changedTouches[0].clientX - x0; x0 = null;
            if (Math.abs(dx) > 50) { dx < 0 ? this.next() : this.prev(); }
        }, { passive: true });
        this.draw();
    },

    shown() {
        var first = this.start(this.page), list = [first];
        if (this.two() && first > 1 && first + 1 <= this.pages) list.push(first + 1);
        return list;
    },

    draw() {
        var spread = document.getElementById("ls-spread");
        if (!spread) return;
        var list = this.shown();
        spread.classList.toggle("two", list.length === 2);
        spread.replaceChildren(...list.map((n) => {
            var img = document.createElement("img");
            img.src = this.url(n);
            img.alt = "Side " + n;
            img.decoding = "async";
            return img;
        }));
        var label = document.getElementById("ls-pageno");
        if (label) label.textContent = (list.length === 2 ? "Side " + list[0] + "-" + list[1] : "Side " + list[0]) + " af " + this.pages;
        // The next spread is fetched now, so the next turn is instant.
        var last = list[list.length - 1];
        for (var n = last + 1; n <= Math.min(this.pages, last + 2); n++) { new Image().src = this.url(n); }
    },

    next() {
        var list = this.shown(), last = list[list.length - 1];
        if (last >= this.pages) return;
        this.page = last + 1; this.draw(); this.save();
    },

    prev() {
        var first = this.shown()[0];
        if (first <= 1) return;
        this.page = this.two() ? Math.max(1, first - 2) : first - 1;
        this.draw(); this.save();
    },

    save() {
        clearTimeout(this.saveTimer);
        var page = this.shown()[0];
        this.saveTimer = setTimeout(() => { fetch(this.saveUrl + page, { method: "POST", credentials: "same-origin" }).catch(() => {}); }, 800);
    },

    close() {
        if (this.keys) document.removeEventListener("keydown", this.keys);
        if (this.resize) window.removeEventListener("resize", this.resize);
    }
};
