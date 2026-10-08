// Tjek dine billeder - the RULES (user 2026-10-08): what is a photo, video, screenshot or chat picture, which year a
// photo is from (EXIF, else the file name), and which files are duplicates. No page and no network: the same file runs
// in the browser and in Node's tests (ITMartinHjem.Server/js-tests). Files are read only through file.slice(), and the
// fingerprint uses crypto.subtle - both exist in browsers and in Node. The page part is billeder-ui.js.
(function (root, factory) {
    if (typeof module === 'object' && module.exports) module.exports = factory();
    else root.BillederDomain = factory();
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    var PHOTO = /\.(jpe?g|png|heic|heif|gif|webp|bmp|tiff?|dng|cr2|cr3|nef|arw|orf|rw2|raf)$/i;
    var VIDEO = /\.(mp4|mov|avi|m4v|3gp|mts|m2ts|mkv|wmv|mpe?g)$/i;
    var SCREENSHOT = /screenshot|sk[æa]rmbillede|screen ?shot|^scr_|skærmbilleder/i;
    var CHAT = /^IMG-\d{8}-WA\d+|^VID-\d{8}-WA\d+|^received_\d+|^FB_IMG_|^Snapchat-|whatsapp|messenger|telegram/i;
    var NAME_DATE = /(19[89]\d|20[0-3]\d)[-_. ]?(0[1-9]|1[0-2])[-_. ]?(0[1-9]|[12]\d|3[01])/;

    function path(f) { return f.webkitRelativePath || f._path || f.name; }

    // EXIF DateTimeOriginal from the start of a JPEG ("2019:06:12 14:03:22"); null when there is none
    function exifYear(buf) {
        var v = new DataView(buf);
        if (v.byteLength < 4 || v.getUint16(0) !== 0xFFD8) return null;
        var o = 2;
        while (o + 4 < v.byteLength) {
            var marker = v.getUint16(o), len = v.getUint16(o + 2);
            if (marker === 0xFFE1 && v.getUint32(o + 4) === 0x45786966) {
                var t = o + 10, le = v.getUint16(t) === 0x4949;
                function u16(p) { return v.getUint16(t + p, le); }
                function u32(p) { return v.getUint32(t + p, le); }
                function find(ifd, tag) {
                    if (t + ifd + 2 > v.byteLength) return null;
                    var n = u16(ifd);
                    for (var i = 0; i < n; i++) {
                        var e = ifd + 2 + i * 12;
                        if (t + e + 12 > v.byteLength) return null;
                        if (u16(e) === tag) return u32(e + 8);
                    }
                    return null;
                }
                var ifd0 = u32(4), exif = find(ifd0, 0x8769), at = exif !== null ? find(exif, 0x9003) : null;
                if (at === null) at = find(ifd0, 0x0132);
                if (at === null || t + at + 4 > v.byteLength) return null;
                var y = '';
                for (var k = 0; k < 4; k++) y += String.fromCharCode(v.getUint8(t + at + k));
                y = +y;
                return y > 1980 && y < 2100 ? y : null;
            }
            if ((marker & 0xFF00) !== 0xFF00) return null;
            o += 2 + len;
        }
        return null;
    }

    function readSlice(file, start, end) {
        return file.slice(start, end).arrayBuffer();
    }

    async function pool(items, size, fn, progress) {
        var i = 0, doneCount = 0;
        async function worker() {
            while (i < items.length) {
                var item = items[i++];
                try { await fn(item); } catch (e) { /* unreadable file (e.g. OneDrive online-only): skip it */ }
                doneCount++;
                if (doneCount % 50 === 0) progress(doneCount, items.length);
            }
        }
        var ws = [];
        for (var w = 0; w < size; w++) ws.push(worker());
        await Promise.all(ws);
        progress(items.length, items.length);
    }

    async function hashOf(file) {
        var head = await readSlice(file, 0, 65536);
        var tail = file.size > 131072 ? await readSlice(file, file.size - 65536, file.size) : new ArrayBuffer(0);
        var both = new Uint8Array(head.byteLength + tail.byteLength);
        both.set(new Uint8Array(head), 0);
        both.set(new Uint8Array(tail), head.byteLength);
        var d = await crypto.subtle.digest('SHA-256', both);
        return Array.from(new Uint8Array(d).slice(0, 12)).map(function (b) { return b.toString(16).padStart(2, '0'); }).join('');
    }

    async function analyse(files, status) {
        var media = [], other = 0;
        files.forEach(function (f) {
            var name = f.name;
            if (name.startsWith('.') || /(^|\/)(thumbnails?|\.thumbnails|@eaDir)\//i.test(path(f))) return;
            if (PHOTO.test(name)) media.push({ f: f, video: false });
            else if (VIDEO.test(name)) media.push({ f: f, video: true });
            else other++;
        });
        var r = {
            photos: 0, videos: 0, bytes: 0, other: other,
            screenshots: { n: 0, bytes: 0 }, chat: { n: 0, bytes: 0 }, small: { n: 0, bytes: 0 },
            dups: { groups: 0, extra: 0, bytes: 0 }, years: {}, undated: 0
        };
        media.forEach(function (m) {
            var p = path(m.f);
            if (m.video) r.videos++; else r.photos++;
            r.bytes += m.f.size;
            if (SCREENSHOT.test(p)) { r.screenshots.n++; r.screenshots.bytes += m.f.size; }
            else if (CHAT.test(m.f.name) || CHAT.test(p)) { r.chat.n++; r.chat.bytes += m.f.size; }
            else if (!m.video && m.f.size < 30 * 1024) { r.small.n++; r.small.bytes += m.f.size; }
        });

        // Year: EXIF for JPEGs, else a date in the file name, else not counted (file dates lie after copying)
        status('Læser datoer …', 0, media.length);
        await pool(media, 12, async function (m) {
            var y = null;
            if (/\.jpe?g$/i.test(m.f.name)) y = exifYear(await readSlice(m.f, 0, 131072));
            if (!y) { var n = path(m.f).match(NAME_DATE); if (n) y = +n[1]; }
            if (y) r.years[y] = (r.years[y] || 0) + 1; else r.undated++;
        }, function (d, t) { status('Læser datoer …', d, t); });

        // Duplicates: only files with exactly the same size can be the same; those get a quick fingerprint
        var bySize = {};
        media.forEach(function (m) { if (m.f.size > 0) (bySize[m.f.size] = bySize[m.f.size] || []).push(m); });
        var candidates = [];
        Object.keys(bySize).forEach(function (s) { if (bySize[s].length > 1) candidates = candidates.concat(bySize[s]); });
        var byHash = {};
        status('Leder efter dubletter …', 0, candidates.length);
        await pool(candidates, 8, async function (m) {
            var h = m.f.size + ':' + await hashOf(m.f);
            (byHash[h] = byHash[h] || []).push(m);
        }, function (d, t) { status('Leder efter dubletter …', d, t); });
        Object.keys(byHash).forEach(function (h) {
            var g = byHash[h];
            if (g.length < 2) return;
            r.dups.groups++;
            r.dups.extra += g.length - 1;
            r.dups.bytes += (g.length - 1) * g[0].f.size;
        });
        return r;
    }

    return {
        path: path, exifYear: exifYear, analyse: analyse, pool: pool,
        patterns: { PHOTO: PHOTO, VIDEO: VIDEO, SCREENSHOT: SCREENSHOT, CHAT: CHAT, NAME_DATE: NAME_DATE }
    };
});
