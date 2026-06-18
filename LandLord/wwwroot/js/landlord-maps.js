/**
 * LandLord Maps v2 — Leaflet.js (NO Google Maps required)
 * Features: Markers, Polygons (GeoJSON), Fly-to, Highlight, Layer control
 * No API key needed — uses OpenStreetMap + ESRI Satellite
 */
window.LandLordMaps = (function () {
    'use strict';

    let map = null;
    let markerLayer = null;
    let polygonLayer = null;
    let highlightedLayer = null;
    let allFeatures = {};

    async function init(containerId, options, markers, polygons) {
        if (typeof L === 'undefined') {
            var el = document.getElementById(containerId);
            if (el) el.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;height:100%;color:#888;flex-direction:column;"><span style="font-size:2rem;">⚠️</span><span>Peta tidak tersedia — Leaflet belum dimuat</span></div>';
            return;
        }

        if (map) { map.remove(); map = null; }

        var center = (options && options.centerLat) ? [options.centerLat, options.centerLng] : [-6.2088, 106.8456];
        var zoom = (options && options.zoom) ? options.zoom : 13;

        map = L.map(containerId, { center: center, zoom: zoom, zoomControl: true });

        var osmLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://osm.org">OSM</a> | LandLord', maxZoom: 21
        });
        var satelliteLayer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            attribution: '&copy; Esri', maxZoom: 20
        });

        var baseMaps = { "🗺️ Peta": osmLayer, "🛰️ Satelit": satelliteLayer };
        osmLayer.addTo(map);
        L.control.layers(baseMaps, null, { position: 'topright' }).addTo(map);

        markerLayer = L.layerGroup().addTo(map);
        polygonLayer = L.layerGroup().addTo(map);
        highlightedLayer = L.layerGroup().addTo(map);
        allFeatures = {};

        var bounds = L.latLngBounds([]);
        var hasFeatures = false;

        (markers || []).forEach(function (m) {
            if (m.lat == null || m.lng == null) return;
            var latlng = [m.lat, m.lng];
            var marker = L.marker(latlng, {
                title: m.title || '',
                icon: L.icon({
                    iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
                    iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
                    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
                    iconSize: [25, 41], iconAnchor: [12, 41]
                })
            });
            if (m.info) marker.bindPopup(m.info);
            marker.addTo(markerLayer);
            bounds.extend(latlng);
            hasFeatures = true;
            if (m.id) allFeatures[m.id] = { layer: marker, type: 'marker', lat: m.lat, lng: m.lng };
        });

        (polygons || []).forEach(function (p) {
            if (!p.geoJson) return;
            try {
                var geojson = typeof p.geoJson === 'string' ? JSON.parse(p.geoJson) : p.geoJson;
                var polyLayer = L.geoJSON(geojson, {
                    style: { color: p.color || '#ff6b6b', weight: 2, fillColor: p.fillColor || '#ff6b6b', fillOpacity: 0.25 },
                    pointToLayer: function (f, ll) { return L.circleMarker(ll, { radius: 6, color: '#ff6b6b', fillOpacity: 0.6 }); }
                });
                if (p.title) polyLayer.bindPopup(p.title);
                polyLayer.addTo(polygonLayer);
                if (polyLayer.getBounds && polyLayer.getBounds().isValid()) { bounds.extend(polyLayer.getBounds()); hasFeatures = true; }
                if (p.id) allFeatures[p.id] = { layer: polyLayer, type: 'polygon' };
            } catch (e) { console.warn('GeoJSON parse error:', p.id, e); }
        });

        if (hasFeatures) map.fitBounds(bounds.pad(0.05), { maxZoom: 16 });
        map.invalidateSize();
    }

    function flyTo(lat, lng, zoom) {
        if (!map) return;
        map.flyTo([lat, lng], zoom || 17, { duration: 1.2 });
    }

    function fitBoundsToGeoJson(geoJsonStr) {
        if (!map) return;
        try {
            var geojson = typeof geoJsonStr === 'string' ? JSON.parse(geoJsonStr) : geoJsonStr;
            var layer = L.geoJSON(geojson);
            if (layer.getBounds().isValid()) map.fitBounds(layer.getBounds().pad(0.1), { maxZoom: 18 });
        } catch (e) { console.warn('fitBounds error:', e); }
    }

    function highlightFeature(id) {
        clearHighlights();
        var feature = allFeatures[id];
        if (!feature) return;

        if (feature.type === 'marker') {
            L.circleMarker([feature.lat, feature.lng], {
                radius: 14, color: '#ff6b6b', fillColor: '#ff6b6b', fillOpacity: 0.35, weight: 3, opacity: 0.8
            }).addTo(highlightedLayer);
            flyTo(feature.lat, feature.lng, 18);
            setTimeout(function () { if (feature.layer && feature.layer.openPopup) feature.layer.openPopup(); }, 1300);
        } else if (feature.type === 'polygon') {
            if (feature.layer && feature.layer.setStyle) {
                feature.layer.setStyle({ color: '#e74c3c', weight: 4, fillColor: '#e74c3c', fillOpacity: 0.4 });
                feature._originalStyle = { color: '#ff6b6b', weight: 2, fillColor: '#ff6b6b', fillOpacity: 0.25 };
            }
            if (feature.layer && feature.layer.getBounds && feature.layer.getBounds().isValid())
                map.fitBounds(feature.layer.getBounds().pad(0.1), { maxZoom: 18 });
        }
    }

    function clearHighlights() {
        highlightedLayer.clearLayers();
        Object.values(allFeatures).forEach(function (f) {
            if (f.type === 'polygon' && f._originalStyle && f.layer && f.layer.setStyle) {
                f.layer.setStyle(f._originalStyle); f._originalStyle = null;
            }
        });
    }

    function setMapType(is3D) { /* Satellite via layer control */ }

    function updateMap(containerId, options, markers, polygons) { init(containerId, options, markers, polygons); }

    return { init, updateMap, flyTo, fitBoundsToGeoJson, highlightFeature, clearHighlights, setMapType };
})();
