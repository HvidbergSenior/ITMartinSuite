const MAX_CONCURRENT = 3;

const _queue = [];
let _running = 0;

export function init(dropZoneId, inputId, slug, dotNetRef) {
    const dz = document.getElementById(dropZoneId);
    const fi = document.getElementById(inputId);

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
        const files = Array.from(e.dataTransfer.files).filter(f => f.size > 0);
        if (files.length > 0) enqueue(files, slug, dotNetRef);
    });

    fi.addEventListener('change', () => {
        const files = Array.from(fi.files).filter(f => f.size > 0);
        if (files.length > 0) enqueue(files, slug, dotNetRef);
        fi.value = '';
    });
}

async function enqueue(files, slug, dotNetRef) {
    const infos = files.map(f => ({ name: f.name, size: f.size }));
    const ids = await dotNetRef.invokeMethodAsync('AddFiles', infos);
    for (let i = 0; i < files.length; i++) {
        _queue.push({ file: files[i], id: ids[i], slug, dotNetRef });
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

async function uploadOne({ file, id, slug, dotNetRef }) {
    await dotNetRef.invokeMethodAsync('OnStart', id);

    return new Promise(resolve => {
        const xhr = new XMLHttpRequest();
        const form = new FormData();
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

        xhr.open('POST', `/api/upload/${encodeURIComponent(slug)}`);
        xhr.send(form);
    });
}
