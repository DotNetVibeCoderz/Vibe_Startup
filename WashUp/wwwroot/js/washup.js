// WashUp JavaScript helpers
window.washup = {
    scrollToBottom: function (containerId) {
        var container = document.getElementById(containerId);
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    },

    setTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try { localStorage.setItem('washup-theme', theme); } catch (e) { }
    },

    getTheme: function () {
        try { return localStorage.getItem('washup-theme') || 'light'; } catch (e) { return 'light'; }
    },

    // Buka jendela print (untuk export PDF via "Save as PDF")
    print: function () {
        window.print();
    },

    // Unduh file dari URL (dipakai tombol export laporan)
    download: function (url) {
        var a = document.createElement('a');
        a.href = url;
        a.download = '';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    },

    // ---------- Peta kurir (Leaflet + OpenStreetMap) ----------
    _maps: {},

    courierMapAvailable: function () {
        return typeof L !== 'undefined';
    },

    // couriers: [{ id, name, lat, lng, destLat, destLng, status, eta }]
    updateCourierMap: function (elementId, couriers) {
        if (typeof L === 'undefined') return false;
        var el = document.getElementById(elementId);
        if (!el) return false;

        var state = window.washup._maps[elementId];
        if (!state || state.container !== el) {
            // Buat peta baru (elemen bisa dibuat ulang oleh Blazor)
            if (state && state.map) { try { state.map.remove(); } catch (e) { } }
            var map = L.map(el, { zoomControl: true, attributionControl: true });
            L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            }).addTo(map);
            map.setView([-6.2088, 106.8456], 12); // default Jakarta
            state = { map: map, container: el, layers: {}, fitted: false };
            window.washup._maps[elementId] = state;
        }

        var map = state.map;
        var seen = {};
        var bounds = [];

        (couriers || []).forEach(function (c) {
            seen[c.id] = true;
            bounds.push([c.lat, c.lng]);
            if (c.destLat != null) bounds.push([c.destLat, c.destLng]);

            var layer = state.layers[c.id];
            var popup = '<strong>🛵 ' + c.name + '</strong><br/>' + (c.status || '') + (c.eta ? '<br/>ETA: ' + c.eta : '');

            if (!layer) {
                var courierIcon = L.divIcon({ className: 'map-emoji-icon', html: '🛵', iconSize: [30, 30], iconAnchor: [15, 15] });
                var destIcon = L.divIcon({ className: 'map-emoji-icon dest', html: '🏁', iconSize: [26, 26], iconAnchor: [13, 13] });
                layer = {
                    marker: L.marker([c.lat, c.lng], { icon: courierIcon, title: c.name }).addTo(map).bindPopup(popup),
                    dest: c.destLat != null ? L.marker([c.destLat, c.destLng], { icon: destIcon }).addTo(map) : null,
                    route: c.destLat != null ? L.polyline([[c.lat, c.lng], [c.destLat, c.destLng]], { color: '#7c3aed', weight: 2.5, dashArray: '6 6', opacity: 0.75 }).addTo(map) : null,
                    trail: L.polyline([[c.lat, c.lng]], { color: '#7c3aed', weight: 3, opacity: 0.45 }).addTo(map)
                };
                state.layers[c.id] = layer;
            } else {
                layer.marker.setLatLng([c.lat, c.lng]).setPopupContent(popup);
                if (layer.route && c.destLat != null) layer.route.setLatLngs([[c.lat, c.lng], [c.destLat, c.destLng]]);
                var trail = layer.trail.getLatLngs();
                trail.push([c.lat, c.lng]);
                if (trail.length > 60) trail.shift();
                layer.trail.setLatLngs(trail);
            }
        });

        // Hapus kurir yang tugasnya sudah selesai
        Object.keys(state.layers).forEach(function (id) {
            if (!seen[id]) {
                var l = state.layers[id];
                [l.marker, l.dest, l.route, l.trail].forEach(function (x) { if (x) map.removeLayer(x); });
                delete state.layers[id];
            }
        });

        if (bounds.length > 0 && !state.fitted) {
            map.fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
            state.fitted = true;
        }
        return true;
    },

    fitCourierMap: function (elementId) {
        var state = window.washup._maps[elementId];
        if (!state) return;
        var bounds = [];
        Object.values(state.layers).forEach(function (l) {
            bounds.push(l.marker.getLatLng());
            if (l.dest) bounds.push(l.dest.getLatLng());
        });
        if (bounds.length > 0) state.map.fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
    },

    disposeCourierMap: function (elementId) {
        var state = window.washup._maps[elementId];
        if (state && state.map) { try { state.map.remove(); } catch (e) { } }
        delete window.washup._maps[elementId];
    }
};

// Kompatibilitas nama lama
window.scrollToBottom = window.washup.scrollToBottom;
window.setTheme = window.washup.setTheme;
window.getTheme = window.washup.getTheme;
