// Mine skiver: the collection lives in this browser only (localStorage) - nothing is sent to ITMartin.
window.skiver = {
    load: function () {
        try { return localStorage.getItem("mineskiver.v1") || "[]"; } catch (e) { return "[]"; }
    },
    save: function (json) {
        try { localStorage.setItem("mineskiver.v1", json); return true; } catch (e) { return false; }
    },
    getPlace: function () {
        try { return localStorage.getItem("mineskiver.place") || ""; } catch (e) { return ""; }
    },
    setPlace: function (p) {
        try { localStorage.setItem("mineskiver.place", p || ""); } catch (e) { }
    },
    // Save the collection as a file - a backup, or to move it to another phone/PC.
    download: function (name, json) {
        var a = document.createElement("a");
        a.href = URL.createObjectURL(new Blob([json], { type: "application/json" }));
        a.download = name;
        document.body.appendChild(a); a.click(); a.remove();
    },
    readFile: function (input) {
        return new Promise(function (ok) {
            var f = input && input.files && input.files[0];
            if (!f) { ok(""); return; }
            var r = new FileReader();
            r.onload = function () { ok(String(r.result || "")); };
            r.onerror = function () { ok(""); };
            r.readAsText(f);
        });
    }
};
