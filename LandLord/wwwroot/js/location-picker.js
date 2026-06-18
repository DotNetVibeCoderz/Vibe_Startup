/**
 * LandLord Location Picker — Leaflet.js + Polygon Drawing + Geocoding
 * Dependency: Leaflet 1.9.x + Leaflet Draw 1.0.x
 */
(function () {
    'use strict';

    let map = null;
    let drawnLayer = null;          // FeatureGroup untuk polygon & marker
    let drawControl = null;
    let currentMode = 'polygon';    // 'polygon' | 'point'
    let dotNetRef = null;
    let geocoderMarker = null;

    // ================================================================
    // INIT — Dipanggil dari Blazor
    // ================================================================

    window.LocationPicker = {
        /**
         * Inisialisasi peta di dalam container element.
         * @param {string} containerId   - ID element div
         * @param {object} dotNetHelper  - .NET reference untuk callback
         * @param {string} initialGeoJson - GeoJSON polygon/point yang sudah ada (edit mode)
         * @param {number} lat           - Center latitude (default Jakarta)
         * @param {number} lng           - Center longitude
         * @param {number} zoom          - Zoom level
         */
        init: function (containerId, dotNetHelper, initialGeoJson, lat, lng, zoom) {
            // Pastikan Leaflet sudah loaded
            if (typeof L === 'undefined') {
                console.error('Leaflet not loaded. Make sure to include Leaflet CSS & JS.');
                return;
            }

            dotNetRef = dotNetHelper;

            // Hancurkan peta lama kalau ada
            if (map) {
                map.remove();
                map = null;
            }

            // Tile layer — OpenStreetMap (gratis, no API key)
            map = L.map(containerId, {
                center: [lat || -6.2088, lng || 106.8456],
                zoom: zoom || 16,
                zoomControl: true
            });

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> | LandLord',
                maxZoom: 21
            }).addTo(map);

            // Satellite layer sebagai opsi (ESRI World Imagery)
            const satelliteLayer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
                attribution: '&copy; Esri',
                maxZoom: 20
            });

            // Layer control
            const baseMaps = {
                "🗺️ Peta": L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 21 }),
                "🛰️ Satelit": satelliteLayer
            };
            baseMaps["🗺️ Peta"].addTo(map);
            L.control.layers(baseMaps, null, { position: 'topright' }).addTo(map);

            // FeatureGroup untuk hasil drawing
            drawnLayer = L.featureGroup().addTo(map);

            // Draw control
            drawControl = new L.Control.Draw({
                position: 'topleft',
                draw: {
                    polygon: {
                        allowIntersection: false,
                        showArea: true,
                        shapeOptions: { color: '#ff6b6b', weight: 3, fillOpacity: 0.2 },
                        metric: true,
                        repeatMode: false
                    },
                    rectangle: {
                        shapeOptions: { color: '#ff6b6b', weight: 3, fillOpacity: 0.2 },
                        metric: true
                    },
                    marker: {
                        icon: L.icon({
                            iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
                            iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
                            shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
                            iconSize: [25, 41],
                            iconAnchor: [12, 41]
                        })
                    },
                    polyline: false,
                    circle: false,
                    circlemarker: false
                },
                edit: {
                    featureGroup: drawnLayer,
                    remove: true
                }
            });
            map.addControl(drawControl);

            // Geocoder search control
            addGeocoder();

            // Load existing GeoJSON jika ada (edit mode)
            if (initialGeoJson && initialGeoJson !== '') {
                loadExistingGeometry(initialGeoJson);
            }

            // Event: setelah draw selesai → kirim ke .NET
            map.on(L.Draw.Event.CREATED, function (e) {
                drawnLayer.clearLayers();
                drawnLayer.addLayer(e.layer);
                notifyDotNet();
            });

            map.on(L.Draw.Event.EDITED, function () {
                notifyDotNet();
            });

            map.on(L.Draw.Event.DELETED, function () {
                notifyDotNet();
            });

            // Invalidate size setelah render
            setTimeout(() => map.invalidateSize(), 300);
        },

        /** Set mode: 'polygon' atau 'point' */
        setMode: function (mode) {
            currentMode = mode;
        },

        /** Hapus semua layer hasil draw */
        clearAll: function () {
            if (drawnLayer) drawnLayer.clearLayers();
            if (geocoderMarker) { map.removeLayer(geocoderMarker); geocoderMarker = null; }
            notifyDotNet();
        },

        /** Dapatkan GeoJSON dari layers yang di-draw */
        getGeoJson: function () {
            if (!drawnLayer || drawnLayer.getLayers().length === 0) return null;

            const geojson = drawnLayer.toGeoJSON();
            return JSON.stringify(geojson);
        },

        /** Dapatkan center point & zoom */
        getCenter: function () {
            if (!map) return null;
            const c = map.getCenter();
            return JSON.stringify({ lat: c.lat, lng: c.lng, zoom: map.getZoom() });
        },

        /** Fly ke lokasi */
        flyTo: function (lat, lng, zoom) {
            if (map) map.flyTo([lat, lng], zoom || 17, { duration: 1.5 });
        },

        /** Hancurkan peta */
        destroy: function () {
            if (map) { map.remove(); map = null; }
            drawnLayer = null;
            dotNetRef = null;
        }
    };

    // ================================================================
    // LOAD EXISTING GEOMETRY
    // ================================================================

    function loadExistingGeometry(geoJsonStr) {
        try {
            const geojson = JSON.parse(geoJsonStr);
            if (!geojson || !geojson.type) return;

            const layer = L.geoJSON(geojson, {
                style: { color: '#ff6b6b', weight: 3, fillOpacity: 0.2 },
                pointToLayer: function (feature, latlng) {
                    return L.marker(latlng, {
                        icon: L.icon({
                            iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
                            iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
                            shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
                            iconSize: [25, 41], iconAnchor: [12, 41]
                        })
                    });
                }
            });

            drawnLayer.clearLayers();
            drawnLayer.addLayer(layer);

            // Fit bounds
            if (drawnLayer.getBounds().isValid()) {
                map.fitBounds(drawnLayer.getBounds().pad(0.1));
            }
        } catch (e) {
            console.warn('Failed to parse existing GeoJSON:', e);
        }
    }

    // ================================================================
    // GEOCODER — Search by location name (Nominatim, free)
    // ================================================================

    function addGeocoder() {
        const GeocoderControl = L.Control.extend({
            onAdd: function () {
                const container = L.DomUtil.create('div', 'll-geocoder-container');
                container.innerHTML = `
                    <div class="ll-geocoder">
                        <input type="text" class="ll-geocoder-input" placeholder="🔍 Cari lokasi..." />
                        <div class="ll-geocoder-results"></div>
                    </div>
                `;

                // Prevent map click propagation
                L.DomEvent.disableClickPropagation(container);
                L.DomEvent.disableScrollPropagation(container);

                const input = container.querySelector('.ll-geocoder-input');
                const results = container.querySelector('.ll-geocoder-results');

                let debounceTimer;
                input.addEventListener('input', function () {
                    clearTimeout(debounceTimer);
                    const q = input.value.trim();
                    if (q.length < 3) { results.innerHTML = ''; return; }

                    debounceTimer = setTimeout(() => searchLocation(q, results), 400);
                });

                // Hide results on blur (with delay for click)
                input.addEventListener('blur', () => {
                    setTimeout(() => { results.innerHTML = ''; }, 200);
                });

                return container;
            }
        });

        new GeocoderControl({ position: 'topright' }).addTo(map);
    }

    async function searchLocation(query, resultsContainer) {
        resultsContainer.innerHTML = '<div class="ll-geocoder-item">🔍 Mencari...</div>';
        try {
            const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=5&countrycodes=id`;
            const resp = await fetch(url);
            const data = await resp.json();

            if (!data || data.length === 0) {
                resultsContainer.innerHTML = '<div class="ll-geocoder-item">❌ Tidak ditemukan</div>';
                return;
            }

            resultsContainer.innerHTML = data.map((r, i) =>
                `<div class="ll-geocoder-item" data-lat="${r.lat}" data-lng="${r.lon}" data-name="${r.display_name}">
                    📍 ${r.display_name.substring(0, 80)}${r.display_name.length > 80 ? '...' : ''}
                </div>`
            ).join('');

            // Click handler
            resultsContainer.querySelectorAll('.ll-geocoder-item').forEach(item => {
                item.addEventListener('click', function () {
                    const lat = parseFloat(this.dataset.lat);
                    const lng = parseFloat(this.dataset.lng);
                    const name = this.dataset.name;

                    // Hapus marker geocoder sebelumnya
                    if (geocoderMarker) map.removeLayer(geocoderMarker);

                    // Tambah marker sementara
                    geocoderMarker = L.marker([lat, lng], {
                        icon: L.icon({
                            iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
                            iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
                            shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
                            iconSize: [25, 41], iconAnchor: [12, 41]
                        })
                    }).addTo(map).bindPopup(`📍 <strong>${name}</strong>`).openPopup();

                    map.flyTo([lat, lng], 17, { duration: 1.5 });
                    resultsContainer.innerHTML = '';

                    // Kirim ke .NET
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnLocationSearched', lat, lng, name);
                    }
                });
            });
        } catch (e) {
            resultsContainer.innerHTML = '<div class="ll-geocoder-item">❌ Gagal mencari</div>';
        }
    }

    // ================================================================
    // NOTIFY .NET
    // ================================================================

    function notifyDotNet() {
        if (!dotNetRef) return;
        const geoJson = window.LocationPicker.getGeoJson();
        const center = window.LocationPicker.getCenter();
        dotNetRef.invokeMethodAsync('OnGeometryChanged', geoJson, center);
    }
})();
