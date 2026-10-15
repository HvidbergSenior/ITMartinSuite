const MAX_CONCURRENT = 3;

const _queue = [];
let _running = 0;

export function init(dropZoneId, inputId, slug, token, dotNetRef, folderInputId) {
    const dz = document.getElementById(dropZoneId);
    const fi = document.getElementById(inputId);
    const fd = folderInputId ? document.getElementById(folderInputId) : null;

    dz.addEventListener('dragover', e => {
        e.preventDefault();
        dz.classList.add('dragover');
    });

    dz.addEventListener('dragleave', e => {
        if (!dz.contains(e.relatedTarget)) dz.classList.remove('dragover');
    });

    dz.addEventListener('drop', e => {
        e.preventDefault();
        dz.classList.remove('dragover');
        // A dropped folder is walked so "David Bowie/Hunky Dory/01 Changes.flac" keeps its folders;
        // plain files arrive with an empty folder, like before.
        const entries = Array.from(e.dataTransfer.items || [])
            .map(i => i.webkitGetAsEntry ? i.webkitGetAsEntry() : null);
        if (entries.length > 0 && entries.every(x => x)) {
            collect(entries).then(items => { if (items.length > 0) enqueue(items, slug, token, dotNetRef); });
            return;
        }
        const items = Array.from(e.dataTransfer.files).filter(f => f.size > 0).map(f => ({ file: f, dir: '' }));
        if (items.length > 0) enqueue(items, slug, token, dotNetRef);
    });

    fi.addEventListener('change', () => {
        const items = Array.from(fi.files).filter(f => f.size > 0).map(f => ({ file: f, dir: '' }));
        if (items.length > 0) enqueue(items, slug, token, dotNetRef);
        fi.value = '';
    });

    if (fd) fd.addEventListener('change', () => {
        const items = Array.from(fd.files).filter(f => f.size > 0)
            .map(f => ({ file: f, dir: (f.webkitRelativePath || '').split('/').slice(0, -1).join('/') }));
        if (items.length > 0) enqueue(items, slug, token, dotNetRef);
        fd.value = '';
    });
}

async function collect(entries) {
    const out = [];
    async function walk(entry, dir) {
        if (entry.isFile) {
            const file = await new Promise((ok, fail) => entry.file(ok, fail));
            if (file.size > 0) out.push({ file, dir });
        } else if (entry.isDirectory) {
            const reader = entry.createReader();
            const sub = dir ? `${dir}/${entry.name}` : entry.name;
            // readEntries hands out at most ~100 at a time - keep asking until it is empty.
            for (;;) {
                const batch = await new Promise((ok, fail) => reader.readEntries(ok, fail));
                if (batch.length === 0) break;
                for (const child of batch) await walk(child, sub);
            }
        }
    }
    for (const e of entries) await walk(e, '');
    return out;
}

async function enqueue(items, slug, token, dotNetRef) {
    const files = items.map(i => i.file);
    const infos = items.map(i => ({ name: i.file.name, size: i.file.size, dir: i.dir }));
    // An empty id means the server already has that exact file - don't send it again.
    const ids = await dotNetRef.invokeMethodAsync('AddFiles', infos);
    for (let i = 0; i < files.length; i++) {
        if (!ids[i]) continue;
        _queue.push({ file: files[i], dir: items[i].dir, id: ids[i], slug, token, dotNetRef });
    }
    drain();
}

function drain() {
    while (_running < MAX_CONCURRENT && _queue.length > 0) {
        const entry = _queue.shift();
        _running++;
        uploadOne(entry).finally(() => {
            _running--;
            drain();
        });
    }
}

async function uploadOne({ file, dir, id, slug, token, dotNetRef }) {
    await dotNetRef.invokeMethodAsync('OnStart', id);

    return new Promise(resolve => {
        const xhr = new XMLHttpRequest();
        const form = new FormData();
        if (dir) form.append('path', dir);
        form.append('file', file);

        xhr.upload.addEventListener('progress', e => {
            if (e.lengthComputable) {
                const pct = Math.round(e.loaded / e.total * 100);
                dotNetRef.invokeMethodAsync('OnProgress', id, pct);
            }
        });

        const finish = (ok, msg) =>
            dotNetRef.invokeMethodAsync('OnDone', id, ok, msg).finally(resolve);

        xhr.addEventListener('load', () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                finish(true, '');
            } else {
                finish(false, xhr.responseText || `Serverfejl (${xhr.status})`);
            }
        });

        xhr.addEventListener('error', () => finish(false, 'Netværksfejl – tjek forbindelsen'));
        xhr.addEventListener('abort', () => finish(false, 'Annulleret'));

        const url = `/api/upload/${encodeURIComponent(slug)}${token ? `?token=${encodeURIComponent(token)}` : ''}`;
        xhr.open('POST', url);
        xhr.send(form);
    });
}
