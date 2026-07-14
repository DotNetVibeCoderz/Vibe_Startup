// BlazePoint client helpers
window.blazePoint = {
    // ---------- theme ----------
    getTheme: () => localStorage.getItem('bp-theme') || 'light',
    setTheme: (theme) => {
        localStorage.setItem('bp-theme', theme);
        document.documentElement.setAttribute('data-theme', theme);
    },
    toggleTheme: () => {
        const next = (localStorage.getItem('bp-theme') || 'light') === 'light' ? 'dark' : 'light';
        window.blazePoint.setTheme(next);
        return next;
    },

    // ---------- leaflet maps ----------
    maps: {},
    initMap: (id, lat, lng, zoom, label) => {
        try {
            if (window.blazePoint.maps[id]) { window.blazePoint.maps[id].remove(); }
            const map = L.map(id).setView([lat, lng], zoom);
            L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors'
            }).addTo(map);
            if (label) { L.marker([lat, lng]).addTo(map).bindPopup(label).openPopup(); }
            window.blazePoint.maps[id] = map;
        } catch (e) { console.error('initMap failed', e); }
    },
    disposeMap: (id) => {
        if (window.blazePoint.maps[id]) {
            window.blazePoint.maps[id].remove();
            delete window.blazePoint.maps[id];
        }
    },

    // ---------- misc ----------
    highlightCode: (rootSelector) => {
        try {
            document.querySelectorAll((rootSelector || '') + ' pre code').forEach(el => {
                if (!el.dataset.highlighted) { hljs.highlightElement(el); }
            });
        } catch (e) { /* hljs not loaded */ }
    },
    scrollToBottom: (id) => {
        const el = document.getElementById(id);
        if (el) { el.scrollTop = el.scrollHeight; }
    },
    copyText: (text) => navigator.clipboard.writeText(text),
    downloadUrl: (url) => { window.open(url, '_blank'); },
    focus: (id) => { const el = document.getElementById(id); if (el) el.focus(); },
    confirmDialog: (message) => confirm(message)
};
