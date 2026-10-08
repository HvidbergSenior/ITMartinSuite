// Tests for the free Budget's rules (wwwroot/budget/budget-domain.js). Run: node --test ITMartinHjem.Server/js-tests
const test = require('node:test');
const assert = require('node:assert/strict');
const B = require('../wwwroot/budget/budget-domain.js');

const enc = (s) => new TextEncoder().encode(s).buffer;
const latin1 = (s) => Uint8Array.from(s, (c) => c.charCodeAt(0)).buffer;

const danske = [
    '"Dato";"Tekst";"Beløb";"Saldo";"Status";"Afstemt"',
    '"02.04.2026";"MobilePay Peter";"-300,00";"10.000,00";"Udført";"Nej"',
    '"03.04.2026";"Netflix.com Dankort-nota 03.04";"-139,00";"9.861,00";"Udført";"Nej"',
    '"03.05.2026";"Netflix.com Dankort-nota 03.05";"-139,00";"9.722,00";"Udført";"Nej"',
    '"03.06.2026";"Netflix.com Dankort-nota 03.06";"-139,00";"9.583,00";"Udført";"Nej"',
    '"10.04.2026";"Netto 1234";"-1.234,50";"8.000,00";"Udført";"Nej"',
    '"30.04.2026";"Løn";"25.000,00";"33.000,00";"Udført";"Nej"',
    '"01.05.2026";"Overført til opsparing";"-5.000,00";"28.000,00";"Udført";"Nej"',
    '"12.05.2026";"Vdk Mob.Pay*Anne";"-99,00";"27.901,00";"Udført";"Nej"',
].join('\n');

test('Danske Bank: header columns, decimal comma, thousands dot', () => {
    const list = B.readTransactions(B.decode(enc(danske)));
    assert.equal(list.length, 8, "8 rows under the header");
    assert.equal(list.find((t) => t.text.startsWith('Netto')).amount, -1234.5);
    assert.equal(list.find((t) => t.text === 'Løn').amount, 25000);
});

test('Nordea: year first with slashes, several text columns joined', () => {
    const csv = 'Bogføringsdato;Beløb;Afsender;Modtager;Navn;Beskrivelse;Saldo;Valuta\n' +
        '2026/04/02;-300,5;123;;MobilePay;MobilePay Peter;1000;DKK\n2026/04/05;-45;123;;Rema 1000;Rema 1000 Aarhus;900;DKK\n2026/05/01;20000;;123;Arbejdsgiver;Løn;20900;DKK';
    const list = B.readTransactions(B.decode(enc(csv)));
    assert.equal(list.length, 3);
    assert.equal(list[0].amount, -300.5);
    assert.match(list[1].text, /Rema 1000 · Rema 1000 Aarhus/);
});

test('Lunar: commas between columns, decimal point', () => {
    const csv = 'Date,Text,Amount,Currency\n2026-04-02,"Spotify",-109.00,DKK\n2026-05-02,"Spotify",-109.00,DKK\n2026-05-03,"Wolt",-245.50,DKK';
    const list = B.readTransactions(B.decode(enc(csv)));
    assert.deepEqual(list.map((t) => t.amount), [-109, -109, -245.5]);
});

test('A raw export without a header, in Windows-1252 (æøå), is read by its contents', () => {
    const raw = '1234567890;02.04.2026;02.04.2026;Kortk\xf8b F\xf8tex;-512,30;4.000,00\n' +
        '1234567890;05.04.2026;05.04.2026;MobilePay K\xe5re;-50,00;3.950,00\n1234567890;06.04.2026;06.04.2026;L\xf8n;10.000,00;13.950,00\n';
    const list = B.readTransactions(B.decode(latin1(raw)));
    assert.deepEqual(list.map((t) => [t.text, t.amount]), [['Kortkøb Føtex', -512.3], ['MobilePay Kåre', -50], ['Løn', 10000]]);
});

test('Amounts as Danish banks write them', () => {
    assert.equal(B.parseAmount('-1.234,56'), -1234.56);
    assert.equal(B.parseAmount('1,234.56'), 1234.56);
    assert.equal(B.parseAmount('1.234'), 1234);
    assert.equal(B.parseAmount('250-'), -250);
    assert.equal(B.parseAmount('−99,00 kr'), -99);
    assert.equal(B.parseAmount('abc'), null);
});

test('Dates in the usual shapes, and impossible dates refused', () => {
    assert.equal(B.parseDate('31.12.2026').getMonth(), 11);
    assert.equal(B.parseDate('05-04-26').getFullYear(), 2026);
    assert.equal(B.parseDate('2026-04-05').getDate(), 5);
    assert.equal(B.parseDate('31.02.2026'), null);
});

test('Analysis: MobilePay counted, own transfers left out, subscriptions found', () => {
    const a = B.analyse(B.readTransactions(B.decode(enc(danske))));
    assert.equal(a.mp.n, 2);
    assert.equal(a.mp.sum, 399);
    assert.equal(a.income, 25000);
    assert.equal(a.spent, 300 + 139 * 3 + 1234.5 + 99, 'the 5.000 to savings is not spending');
    assert.equal(a.grocery, 1234.5);
    assert.equal(a.subs.length, 1);
    assert.equal(a.subs[0].amount, 139);
    assert.equal(a.monthCount, 3);
});

test('A shop name is cleaned of card words and dates', () => {
    assert.equal(B.clean('Vdk Netflix.com 12.05'), 'netflix.com');
    assert.equal(B.clean('Netflix.com Dankort-nota 13.06'), 'netflix.com');
});

test('A file without dates and amounts gives nothing to show', () => {
    assert.deepEqual(B.readTransactions('hej;med;dig\nikke;en;bank'), null);
});
