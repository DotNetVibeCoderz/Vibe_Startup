/**
 * Ngibrid Logistics - Client-side JavaScript
 * Leaflet maps, auth POST helpers, scroll utilities.
 * Charts live in ngibrid-charts.js (D3.js).
 */
window.ngibrid = {
    // ─── Leaflet Map ───
    _map: null,
    _markers: [],
    _currentMarker: null,
    _destMarker: null,

    // Keyed by container id so several maps can live on one page and each can be torn down
    // independently — the tracking and route maps predate this and use their own fields.
    _maps: {},

    _escape: function (s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    },

    _tiles: function (map) {
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap | Ngibrid Logistics', maxZoom: 19
        }).addTo(map);
    },

    /**
     * Generic point map: warehouses, lockers, couriers — anything with coordinates.
     * Popup content is assembled here from plain fields, never from server-built HTML,
     * so a warehouse called "<script>" cannot inject markup.
     */
    renderPointsMap: function (data) {
        const el = document.getElementById(data.mapId);
        if (!el) return false;

        const points = (data.points || []).filter(p => p && isFinite(p.lat) && isFinite(p.lng));
        if (!points.length) return false;

        if (this._maps[data.mapId]) { this._maps[data.mapId].remove(); delete this._maps[data.mapId]; }

        const map = L.map(data.mapId, { scrollWheelZoom: false })
            .setView([points[0].lat, points[0].lng], data.zoom || 11);
        this._maps[data.mapId] = map;
        this._tiles(map);

        const self = this;
        points.forEach(function (p) {
            const icon = L.divIcon({
                className: 'leaflet-point-icon',
                html: '<div style="background:' + (p.color || '#1877f2') + ';width:30px;height:30px;' +
                    'border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:15px;' +
                    'border:2px solid white;box-shadow:0 2px 8px rgba(0,0,0,0.35);">' + (p.emoji || '📍') + '</div>',
                iconSize: [30, 30], iconAnchor: [15, 15]
            });
            const marker = L.marker([p.lat, p.lng], { icon: icon }).addTo(map);
            let html = '<strong>' + self._escape(p.title) + '</strong>';
            if (p.subtitle) html += '<br/><span style="color:#65676b;">' + self._escape(p.subtitle) + '</span>';
            (p.lines || []).forEach(function (line) { html += '<br/>' + self._escape(line); });
            marker.bindPopup(html);
        });

        if (points.length > 1) {
            map.fitBounds(L.latLngBounds(points.map(p => [p.lat, p.lng])).pad(0.25));
        }
        // The container is often still being laid out when Blazor calls this.
        setTimeout(function () { map.invalidateSize(); }, 300);
        return true;
    },

    disposeMap: function (mapId) {
        if (this._maps[mapId]) { this._maps[mapId].remove(); delete this._maps[mapId]; }
    },

    /**
     * Render tracking map with Leaflet
     */
    renderTrackingMap: function (data) {
        const el = document.getElementById(data.mapId);
        if (!el) return;

        // Cleanup existing map
        if (this._map) {
            this._map.remove();
            this._map = null;
        }
        this._markers = [];
        this._currentMarker = null;
        this._destMarker = null;

        // Create Leaflet map
        this._map = L.map(data.mapId).setView([data.centerLat, data.centerLng], 11);

        // Tile layer (OpenStreetMap)
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> | Ngibrid Logistics',
            maxZoom: 19
        }).addTo(this._map);

        // Add destination marker
        if (data.destination) {
            var destIcon = L.divIcon({
                className: 'leaflet-dest-icon',
                html: '<div style="background:#dc3545;color:white;border-radius:50%;width:30px;height:30px;display:flex;align-items:center;justify-content:center;font-size:14px;border:3px solid white;box-shadow:0 2px 8px rgba(0,0,0,0.3);">🏁</div>',
                iconSize: [30, 30],
                iconAnchor: [15, 15]
            });
            this._destMarker = L.marker([data.destination.lat, data.destination.lng], { icon: destIcon })
                .addTo(this._map)
                .bindPopup('<strong>Tujuan:</strong> ' + data.destination.city);
        }

        // Add history markers
        if (data.markers && data.markers.length > 0) {
            var points = data.markers.map(function (m) { return [m.lat, m.lng]; });

            // Draw polyline
            var polyline = L.polyline(points, {
                color: '#1877f2',
                weight: 3,
                opacity: 0.7,
                dashArray: '8, 8'
            }).addTo(this._map);

            // Add markers for significant events
            data.markers.forEach(function (m, i) {
                if (i === 0 || i === data.markers.length - 1 || m.type !== 'GPS_UPDATE') {
                    var colors = { 'PICKUP': '#31a24c', 'DELIVERY': '#dc3545', 'STATUS_CHANGE': '#f59e0b' };
                    var bgColor = colors[m.type] || '#1877f2';
                    var icon = L.divIcon({
                        className: 'leaflet-marker-icon',
                        html: '<div style="background:' + bgColor + ';color:white;border-radius:50%;width:20px;height:20px;display:flex;align-items:center;justify-content:center;font-size:10px;border:2px solid white;box-shadow:0 1px 4px rgba(0,0,0,0.3);">' + (i + 1) + '</div>',
                        iconSize: [20, 20],
                        iconAnchor: [10, 10]
                    });
                    var marker = L.marker([m.lat, m.lng], { icon: icon })
                        .addTo(this._map)
                        .bindPopup('<strong>' + m.type + '</strong><br/>' + m.time + '<br/>Speed: ' + m.speed + ' km/h');
                    this._markers.push(marker);
                }
            });

            // Fit bounds
            if (points.length > 1) {
                this._map.fitBounds(polyline.getBounds().pad(0.2));
            }
        }

        // Add current position marker
        if (data.current) {
            this.addCurrentMarker(data.current.lat, data.current.lng);
        }

        // Invalidate size after render
        setTimeout(function () {
            if (window.ngibrid._map) window.ngibrid._map.invalidateSize();
        }, 300);
    },

    /**
     * Add/update current position marker (pulsing animation)
     */
    addCurrentMarker: function (lat, lng) {
        if (!this._map) return;
        if (this._currentMarker) this._map.removeLayer(this._currentMarker);

        var icon = L.divIcon({
            className: 'leaflet-current-icon',
            html: '<div style="background:#1877f2;color:white;border-radius:50%;width:32px;height:32px;display:flex;align-items:center;justify-content:center;font-size:16px;border:3px solid white;box-shadow:0 2px 12px rgba(24,119,242,0.6);animation:leaflet-pulse 1.5s infinite;">📍</div>',
            iconSize: [32, 32],
            iconAnchor: [16, 16]
        });

        this._currentMarker = L.marker([lat, lng], { icon: icon, zIndexOffset: 1000 })
            .addTo(this._map)
            .bindPopup('<strong>📍 Posisi Terkini</strong>');
    },

    /**
     * Update current marker position
     */
    updateCurrentMarker: function (lat, lng) {
        if (this._currentMarker) {
            this._currentMarker.setLatLng([lat, lng]);
        } else {
            this.addCurrentMarker(lat, lng);
        }
    },

    /**
     * Render a static route polyline (courier route planner).
     */
    renderRouteMap: function (data) {
        const el = document.getElementById(data.mapId);
        if (!el || !data.stops || data.stops.length === 0) return;

        if (this._routeMap) { this._routeMap.remove(); this._routeMap = null; }
        this._routeMap = L.map(data.mapId).setView([data.stops[0].lat, data.stops[0].lng], 11);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap | Ngibrid Logistics', maxZoom: 19
        }).addTo(this._routeMap);

        const points = data.stops.map(s => [s.lat, s.lng]);
        const line = L.polyline(points, { color: '#1877f2', weight: 4, opacity: 0.8 }).addTo(this._routeMap);

        data.stops.forEach((s, i) => {
            const isStart = i === 0;
            const icon = L.divIcon({
                className: 'leaflet-route-icon',
                html: '<div style="background:' + (isStart ? '#31a24c' : '#1877f2') +
                    ';color:white;border-radius:50%;width:26px;height:26px;display:flex;align-items:center;' +
                    'justify-content:center;font-size:11px;font-weight:700;border:2px solid white;' +
                    'box-shadow:0 1px 6px rgba(0,0,0,0.3);">' + (isStart ? '🏠' : i) + '</div>',
                iconSize: [26, 26], iconAnchor: [13, 13]
            });
            L.marker([s.lat, s.lng], { icon: icon }).addTo(this._routeMap)
                .bindPopup('<strong>#' + i + ' ' + (s.label || '') + '</strong><br/>' +
                    (s.city || '') + (s.eta ? '<br/>ETA: ' + s.eta : ''));
        });

        this._routeMap.fitBounds(line.getBounds().pad(0.2));
        setTimeout(() => { if (window.ngibrid._routeMap) window.ngibrid._routeMap.invalidateSize(); }, 300);
    },

    _routeMap: null,

    // ─── Charts (delegates to D3 in ngibrid-charts.js) ───
    renderCharts: function (data) {
        if (!window.ngibridCharts) return;
        window.ngibridCharts.lineChart('deliveryChart', data.deliveryData || {}, { label: 'Order' });
        window.ngibridCharts.donutChart('statusChart', data.statusData || {});
    },

    // ─── Auth ───
    /**
     * POST credentials to the auth API. Sign-in must happen on a real HTTP request —
     * a Blazor circuit cannot write the auth cookie — so the page calls this and reloads.
     */
    postAuth: async function (url, payload) {
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'same-origin',
                body: JSON.stringify(payload)
            });
            const data = await response.json().catch(() => ({}));
            return {
                success: response.ok && data.success !== false,
                message: data.message || (response.ok ? 'Berhasil.' : 'Terjadi kesalahan.'),
                redirectUrl: data.redirectUrl || null
            };
        } catch (err) {
            return { success: false, message: 'Tidak dapat menghubungi server: ' + err.message, redirectUrl: null };
        }
    },

    navigate: function (url) {
        window.location.href = url;
    },

    // ─── Utilities ───
    scrollToBottom: function (selector) {
        const el = document.querySelector('.' + selector) || document.getElementById(selector);
        if (el) el.scrollTop = el.scrollHeight;
    },

    /** Open printable HTML (invoice / shipping label) in a new window. */
    printHtml: function (html) {
        const win = window.open('', '_blank');
        if (!win) return false;
        win.document.write(html);
        win.document.close();
        win.focus();
        setTimeout(() => win.print(), 400);
        return true;
    },

    downloadText: function (fileName, mimeType, content) {
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = fileName;
        document.body.appendChild(a); a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    copyToClipboard: async function (text) {
        try { await navigator.clipboard.writeText(text); return true; }
        catch { return false; }
    },

    /** Apply the saved theme before Blazor renders, avoiding a flash of the wrong theme. */
    applyTheme: function (isDark) {
        document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
        try { localStorage.setItem('ngibrid-theme', isDark ? 'dark' : 'light'); } catch { }
        if (window.ngibridCharts) window.ngibridCharts.refreshAll();
    },

    /** Reports the theme actually applied to the document, which may have come from
     *  prefers-color-scheme rather than localStorage — the server needs the effective value. */
    getStoredTheme: function () {
        return document.documentElement.getAttribute('data-theme') || 'light';
    }
};

// Restore the theme immediately on load.
(function () {
    let stored = null;
    try { stored = localStorage.getItem('ngibrid-theme'); } catch { }
    if (!stored && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) stored = 'dark';
    document.documentElement.setAttribute('data-theme', stored === 'dark' ? 'dark' : 'light');
})();

// Add pulse animation
var style = document.createElement('style');
style.textContent = '@keyframes leaflet-pulse { 0% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.3); opacity: 0.7; } 100% { transform: scale(1); opacity: 1; } }';
document.head.appendChild(style);

console.log('🚚 Ngibrid Logistics JS + Leaflet initialized');
