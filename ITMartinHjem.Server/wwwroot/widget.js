// Martin's "Lige nu" + chat bubble for other sites (itmartin.dk). Usage:
//   <div id="martin-nu"></div>                         -> the live "Lige nu" box
//   <script src="https://martin.itmartin.dk/widget.js" data-chat defer></script>
//   (data-chat adds the 💬 bubble in the corner; leave it out for the box only)
(function () {
    var BASE = 'https://martin.itmartin.dk';
    var me = document.currentScript;
    var wantChat = me && me.hasAttribute('data-chat');

    var css = '' +
        '.mw-box{font:inherit;color:inherit;border:1px solid rgba(0,0,0,.12);border-left:4px solid #92400e;border-radius:14px;padding:16px 18px;background:#fff;box-shadow:0 1px 3px rgba(0,0,0,.06)}' +
        '.mw-tag{display:inline-block;font-size:12px;font-weight:700;letter-spacing:.05em;text-transform:uppercase;color:#92400e;background:#fbeedd;border-radius:999px;padding:2px 10px}' +
        '.mw-text{font-size:1.05rem;line-height:1.5;margin-top:8px}.mw-text p{margin:0 0 8px}.mw-text ul{list-style:none;padding:0;margin:0 0 10px;display:grid;gap:6px}' +
        '.mw-media{margin-top:10px}.mw-media img,.mw-media video{width:100%;max-height:60vh;object-fit:contain;border-radius:10px;display:block;background:#f1f1f1}' +
        '.mw-embed{position:relative;aspect-ratio:16/9}.mw-embed iframe{position:absolute;inset:0;width:100%;height:100%;border:0;border-radius:10px}' +
        '.mw-foot{display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-top:10px;font-size:.9rem;color:#667}' +
        '.mw-btn{display:inline-block;padding:9px 14px;border-radius:10px;background:#92400e;color:#fff!important;text-decoration:none;font-weight:600;border:0;cursor:pointer;font:inherit}' +
        '.mw-btn.ghost{background:transparent;color:#92400e!important;border:1px solid #92400e}' +
        '.mw-dot{display:inline-block;width:10px;height:10px;border-radius:50%;background:#b8bec3;margin-right:6px;vertical-align:middle}.mw-dot.on{background:#1f9d55;box-shadow:0 0 0 4px rgba(31,157,85,.18)}' +
        '.mw-bubble{position:fixed;right:16px;bottom:16px;z-index:9999;border-radius:999px;padding:12px 18px;box-shadow:0 4px 14px rgba(0,0,0,.2)}' +
        '.mw-panel{position:fixed;right:16px;bottom:76px;z-index:9999;width:370px;max-width:calc(100vw - 32px);height:560px;max-height:calc(100vh - 100px);border:0;border-radius:14px;box-shadow:0 8px 30px rgba(0,0,0,.25);background:#fff;display:none}' +
        '.mw-panel.open{display:block}';
    var st = document.createElement('style');
    st.textContent = css;
    document.head.appendChild(st);

    var chatOpen = function () { window.open(BASE + '/#chat', '_blank'); };
    var dotEls = [];

    function esc(s) { var d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; }

    function renderBox(el, d) {
        var media = '';
        if (d.player) media = '<div class="mw-media mw-embed"><iframe src="' + esc(d.player) + '" allow="autoplay; encrypted-media; picture-in-picture; fullscreen" allowfullscreen loading="lazy" title="Video"></iframe></div>';
        else if (d.media && d.media.type === 'video') media = '<div class="mw-media"><video src="' + esc(d.media.url) + '" controls preload="metadata" playsinline></video></div>';
        else if (d.media) media = '<div class="mw-media"><img src="' + esc(d.media.thumb || d.media.url) + '" alt="Lige nu" loading="lazy"></div>';
        el.innerHTML =
            '<div class="mw-box"><span class="mw-tag">Lige nu</span>' + media +
            '<div class="mw-text">' + d.html + '</div>' +   // server-side encoded (TinyMarkdown)
            (d.link ? '<p><a class="mw-btn" href="' + esc(d.link) + '" target="_blank" rel="noopener">▶ Se med</a></p>' : '') +
            '<div class="mw-foot"><span><span class="mw-dot' + (d.available ? ' on' : '') + '"></span>' +
            (d.available ? esc(first(d)) + ' er her nu' : esc(first(d)) + ' er ikke ved skærmen – skriv alligevel') + '</span>' +
            '<button class="mw-btn" type="button" data-mw-chat>💬 Skriv til ' + esc(first(d)) + '</button>' +
            '<a class="mw-btn ghost" href="' + BASE + '" target="_blank" rel="noopener">Se min tidslinje</a>' +
            '<span>Opdateret ' + esc(d.updated) + '</span></div></div>';
        el.querySelector('[data-mw-chat]').addEventListener('click', function () { chatOpen(); });
        dotEls.push(el.querySelector('.mw-dot'));
    }

    function first(d) { return (d.name || 'Martin').split(' ')[0]; }

    function addBubble(d) {
        var panel = document.createElement('iframe');
        panel.className = 'mw-panel';
        panel.title = 'Skriv til ' + first(d);
        var btn = document.createElement('button');
        btn.className = 'mw-btn mw-bubble';
        btn.type = 'button';
        btn.innerHTML = '<span class="mw-dot' + (d.available ? ' on' : '') + '"></span>💬 Skriv til ' + esc(first(d));
        dotEls.push(btn.querySelector('.mw-dot'));
        document.body.appendChild(panel);
        document.body.appendChild(btn);
        chatOpen = function () {
            if (!panel.src) panel.src = BASE + '/chat';
            panel.classList.toggle('open');
        };
        btn.addEventListener('click', function () { chatOpen(); });
    }

    function load() {
        return fetch(BASE + '/api/nu').then(function (r) { return r.json(); });
    }

    load().then(function (d) {
        document.querySelectorAll('#martin-nu, [data-martin-nu]').forEach(function (el) { renderBox(el, d); });
        if (wantChat) addBubble(d);
        // Keep the green dot honest while the page stays open.
        setInterval(function () {
            load().then(function (n) { dotEls.forEach(function (x) { if (x) x.classList.toggle('on', !!n.available); }); }).catch(function () { });
        }, 60000);
    }).catch(function () { /* the host page just shows without the box */ });
})();
