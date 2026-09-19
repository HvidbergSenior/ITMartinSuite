// Tag billede: upload every chosen file straight away with a plain fetch,
// remember the draft piece in sessionStorage so a reload (or the phone
// killing the tab while the camera is open) continues the same piece.
(function () {
    var status = document.getElementById('upload-status');
    var thumbs = document.getElementById('upload-thumbs');
    var errBox = document.getElementById('upload-error');
    var camText = document.getElementById('cam-text');
    var pieceIdInput = document.getElementById('pieceId');
    var count = 0;

    // A draft survives a reload but not a finished save (the form submit
    // clears it) - and never older than a day, so tomorrow starts fresh.
    var draft = null;
    try {
        var d = JSON.parse(sessionStorage.getItem('polstrer_draft') || 'null');
        if (d && Date.now() - d.at < 24 * 3600 * 1000) draft = d;
    } catch (e) { }
    if (draft) {
        pieceIdInput.value = draft.pieceId;
        (draft.files || []).forEach(addThumb);
        count = (draft.files || []).length;
        render();
    }

    function addThumb(f) {
        var el = document.createElement(f.isVideo ? 'div' : 'img');
        el.className = 'thumb';
        if (f.isVideo) el.textContent = '🎬'; else el.src = f.thumb || f.url;
        thumbs.appendChild(el);
    }

    function render() {
        status.textContent = count === 0 ? '' : count + (count === 1 ? ' billede gemt' : ' billeder gemt');
        camText.textContent = count === 0 ? 'Tag billede' : 'Tag et til';
    }

    async function upload(files) {
        if (!files || files.length === 0) return;
        errBox.hidden = true;
        camText.textContent = 'Gemmer…';
        var fd = new FormData();
        if (pieceIdInput.value) fd.append('pieceId', pieceIdInput.value);
        for (var i = 0; i < files.length; i++) fd.append('files', files[i], files[i].name || ('billede-' + Date.now() + '.jpg'));
        try {
            var res = await fetch('/api/upload', { method: 'POST', body: fd, credentials: 'same-origin' });
            if (!res.ok) throw new Error('HTTP ' + res.status);
            var json = await res.json();
            pieceIdInput.value = json.pieceId;
            draft = draft || { pieceId: json.pieceId, files: [], at: Date.now() };
            draft.pieceId = json.pieceId;
            json.files.forEach(function (f) { draft.files.push(f); addThumb(f); count++; });
            sessionStorage.setItem('polstrer_draft', JSON.stringify(draft));
        } catch (e) {
            errBox.textContent = 'Kunne ikke gemme billedet – prøv igen. (' + e.message + ')';
            errBox.hidden = false;
        }
        render();
    }

    ['cam-input', 'lib-input'].forEach(function (id) {
        var input = document.getElementById(id);
        input.addEventListener('change', function () { upload(input.files); input.value = ''; });
    });

    // Category chips fill the text field; tapping the active one clears it.
    var catInput = document.getElementById('category');
    document.querySelectorAll('#cat-chips .tag').forEach(function (b) {
        b.addEventListener('click', function () {
            var on = b.classList.contains('tag-on');
            document.querySelectorAll('#cat-chips .tag').forEach(function (x) { x.classList.remove('tag-on'); });
            catInput.value = on ? '' : b.dataset.cat;
            if (!on) b.classList.add('tag-on');
        });
    });

    document.getElementById('piece-form').addEventListener('submit', function () {
        sessionStorage.removeItem('polstrer_draft');
        document.getElementById('save-btn').disabled = true;
    });
})();
