// ============================================
// EstateHub - Map, Chat, Layout & Auth JS
// ============================================

function getBrowserWidth() { return window.innerWidth; }

// --- LocalStorage Auth ---
const AUTH_KEY = 'estatehub_auth';
function setAuthToLocalStorage(json) { try { localStorage.setItem(AUTH_KEY, json); } catch(e){} }
function getAuthFromLocalStorage() {
    try { var json = localStorage.getItem(AUTH_KEY); if(!json) return ''; var data = JSON.parse(json); if(data.ExpiresAt && new Date(data.ExpiresAt) < new Date()) { localStorage.removeItem(AUTH_KEY); return ''; } return json; } catch(e){ return ''; }
}
function clearAuthFromLocalStorage() { try { localStorage.removeItem(AUTH_KEY); } catch(e){} }

// --- Trigger click on element by ID (for InputFile) ---
function triggerClick(id) { var el = document.getElementById(id); if (el) el.click(); }

// --- Trigger file download from base64 (contract, etc) ---
function triggerDownload(base64, fileName, mimeType) {
    var link = document.createElement('a');
    link.href = 'data:' + mimeType + ';base64,' + base64;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

// --- Leaflet Map ---
let map, markers = [];
function initMap(lat, lng, zoom) {
    if (typeof L === 'undefined') { console.warn('Leaflet not loaded'); return; }
    if (map) { map.remove(); markers = []; }
    map = L.map('map').setView([lat, lng], zoom);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '© OpenStreetMap contributors', maxZoom: 19 }).addTo(map);
}
function addMarker(lat, lng, title, price, propertyId) {
    if (!map) return;
    var popupContent = '<div style="min-width:200px;"><h5 style="margin:0 0 8px 0;font-weight:700;">' + title + '</h5><p style="margin:0;color:#2563EB;font-weight:700;">' + price + '</p><a href="/property/' + propertyId + '" style="color:#2563EB;text-decoration:none;font-size:0.9em;font-weight:600;">Lihat Detail →</a></div>';
    const marker = L.marker([lat, lng]).addTo(map).bindPopup(popupContent); markers.push(marker);
}
function clearMarkers() { markers.forEach(function(m) { map.removeLayer(m); }); markers = []; }
function scrollChatToBottom(element) { if (element) element.scrollTop = element.scrollHeight; }
