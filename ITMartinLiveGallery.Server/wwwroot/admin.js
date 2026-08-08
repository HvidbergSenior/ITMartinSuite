var token = sessionStorage.getItem('gallery_admin_token') || '';

function unlock() {
  token = document.getElementById('tokenInput').value;
  sessionStorage.setItem('gallery_admin_token', token);
  showApp();
}

function showApp() {
  document.getElementById('gate').style.display = 'none';
  document.getElementById('app').style.display = 'block';
  loadEvents();
}

if (token) showApp();

function createEvent() {
  var slug = document.getElementById('slugInput').value.trim();
  var pin = document.getElementById('pinInput').value.trim();
  var title = document.getElementById('titleInput').value.trim();
  var status = document.getElementById('createStatus');
  if (!slug || !pin) { status.textContent = 'Slug og pin er påkrævet'; return; }

  fetch('/api/admin/events', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Admin-Token': token },
    body: JSON.stringify({ slug: slug, pin: pin, title: title }),
  })
    .then(function (r) {
      if (r.status === 401) { status.textContent = 'Forkert token'; return null; }
      if (r.status === 409) { status.textContent = 'Slug findes allerede'; return null; }
      if (!r.ok) { status.textContent = 'Noget gik galt'; return null; }
      return r.json();
    })
    .then(function (data) {
      if (!data) return;
      status.textContent = 'Oprettet! Link: ' + location.origin + data.url;
      document.getElementById('slugInput').value = '';
      document.getElementById('pinInput').value = '';
      document.getElementById('titleInput').value = '';
      loadEvents();
    });
}

function loadEvents() {
  fetch('/api/admin/events', { headers: { 'X-Admin-Token': token } })
    .then(function (r) { return r.status === 401 ? [] : r.json(); })
    .then(function (events) {
      var list = document.getElementById('eventList');
      if (!events.length) { list.innerHTML = '<p class="status">Ingen events endnu</p>'; return; }
      list.innerHTML = events.map(function (e) {
        var url = location.origin + '/' + e.slug + '?pin=' + e.pin;
        return '<div class="event-row">' +
          '<div><strong>' + escapeHtml(e.title) + '</strong><br>' +
          '<a href="' + url + '" target="_blank">' + url + '</a><br>' +
          e.photoCount + ' billede(r)</div>' +
          '<button onclick="deleteEvent(\'' + e.slug + '\')">Slet</button>' +
          '</div>';
      }).join('');
    });
}

function deleteEvent(slug) {
  if (!confirm('Slet "' + slug + '" og alle dets billeder? Kan ikke fortrydes.')) return;
  fetch('/api/admin/events/' + encodeURIComponent(slug), {
    method: 'DELETE',
    headers: { 'X-Admin-Token': token },
  }).then(loadEvents);
}

function escapeHtml(s) {
  return (s || '').replace(/[&<>"']/g, function (c) {
    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
  });
}
