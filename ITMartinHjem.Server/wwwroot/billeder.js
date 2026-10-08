// Tjek dine billeder (itmartin.dk/billeder, user 2026-10-08): pick the photo folder and see how many photos and videos
// there are, duplicates, screenshots, chat pictures and which years are missing. Like Budget, everything is read HERE
// in the browser - no file is sent anywhere, nothing is saved. The paid version is the full FileSorter job.
(function () {
    'use strict';

    var PHOTO = /\.(jpe?g|png|heic|heif|gif|webp|bmp|tiff?|dng|cr2|cr3|nef|arw|orf|rw2|raf)$/i;
    var VIDEO = /\.(mp4|mov|avi|m4v|3gp|mts|m2ts|mkv|wmv|mpe?g)$/i;
    var SCREENSHOT = /screenshot|sk[æa]rmbillede|screen ?shot|^scr_|skærmbilleder/i;
    var CHAT = /^IMG-\d{8}-WA\d+|^VID-\d{8}-WA\d+|^received_\d+|^FB_IMG_|^Snapchat-|whatsapp|messenger|telegram/i;
    var NAME_DATE = /(19[89]\d|20[0-3]\d)[-_. ]?(0[1-9]|1[0-2])[-_. ]?(0[1-9]|[12]\d|3[01])/;

    // ---------- reading the folder ----------
    function path(f) { return f.webkitRelativePath || f._path || f.name; }

    // Files dropped as a folder: walk the entries (the folder picker gives a flat list by itself)
    function walk(entry, prefix, out) {
        return new Promise(function (done) {
            if (entry.isFile) {
                entry.file(function (f) { f._path = prefix + f.name; out.push(f); done(); }, function () { done(); });
            } else if (entry.isDirectory) {
                var reader = entry.createReader(), all = [];
                (function more() {
                    reader.readEntries(function (batch) {
                        if (!batch.length) {
                            Promise.all(all.map(function (e) { return walk(e, prefix + entry.name + '/', out); })).then(done);
                            return;
                        }
                        all = all.concat(Array.prototype.slice.call(batch));
                        more();
                    }, function () { done(); });
                })();
            } else done();
        });
    }

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

    // ---------- showing it ----------
    var nf = new Intl.NumberFormat('da-DK');
    function n(x) { return nf.format(x); }
    function gb(b) {
        if (b < 1e9) return new Intl.NumberFormat('da-DK', { maximumFractionDigits: 0 }).format(b / 1e6) + ' MB';
        return new Intl.NumberFormat('da-DK', { maximumFractionDigits: 1 }).format(b / 1e9) + ' GB';
    }

    function tile(label, count, bytes, sub) {
        return '<div class="bu-tile"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + n(count) + '</div>' +
            '<div class="bu-tile-sub">' + gb(bytes) + (sub ? ' · ' + sub : '') + '</div></div>';
    }

    function render(r, box) {
        var total = r.photos + r.videos;
        var maybe = r.screenshots.bytes + r.chat.bytes + r.small.bytes;
        var html = '<section class="k-card bu-result" data-nofold><h2 class="k-card-title">🖼️ Det fandt jeg i din mappe</h2>';
        html += '<div class="bu-big"><div class="bu-big-label">Billeder og videoer</div><div class="bu-big-num">' + n(total) + '</div>' +
            '<div class="bu-big-sub">' + n(r.photos) + ' billeder · ' + n(r.videos) + ' videoer · ' + gb(r.bytes) + ' i alt</div></div>';
        html += '<div class="bu-grid">' +
            tile('♊ Dubletter (ekstra kopier)', r.dups.extra, r.dups.bytes, 'kan fjernes – én kopi bliver') +
            tile('📱 Skærmbilleder', r.screenshots.n, r.screenshots.bytes) +
            tile('💬 Billeder fra WhatsApp, Messenger m.fl.', r.chat.n, r.chat.bytes) +
            tile('🔍 Meget små billeder', r.small.n, r.small.bytes, 'ofte ikoner og miniaturer') + '</div>';
        html += '<p class="bu-sum">Dubletterne alene fylder <b>' + gb(r.dups.bytes) + '</b>. Skærmbilleder, chat-billeder og små billeder fylder <b>' +
            gb(maybe) + '</b> mere – dem kan de fleste undvære en stor del af.</p></section>';

        var years = Object.keys(r.years).map(Number).sort();
        if (years.length) {
            var max = Math.max.apply(null, years.map(function (y) { return r.years[y]; })), missing = [];
            html += '<details class="k-card" open><summary><b>📅 Billeder pr. år</b></summary><div class="bi-years">';
            for (var y = years[0]; y <= years[years.length - 1]; y++) {
                var c = r.years[y] || 0;
                if (!c) missing.push(y);
                html += '<div class="bi-year"><span class="bi-y">' + y + '</span><span class="bi-bar' + (c ? '' : ' none') + '" style="width:' +
                    (c ? Math.max(2, Math.round(c / max * 100)) : 0) + '%"></span><span class="bi-c">' + (c ? n(c) : 'ingen') + '</span></div>';
            }
            html += '</div>';
            if (missing.length) html += '<p class="bu-blind">🕳️ <b>Ingen billeder fra ' + missing.join(', ') + '.</b> Ligger de på en gammel telefon, en harddisk eller i en sky?</p>';
            if (r.undated) html += '<p class="k-help">' + n(r.undated) + ' billeder og videoer har ingen dato, jeg kunne læse. De tæller ikke med her.</p>';
            html += '</details>';
        }

        html += '<section class="k-card bu-paid" data-nofold><h2 class="k-card-title">➕ Skal jeg rydde op for dig?</h2>' +
            '<p>I den <b>udvidede udgave</b> sorterer jeg <b>alle</b> dine billeder og videoer efter år og måned, fjerner dubletter og skrammel, ' +
            'finder personerne på billederne og laver et <b>galleri</b>, hele familien kan se på telefonen – plus en backup, så intet går tabt.</p>' +
            '<p>Se hvordan det ser ud: <a href="https://gallery.itmartin.dk/demo" target="_blank" rel="noopener">eksempel-galleriet</a> (spørg mig om koden i chatten).</p>' +
            '<a class="k-btn k-btn-primary" href="/#chat">💬 Spørg om oprydning</a></section>';
        box.innerHTML = html;
        box.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function status(box, text, done, totalCount) {
        box.innerHTML = '<section class="k-card" data-nofold><p><b>' + text + '</b> ' + (totalCount ? n(done) + ' af ' + n(totalCount) : '') + '</p>' +
            '<p class="k-help">Alt sker her på din computer. Mange tusinde billeder kan tage et minut eller to.</p></section>';
    }

    async function handle(files) {
        var box = document.getElementById('bi-out');
        if (!box) return;
        if (!files.length) {
            box.innerHTML = '<section class="k-card bu-error" data-nofold><p>Mappen var tom – eller din browser kan ikke åbne mapper. Prøv Chrome eller Edge på en computer.</p></section>';
            return;
        }
        var r = await analyse(files, function (t, d, c) { status(box, t, d, c); });
        if (!r.photos && !r.videos) {
            box.innerHTML = '<section class="k-card bu-error" data-nofold><p>Jeg fandt ingen billeder eller videoer i den mappe. Vælg mappen, der indeholder dine billeder.</p></section>';
            return;
        }
        render(r, box);
    }

    document.addEventListener('change', function (e) {
        if (e.target && e.target.id === 'bi-folder') handle(Array.prototype.slice.call(e.target.files));
    });
    ['dragover', 'drop'].forEach(function (ev) {
        document.addEventListener(ev, function (e) {
            if (!(e.target && e.target.closest && e.target.closest('#bi-drop'))) return;
            e.preventDefault();
            if (ev !== 'drop') return;
            var out = [], entries = Array.prototype.map.call(e.dataTransfer.items || [], function (i) { return i.webkitGetAsEntry && i.webkitGetAsEntry(); }).filter(Boolean);
            Promise.all(entries.map(function (en) { return walk(en, '', out); })).then(function () { handle(out); });
        });
    });

    window.tjekBilleder = { exifYear: exifYear, analyse: analyse };   // for tests
})();
