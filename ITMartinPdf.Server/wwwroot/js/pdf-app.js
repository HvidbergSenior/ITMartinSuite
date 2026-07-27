(function () {
    // No login, no invite link - anyone can use this tool. Each browser gets a
    // silent, random anonymous ID (never shown to the visitor) so the results
    // gallery only ever shows *their own* files, never anyone else's.
    var clientId = localStorage.getItem("pdf_client_id");
    if (!clientId) {
        clientId = (crypto.randomUUID ? crypto.randomUUID() : String(Date.now()) + Math.random()).replace(/-/g, "");
        localStorage.setItem("pdf_client_id", clientId);
    }

    function apiGet(url) {
        return fetch(url + (url.includes("?") ? "&" : "?") + "clientId=" + encodeURIComponent(clientId))
            .then(function (r) { if (!r.ok) throw new Error("request failed"); return r.json(); });
    }

    function apiPostForm(url, formData) {
        formData.append("clientId", clientId);
        return fetch(url, { method: "POST", body: formData })
            .then(function (r) { if (!r.ok) return r.text().then(function (t) { throw new Error(t || "request failed"); }); return r.json(); });
    }

    function apiPostJson(url, body) {
        body.clientId = clientId;
        return fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) })
            .then(function (r) { if (!r.ok) return r.text().then(function (t) { throw new Error(t || "request failed"); }); return r.json(); });
    }

    function apiDelete(url) {
        return fetch(url + "?clientId=" + encodeURIComponent(clientId), { method: "DELETE" });
    }

    // ═══ Results gallery ═══════════════════════════════════════════════

    function loadResults() {
        apiGet("/api/pdf/outputs").then(function (items) {
            var el = document.getElementById("results-list");
            if (items.length === 0) {
                el.innerHTML = '<div class="results-empty">Ingen resultater endnu — kør et af værktøjerne ovenfor.</div>';
                return;
            }
            el.innerHTML = items.map(function (it) {
                var time = new Date(it.createdAt).toLocaleString("da-DK", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit" });
                return '<div class="result-row">' +
                    '<div class="result-info"><div class="result-time">' + time + '</div><div class="result-size">' + it.sizeKb + ' KB</div></div>' +
                    '<a class="btn btn-ghost" href="/api/pdf/download/' + it.id + '?clientId=' + encodeURIComponent(clientId) + '" target="_blank">⬇️ Download</a>' +
                    '<button class="btn-icon" onclick="pdfApp.shareLink(\'' + it.id + '\')" title="Kopiér link (til at åbne på en anden enhed)">🔗</button>' +
                    '<button class="btn-icon" onclick="pdfApp.deleteResult(\'' + it.id + '\')" title="Slet">🗑️</button>' +
                    '</div>';
            }).join("");
        }).catch(function () {});
    }

    function afterRun(result) {
        loadResults();
        return result;
    }

    // Disables the triggering button and swaps its label to a busy state for
    // the duration of the async operation - without this, a slow request
    // (large images, a loaded NAS) looks exactly like "nothing happened",
    // which invites a second click and doubles the load on an already-slow
    // request instead of just waiting.
    function withBusy(btnId, busyText, fn) {
        var btn = document.getElementById(btnId);
        var original = btn ? btn.innerHTML : null;
        if (btn) { btn.disabled = true; btn.innerHTML = busyText; }
        return fn().finally(function () {
            if (btn) { btn.disabled = false; btn.innerHTML = original; }
        });
    }

    // ═══ Mode / sub-tab switching ═══════════════════════════════════════

    function showMode(mode) {
        document.querySelectorAll(".mode-panel").forEach(function (p) { p.style.display = "none"; });
        document.querySelectorAll(".mode-tab").forEach(function (t) { t.classList.remove("active"); });
        document.getElementById("mode-" + mode).style.display = "";
        document.querySelector('.mode-tab[data-mode="' + mode + '"]').classList.add("active");
    }

    function showSub(group, sub) {
        document.querySelectorAll('[id^="' + group + '-"]').forEach(function (p) { p.style.display = "none"; });
        document.querySelectorAll('.sub-tabs[data-group="' + group + '"] .sub-tab').forEach(function (t) { t.classList.remove("active"); });
        document.getElementById(group + "-" + sub).style.display = "";
        document.querySelector('.sub-tabs[data-group="' + group + '"] .sub-tab[data-sub="' + sub + '"]').classList.add("active");
    }

    // ═══ Create ══════════════════════════════════════════════════════

    function draftWithAi() {
        var notes = document.getElementById("c-notes").value.trim();
        if (!notes) return;
        var btn = document.getElementById("c-draft-btn");
        btn.disabled = true;
        btn.textContent = "Tænker…";
        apiPostJson("/api/pdf/draft", { notes: notes }).then(function (draft) {
            document.getElementById("c-title").value = draft.title || "";
            document.getElementById("c-body").value = draft.body || "";
        }).catch(function (err) {
            alert("Kunne ikke forbedre teksten: " + err.message);
        }).finally(function () {
            btn.disabled = false;
            btn.textContent = "🤖 Forbedre med AI";
        });
    }

    function createPdf() {
        var title = document.getElementById("c-title").value.trim() || "Dokument";
        var body = document.getElementById("c-body").value.trim();
        if (!body) { alert("Skriv noget indhold først"); return; }
        withBusy("c-create-btn", "⏳ Genererer...", function () {
            return apiPostJson("/api/pdf/create", { title: title, body: body }).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke generere PDF: " + err.message);
        });
    }

    // ═══ Images -> PDF ═══════════════════════════════════════════════

    var imageFiles = [];

    function onImagesSelected() {
        imageFiles = Array.from(document.getElementById("img-files").files);
        renderChipList("img-list", imageFiles.map(function (f) { return f.name; }));
        document.getElementById("img-run-btn").disabled = imageFiles.length === 0;
    }

    function imagesToPdf() {
        if (imageFiles.length === 0) return;
        var fd = new FormData();
        imageFiles.forEach(function (f, i) { fd.append("order-" + String(i).padStart(3, "0"), f); });
        withBusy("img-run-btn", "⏳ Genererer PDF...", function () {
            return apiPostForm("/api/pdf/images-to-pdf", fd).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke lave PDF: " + err.message);
        });
    }

    // ═══ Images -> PDF, from a ZIP ═══════════════════════════════════

    var zipFile = null;

    function onZipSelected() {
        var files = document.getElementById("zip-file").files;
        zipFile = files.length > 0 ? files[0] : null;
        renderChipList("zip-list", zipFile ? [zipFile.name] : []);
        document.getElementById("zip-run-btn").disabled = !zipFile;
    }

    function onOverlappingToggled() {
        var on = document.getElementById("zip-overlapping").checked;
        document.getElementById("zip-crop-row").style.display = on ? "flex" : "none";
    }

    function onPreciseToggled() {
        var precise = document.getElementById("zip-precise").checked;
        // The flat percentage stays visible either way - it's used as the
        // fallback if the precise AI crop call fails, and some overlaps
        // really are uniform, so it's still useful on its own.
        document.getElementById("zip-crop-percent-row").style.opacity = precise ? "0.6" : "1";
    }

    function zipToPdf() {
        if (!zipFile) return;
        var fd = new FormData();
        fd.append("zipfile", zipFile);
        var instructions = document.getElementById("zip-instructions").value.trim();
        if (instructions) fd.append("instructions", instructions);
        var overlapping = document.getElementById("zip-overlapping").checked;
        if (overlapping) {
            fd.append("overlapping", "true");
            fd.append("cropPercent", document.getElementById("zip-crop-percent").value || "15");
            if (document.getElementById("zip-precise").checked) fd.append("precise", "true");
        }
        withBusy("zip-run-btn", overlapping ? "⏳ Læser rækkefølge og bygger PDF..." : "⏳ Genererer PDF...", function () {
            return apiPostForm("/api/pdf/zip-to-pdf", fd).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke lave PDF af ZIP: " + err.message);
        });
    }

    // ═══ Organize: merge ═════════════════════════════════════════════

    var mergeFiles = [];

    function onMergeFilesSelected() {
        mergeFiles = Array.from(document.getElementById("merge-files").files);
        renderChipList("merge-list", mergeFiles.map(function (f) { return f.name; }));
        document.getElementById("merge-run-btn").disabled = mergeFiles.length < 2;
    }

    function mergePdfs() {
        if (mergeFiles.length < 2) return;
        var fd = new FormData();
        mergeFiles.forEach(function (f, i) { fd.append("order-" + String(i).padStart(3, "0"), f); });
        withBusy("merge-run-btn", "⏳ Fletter...", function () {
            return apiPostForm("/api/pdf/merge", fd).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke flette: " + err.message);
        });
    }

    // ═══ Organize: split ═════════════════════════════════════════════

    var splitFile = null;

    function onSplitFileSelected() {
        splitFile = document.getElementById("split-file").files[0] || null;
        renderChipList("split-filename", splitFile ? [splitFile.name] : []);
        document.getElementById("split-run-btn").disabled = !splitFile;
    }

    function splitPdf() {
        if (!splitFile) return;
        var fd = new FormData();
        fd.append("file", splitFile);
        fd.append("from", document.getElementById("split-from").value);
        fd.append("to", document.getElementById("split-to").value);
        withBusy("split-run-btn", "⏳ Deler...", function () {
            return apiPostForm("/api/pdf/split", fd).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke dele filen: " + err.message);
        });
    }

    // ═══ Organize: rotate ════════════════════════════════════════════

    var rotateFile = null;

    function onRotateFileSelected() {
        rotateFile = document.getElementById("rotate-file").files[0] || null;
        renderChipList("rotate-filename", rotateFile ? [rotateFile.name] : []);
        document.getElementById("rotate-run-btn").disabled = !rotateFile;
    }

    function rotatePdf() {
        if (!rotateFile) return;
        var fd = new FormData();
        fd.append("file", rotateFile);
        fd.append("page", document.getElementById("rotate-page").value);
        fd.append("degrees", document.getElementById("rotate-degrees").value);
        withBusy("rotate-run-btn", "⏳ Roterer...", function () {
            return apiPostForm("/api/pdf/rotate", fd).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke rotere: " + err.message);
        });
    }

    // ═══ Fill & Sign ═════════════════════════════════════════════════

    var signState = {
        file: null,
        pdfDoc: null,
        pageNum: 1,
        scale: 1.4,
        tool: "text",
        pendingSignature: null, // base64 png
        stamps: [] // { page, x, y, type, text, imageBase64, width, height }
    };

    function onSignFileSelected() {
        var file = document.getElementById("sign-file").files[0];
        if (!file) return;
        signState.file = file;
        signState.stamps = [];
        signState.pendingSignature = null;
        drawPadReady = false;
        renderPlacedList();
        var reader = new FileReader();
        reader.onload = function () {
            pdfjsLib.getDocument({ data: new Uint8Array(reader.result) }).promise.then(function (pdf) {
                signState.pdfDoc = pdf;
                signState.pageNum = 1;
                document.getElementById("sign-workspace").style.display = "";
                renderSignPage();
            });
        };
        reader.readAsArrayBuffer(file);
    }

    function renderSignPage() {
        signState.pdfDoc.getPage(signState.pageNum).then(function (page) {
            var viewport = page.getViewport({ scale: signState.scale });
            var canvas = document.getElementById("sign-canvas");
            canvas.width = viewport.width;
            canvas.height = viewport.height;
            var ctx = canvas.getContext("2d");
            page.render({ canvasContext: ctx, viewport: viewport }).promise.then(function () {
                drawStampMarkers();
            });
            document.getElementById("sign-page-label").textContent = "Side " + signState.pageNum + " / " + signState.pdfDoc.numPages;
        });
    }

    function drawStampMarkers() {
        var canvas = document.getElementById("sign-canvas");
        var ctx = canvas.getContext("2d");
        signState.stamps.filter(function (s) { return s.page === signState.pageNum; }).forEach(function (s) {
            var canvasX = s.x * signState.scale;
            var canvasY = canvas.height - (s.y * signState.scale);
            ctx.save();
            ctx.strokeStyle = "#e11d48";
            ctx.lineWidth = 1.5;
            ctx.strokeRect(canvasX - 2, canvasY - 14, (s.type === "image" ? s.width * signState.scale : 100), (s.type === "image" ? s.height * signState.scale : 18));
            ctx.restore();
        });
    }

    function signPrevPage() {
        if (!signState.pdfDoc || signState.pageNum <= 1) return;
        signState.pageNum--;
        renderSignPage();
    }

    function signNextPage() {
        if (!signState.pdfDoc || signState.pageNum >= signState.pdfDoc.numPages) return;
        signState.pageNum++;
        renderSignPage();
    }

    function setSignTool(tool) {
        signState.tool = tool;
        document.getElementById("sign-mode-text").classList.toggle("active", tool === "text");
        document.getElementById("sign-mode-draw").classList.toggle("active", tool === "draw");
        document.getElementById("sign-text-input").style.display = tool === "text" ? "" : "none";
        document.getElementById("sign-draw-pad").style.display = tool === "draw" ? "" : "none";
        if (tool === "draw") initDrawPad();
    }

    var drawing = false;
    var drawPadReady = false;
    function initDrawPad() {
        var canvas = document.getElementById("sign-draw-canvas");
        var ctx = canvas.getContext("2d");
        if (drawPadReady) return; // switching tabs shouldn't wipe an in-progress signature
        drawPadReady = true;
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.strokeStyle = "#0f172a";
        ctx.lineWidth = 2.5;
        ctx.lineCap = "round";

        function pos(e) {
            var rect = canvas.getBoundingClientRect();
            var cx = (e.touches ? e.touches[0].clientX : e.clientX) - rect.left;
            var cy = (e.touches ? e.touches[0].clientY : e.clientY) - rect.top;
            return { x: cx * (canvas.width / rect.width), y: cy * (canvas.height / rect.height) };
        }
        canvas.onmousedown = canvas.ontouchstart = function (e) {
            drawing = true;
            var p = pos(e);
            ctx.beginPath();
            ctx.moveTo(p.x, p.y);
        };
        canvas.onmousemove = canvas.ontouchmove = function (e) {
            if (!drawing) return;
            var p = pos(e);
            ctx.lineTo(p.x, p.y);
            ctx.stroke();
            e.preventDefault();
        };
        canvas.onmouseup = canvas.onmouseleave = canvas.ontouchend = function () { drawing = false; };
    }

    function clearSignature() {
        var canvas = document.getElementById("sign-draw-canvas");
        var ctx = canvas.getContext("2d");
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
    }

    function useSignature() {
        var canvas = document.getElementById("sign-draw-canvas");
        signState.pendingSignature = canvas.toDataURL("image/png").split(",")[1];
        alert("Signatur klar — klik nu på siden hvor den skal placeres.");
    }

    function onSignCanvasClick(e) {
        var canvas = document.getElementById("sign-canvas");
        var rect = canvas.getBoundingClientRect();
        var canvasX = (e.clientX - rect.left) * (canvas.width / rect.width);
        var canvasY = (e.clientY - rect.top) * (canvas.height / rect.height);
        var pdfX = canvasX / signState.scale;
        var pdfY = (canvas.height - canvasY) / signState.scale;

        if (signState.tool === "text") {
            var text = document.getElementById("sign-text-value").value.trim();
            if (!text) { alert("Skriv teksten først"); return; }
            signState.stamps.push({ page: signState.pageNum, x: pdfX, y: pdfY, type: "text", text: text, fontSize: 13 });
        } else {
            if (!signState.pendingSignature) { alert("Tegn og tryk 'Brug denne' først"); return; }
            signState.stamps.push({ page: signState.pageNum, x: pdfX, y: pdfY, type: "image", imageBase64: signState.pendingSignature, width: 130, height: 50 });
        }
        renderSignPage();
        renderPlacedList();
        document.getElementById("sign-run-btn").disabled = signState.stamps.length === 0;
    }

    function renderPlacedList() {
        var el = document.getElementById("sign-placed-list");
        if (signState.stamps.length === 0) { el.innerHTML = ""; return; }
        el.innerHTML = signState.stamps.map(function (s, i) {
            var label = s.type === "text" ? ('"' + escapeHtml(s.text) + '"') : "✍️ Signatur";
            return '<span class="chip">Side ' + s.page + ': ' + label + ' <button onclick="pdfApp.removeStamp(' + i + ')">✕</button></span>';
        }).join("");
    }

    function removeStamp(i) {
        signState.stamps.splice(i, 1);
        renderSignPage();
        renderPlacedList();
        document.getElementById("sign-run-btn").disabled = signState.stamps.length === 0;
    }

    function saveSignedPdf() {
        if (!signState.file || signState.stamps.length === 0) return;
        var fd = new FormData();
        fd.append("file", signState.file);
        fd.append("stamps", JSON.stringify(signState.stamps));
        withBusy("sign-run-btn", "⏳ Gemmer...", function () {
            return apiPostForm("/api/pdf/stamp", fd).then(afterRun);
        }).then(function () {
            signState.stamps = [];
            renderPlacedList();
            renderSignPage();
            document.getElementById("sign-run-btn").disabled = true;
        }).catch(function (err) {
            alert("Kunne ikke gemme: " + err.message);
        });
    }

    // ═══ Fill Form ═══════════════════════════════════════════════════

    var formFile = null;

    function onFormFileSelected() {
        formFile = document.getElementById("form-file").files[0];
        if (!formFile) return;
        var fd = new FormData();
        fd.append("file", formFile);
        apiPostForm("/api/pdf/form-fields", fd).then(function (fields) {
            var el = document.getElementById("form-fields-list");
            if (fields.length === 0) {
                el.innerHTML = '<p class="panel-hint">Ingen udfyldelige felter fundet i denne PDF. Prøv "Udfyld & signér" i stedet.</p>';
                document.getElementById("form-run-btn").style.display = "none";
                document.getElementById("form-flatten-row").style.display = "none";
                return;
            }
            el.innerHTML = fields.map(function (f) {
                return '<div class="form-field-row"><label class="field-label">' + escapeHtml(f.name) + '</label>' +
                    '<input class="field-input form-field-input" data-field="' + escapeHtml(f.name) + '" value="' + escapeHtml(f.value || "") + '" /></div>';
            }).join("");
            document.getElementById("form-run-btn").style.display = "";
            document.getElementById("form-flatten-row").style.display = "";
        }).catch(function (err) {
            alert("Kunne ikke læse formularen: " + err.message);
        });
    }

    function fillForm() {
        if (!formFile) return;
        var values = {};
        document.querySelectorAll(".form-field-input").forEach(function (inp) {
            values[inp.getAttribute("data-field")] = inp.value;
        });
        var fd = new FormData();
        fd.append("file", formFile);
        fd.append("values", JSON.stringify(values));
        fd.append("flatten", document.getElementById("form-flatten").checked ? "true" : "false");
        withBusy("form-run-btn", "⏳ Udfylder...", function () {
            return apiPostForm("/api/pdf/fill-form", fd).then(afterRun);
        }).catch(function (err) {
            alert("Kunne ikke udfylde: " + err.message);
        });
    }

    // ═══ Shared helpers ══════════════════════════════════════════════

    function renderChipList(elId, names) {
        var el = document.getElementById(elId);
        el.innerHTML = names.map(function (n) { return '<span class="chip">' + escapeHtml(n) + '</span>'; }).join("");
    }

    function escapeHtml(s) {
        var d = document.createElement("div");
        d.textContent = s == null ? "" : String(s);
        return d.innerHTML;
    }

    function deleteResult(id) {
        apiDelete("/api/pdf/outputs/" + id).then(loadResults);
    }

    // The download link already embeds clientId in the URL itself, so it's
    // not actually tied to this browser - copying the full link to another
    // device (phone -> PC) and opening it there works fine. This just makes
    // that path discoverable instead of requiring people to know that.
    function shareLink(id) {
        var url = window.location.origin + "/api/pdf/download/" + id + "?clientId=" + encodeURIComponent(clientId);
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(url).then(function () {
                alert("Link kopieret! Send det til dig selv (fx SMS/mail) og åbn det på den anden enhed for at downloade.");
            }).catch(function () { prompt("Kopiér dette link:", url); });
        } else {
            prompt("Kopiér dette link:", url);
        }
    }

    window.pdfApp = {
        showMode: showMode,
        showSub: showSub,
        draftWithAi: draftWithAi,
        createPdf: createPdf,
        onZipSelected: onZipSelected,
        onOverlappingToggled: onOverlappingToggled,
        onPreciseToggled: onPreciseToggled,
        zipToPdf: zipToPdf,
        onImagesSelected: onImagesSelected,
        imagesToPdf: imagesToPdf,
        onMergeFilesSelected: onMergeFilesSelected,
        mergePdfs: mergePdfs,
        onSplitFileSelected: onSplitFileSelected,
        splitPdf: splitPdf,
        onRotateFileSelected: onRotateFileSelected,
        rotatePdf: rotatePdf,
        onSignFileSelected: onSignFileSelected,
        setSignTool: setSignTool,
        clearSignature: clearSignature,
        useSignature: useSignature,
        onSignCanvasClick: onSignCanvasClick,
        signPrevPage: signPrevPage,
        signNextPage: signNextPage,
        removeStamp: removeStamp,
        saveSignedPdf: saveSignedPdf,
        onFormFileSelected: onFormFileSelected,
        fillForm: fillForm,
        deleteResult: deleteResult,
        shareLink: shareLink
    };

    document.addEventListener("DOMContentLoaded", function () {
        var page = document.getElementById("page-data");
        if (!page || page.getAttribute("data-page") !== "home") return;

        document.getElementById("p-app").style.display = "";
        loadResults();
    });
})();
