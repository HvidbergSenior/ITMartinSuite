(function () {
    var token = localStorage.getItem("budget_quick_token");

    function show(id) {
        ["q-denied", "q-loading", "q-content"].forEach(function (x) {
            document.getElementById(x).style.display = x === id ? "" : "none";
        });
    }

    function kr(n) {
        return Math.round(n).toLocaleString("da-DK") + " kr.";
    }

    if (!token) {
        show("q-denied");
        return;
    }

    Promise.all([
        fetch("/api/quick/overview?token=" + encodeURIComponent(token)),
        fetch("/api/quick/subscriptions?token=" + encodeURIComponent(token))
    ]).then(function (responses) {
        if (responses[0].status === 401 || responses[1].status === 401) {
            localStorage.removeItem("budget_quick_token");
            show("q-denied");
            return null;
        }
        return Promise.all(responses.map(function (r) { return r.json(); }));
    }).then(function (data) {
        if (!data) return;
        var overview = data[0];
        var subs = data[1];

        document.getElementById("q-month").textContent = overview.month;
        document.getElementById("q-income").textContent = kr(overview.income);
        document.getElementById("q-expenses").textContent = kr(overview.expenses);
        document.getElementById("q-net").textContent = kr(overview.net);
        document.getElementById("q-net-label").textContent = overview.net >= 0 ? "Overskud" : "Underskud";

        var subsEl = document.getElementById("q-subs");
        if (subs.length === 0) {
            subsEl.innerHTML = '<div class="quick-subs-empty">Ingen faste, tilbagevendende beløb fundet endnu.</div>';
        } else {
            subsEl.innerHTML = subs.map(function (s) {
                var stale = s.daysSinceLastCharge > 45;
                return '<div class="quick-sub-row' + (stale ? ' quick-sub-row--stale' : '') + '">' +
                    '<div class="quick-sub-desc">' + escapeHtml(s.sampleDescription || "Ukendt") + '</div>' +
                    '<div class="quick-sub-meta">' + s.intervalLabel + ' · sidst ' + s.daysSinceLastCharge + ' dage siden</div>' +
                    '<div class="quick-sub-amount">' + kr(s.amount) + '</div>' +
                    '</div>';
            }).join("");
        }

        show("q-content");
    }).catch(function () {
        show("q-denied");
    });

    function escapeHtml(s) {
        var d = document.createElement("div");
        d.textContent = s;
        return d.innerHTML;
    }
})();
