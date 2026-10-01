// Notatskriver: dictation (the browser's own speech recognition), copy, download, share.
window.notat = {
    _rec: null,

    canDictate: () => !!(window.SpeechRecognition || window.webkitSpeechRecognition),

    // Streams finished sentences to .NET (OnDictated); stops when stopDictate() is called or the browser gives up.
    startDictate: (dotnet) => {
        const R = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!R) return false;
        const rec = new R();
        rec.lang = "da-DK";
        rec.continuous = true;
        rec.interimResults = false;
        rec.onresult = (e) => {
            for (let i = e.resultIndex; i < e.results.length; i++)
                if (e.results[i].isFinal) dotnet.invokeMethodAsync("OnDictated", e.results[i][0].transcript.trim());
        };
        rec.onend = () => { if (window.notat._rec === rec) { window.notat._rec = null; dotnet.invokeMethodAsync("OnDictateEnded"); } };
        rec.onerror = (e) => dotnet.invokeMethodAsync("OnDictateError", e.error || "fejl");
        window.notat._rec = rec;
        rec.start();
        return true;
    },

    stopDictate: () => { const r = window.notat._rec; window.notat._rec = null; if (r) r.stop(); },

    copy: async (text) => { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } },

    download: (name, base64, mime) => {
        const a = document.createElement("a");
        a.href = "data:" + mime + ";base64," + base64;
        a.download = name;
        document.body.appendChild(a); a.click(); a.remove();
    },

    canShare: () => !!navigator.share,
    share: async (title, text) => { try { await navigator.share({ title, text }); return true; } catch { return false; } },

    // The test-period access code, remembered on this device only.
    remember: (code) => { try { localStorage.setItem("notat-kode", code); } catch { } },
    remembered: () => { try { return localStorage.getItem("notat-kode"); } catch { return null; } },

    // Opens the camera / file picker behind a styled label.
    pick: (id) => document.getElementById(id)?.click(),
};
