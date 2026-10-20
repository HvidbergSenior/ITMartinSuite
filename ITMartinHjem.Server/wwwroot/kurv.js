// The basket on bogshoppen.dk (2026-10-20): item ids in this browser's own storage - a convenience for the buyer, nothing
// to keep. Lager prices it with today's offers whenever the basket page is opened, so a stale basket never costs wrong.
window.bsKurv = {
    key: "bs-kurv",
    memory: [],
    get() {
        try { var v = JSON.parse(localStorage.getItem(this.key) || "[]"); return Array.isArray(v) ? v.filter(Number.isInteger) : []; }
        catch (e) { return this.memory.slice(); }
    },
    save(ids) {
        this.memory = ids.slice(0, 50);
        try { localStorage.setItem(this.key, JSON.stringify(this.memory)); } catch (e) { }
        this.show();
        return this.memory.length;
    },
    add(id) { var ids = this.get(); ids.push(id); return this.save(ids); },
    remove(id) { var ids = this.get(), n = ids.indexOf(id); if (n >= 0) ids.splice(n, 1); return this.save(ids); },
    clear() { return this.save([]); },
    count() { return this.get().length; },
    show() {
        var n = this.get().length;
        document.querySelectorAll("[data-kurv-count]").forEach(function (el) { el.textContent = n > 0 ? " (" + n + ")" : ""; });
    }
};
document.addEventListener("DOMContentLoaded", function () { bsKurv.show(); });
window.addEventListener("storage", function () { bsKurv.show(); });
