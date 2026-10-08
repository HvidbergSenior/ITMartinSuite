// Tests for Tjek dine billeder's rules (wwwroot/billeder/billeder-domain.js). Run: node --test ITMartinHjem.Server/js-tests
const test = require('node:test');
const assert = require('node:assert/strict');
const D = require('../wwwroot/billeder/billeder-domain.js');

// A minimal JPEG with EXIF DateTimeOriginal in the given year (little-endian TIFF inside APP1).
function jpeg(year, pad = 0) {
    const tiff = Buffer.alloc(200);
    tiff.write('II', 0); tiff.writeUInt16LE(42, 2); tiff.writeUInt32LE(8, 4);
    tiff.writeUInt16LE(1, 8); tiff.writeUInt16LE(0x8769, 10); tiff.writeUInt16LE(4, 12); tiff.writeUInt32LE(1, 14); tiff.writeUInt32LE(26, 18);
    tiff.writeUInt16LE(1, 26); tiff.writeUInt16LE(0x9003, 28); tiff.writeUInt16LE(2, 30); tiff.writeUInt32LE(20, 32); tiff.writeUInt32LE(60, 36);
    tiff.write(year + ':06:12 14:03:22', 60);
    const app1 = Buffer.concat([Buffer.from('Exif\0\0', 'binary'), tiff]);
    const hdr = Buffer.alloc(4); hdr.writeUInt16BE(0xffe1, 0); hdr.writeUInt16BE(app1.length + 2, 2);
    return Buffer.concat([Buffer.from([0xff, 0xd8]), hdr, app1, Buffer.alloc(pad, 7), Buffer.from([0xff, 0xd9])]);
}

// A File-like object: what the browser gives, as far as the rules use it.
class FakeFile {
    constructor(name, buf, folder = 'Billeder') { this.name = name; this.buf = buf; this.size = buf.length; this.webkitRelativePath = folder + '/' + name; }
    slice(a, e) { const s = this.buf.subarray(a, e); return { arrayBuffer: async () => s.buffer.slice(s.byteOffset, s.byteOffset + s.length) }; }
}

const ab = (b) => b.buffer.slice(b.byteOffset, b.byteOffset + b.length);

test('The year is read from EXIF', () => {
    assert.equal(D.exifYear(ab(jpeg(2019))), 2019);
    assert.equal(D.exifYear(ab(Buffer.from('not a jpeg'))), null);
});

test('Photos, videos, screenshots, chat pictures, duplicates and years are counted', async () => {
    const files = [
        new FakeFile('a.jpg', jpeg(2019, 40000)),
        new FakeFile('kopi af a.jpg', jpeg(2019, 40000)),           // same size AND content: a duplicate
        new FakeFile('b.jpg', jpeg(2021, 40000)),                   // same size, other content: NOT a duplicate
        new FakeFile('Screenshot_20200101.png', Buffer.alloc(50000, 1)),
        new FakeFile('IMG-20180505-WA0001.jpg', Buffer.alloc(40100, 2)),
        new FakeFile('film.mp4', Buffer.alloc(90000, 3)),
        new FakeFile('ikon.png', Buffer.alloc(800, 4)),             // tiny: an icon or thumbnail
        new FakeFile('noter.txt', Buffer.alloc(10)),
        new FakeFile('x.jpg', jpeg(2015), 'Billeder/thumbnails'),  // generated thumbnails are skipped
    ];
    const r = await D.analyse(files, () => {});
    assert.equal(r.photos, 6);
    assert.equal(r.videos, 1);
    assert.equal(r.other, 1);
    assert.equal(r.screenshots.n, 1);
    assert.equal(r.chat.n, 1);
    assert.equal(r.small.n, 1);
    assert.equal(r.dups.extra, 1, 'only the true copy');
    assert.deepEqual(r.years, { 2018: 1, 2019: 2, 2020: 1, 2021: 1 });
    assert.equal(r.undated, 2, 'the video and the icon have no date');
});

test('Progress is reported while reading', async () => {
    const seen = [];
    await D.analyse([new FakeFile('a.jpg', jpeg(2019))], (text) => seen.push(text));
    assert.ok(seen.some((t) => t.includes('datoer')));
});

test('A folder with no pictures has none', async () => {
    const r = await D.analyse([new FakeFile('a.txt', Buffer.alloc(5))], () => {});
    assert.equal(r.photos + r.videos, 0);
});

test('A year folder dates photos that carry no date themselves (2026-10-08: 171 of 1,009 were "undated" in Billeder/2015)', async () => {
    const r = await D.analyse([
        new FakeFile('emil 116.jpg', Buffer.alloc(40000, 9), '2015/7 Juli'),                // no EXIF, no date in the name
        new FakeFile('IMG_20170101_1200.jpg', Buffer.alloc(40000, 8), '2015/1 Januar'),      // the name's date wins over the folder
        new FakeFile('ferie.jpg', Buffer.alloc(40000, 7), 'Diverse'),                         // nothing to go by
    ], () => {});
    assert.deepEqual(r.years, { 2015: 1, 2017: 1 });
    assert.equal(r.undated, 1);
});
