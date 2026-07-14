// BlazorViz JS interop: charts (ECharts / Chart.js / D3), maps (Leaflet), grid layout, theme, downloads.
window.bv = (function () {
    const charts = {};   // panelId -> echarts instance
    const maps = {};     // panelId -> leaflet map
    const customs = {};  // panelId -> cleanup fn

    const palette = ["#6C5CE7", "#00B894", "#FDCB6E", "#E17055", "#0984E3", "#E84393", "#00CEC9", "#D63031", "#636E72", "#A29BFE"];

    function isDark() {
        return document.documentElement.getAttribute("data-theme") === "dark";
    }

    function baseTheme() {
        const dark = isDark();
        return {
            color: palette,
            backgroundColor: "transparent",
            textStyle: { color: dark ? "#e8e6f0" : "#2d2a3e", fontFamily: "'Space Grotesk', 'Segoe UI', sans-serif" },
            legend: { textStyle: { color: dark ? "#e8e6f0" : "#2d2a3e" } },
            tooltip: {
                backgroundColor: dark ? "#2a2740" : "#ffffff",
                borderColor: dark ? "#4d4768" : "#2d2a3e",
                borderWidth: 2,
                textStyle: { color: dark ? "#e8e6f0" : "#2d2a3e" }
            }
        };
    }

    function disposePanel(id) {
        if (charts[id]) { charts[id].dispose(); delete charts[id]; }
        if (maps[id]) { maps[id].remove(); delete maps[id]; }
        if (customs[id]) { try { customs[id](); } catch { } delete customs[id]; }
    }

    return {
        // ---------- ECharts ----------
        renderChart: function (elId, optionJson) {
            const el = document.getElementById(elId);
            if (!el || typeof echarts === "undefined") return;
            disposePanel(elId);
            const chart = echarts.init(el, null, { renderer: "canvas" });
            const option = Object.assign({}, baseTheme(), JSON.parse(optionJson));
            chart.setOption(option);
            charts[elId] = chart;
        },
        chartImage: function (elId) {
            const chart = charts[elId];
            return chart ? chart.getDataURL({ pixelRatio: 2, backgroundColor: isDark() ? "#1e1b2e" : "#ffffff" }) : null;
        },
        resizeCharts: function () {
            Object.values(charts).forEach(c => c.resize());
            Object.values(maps).forEach(m => m.invalidateSize());
        },
        disposeChart: disposePanel,

        // ---------- Leaflet ----------
        renderMap: function (elId, pointsJson, heat) {
            const el = document.getElementById(elId);
            if (!el || typeof L === "undefined") return;
            disposePanel(elId);
            const points = JSON.parse(pointsJson); // [lat, lng, value, label]
            const map = L.map(el, { attributionControl: false, zoomControl: true });
            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { maxZoom: 18 }).addTo(map);
            const maxVal = Math.max(1, ...points.map(p => p[2] || 1));
            const group = [];
            points.forEach(p => {
                const radius = 6 + 24 * Math.sqrt((p[2] || 1) / maxVal);
                const marker = L.circleMarker([p[0], p[1]], {
                    radius: radius,
                    color: "#2d2a3e", weight: 2,
                    fillColor: heat ? "#E17055" : "#6C5CE7", fillOpacity: 0.65
                }).addTo(map);
                if (p[3] || p[2]) marker.bindPopup(`<b>${p[3] ?? ""}</b><br>${(p[2] ?? "").toLocaleString?.() ?? p[2]}`);
                group.push(marker);
            });
            if (group.length) map.fitBounds(L.featureGroup(group).getBounds().pad(0.2));
            else map.setView([0, 0], 2);
            maps[elId] = map;
        },

        // ---------- Custom visuals (Chart.js / D3 / raw ECharts) ----------
        renderCustom: function (elId, lib, code, rowsJson, columnsJson) {
            const el = document.getElementById(elId);
            if (!el) return null;
            disposePanel(elId);
            el.innerHTML = "";
            const rows = JSON.parse(rowsJson);
            const columns = JSON.parse(columnsJson);
            try {
                const libObj = lib === "chartjs" ? window.Chart : lib === "d3" ? window.d3 : window.echarts;
                const fn = new Function("el", "rows", "columns", "lib", "palette", code);
                const cleanup = fn(el, rows, columns, libObj, palette);
                if (typeof cleanup === "function") customs[elId] = cleanup;
                return null;
            } catch (e) {
                el.innerHTML = `<div style="padding:12px;color:#D63031;font-size:12px;white-space:pre-wrap;">Custom visual error: ${e.message}</div>`;
                return e.message;
            }
        },

        // ---------- Gridstack ----------
        initGrid: function (elId, dotnetRef) {
            const el = document.getElementById(elId);
            if (!el || typeof GridStack === "undefined") return;
            if (el.gridstack) { el.gridstack.destroy(false); }
            const grid = GridStack.init({
                column: 12, cellHeight: 80, margin: 8, float: false,
                resizable: { handles: "se" }, draggable: { handle: ".panel-drag" }
            }, el);
            grid.on("change", function (_e, items) {
                const changes = (items || []).map(i => ({ id: i.el.getAttribute("data-panel-id"), x: i.x, y: i.y, w: i.w, h: i.h }));
                if (changes.length) dotnetRef.invokeMethodAsync("OnGridChanged", JSON.stringify(changes));
            });
            setTimeout(() => Object.values(charts).forEach(c => c.resize()), 350);
        },
        destroyGrid: function (elId) {
            const el = document.getElementById(elId);
            if (el && el.gridstack) el.gridstack.destroy(false);
        },

        // ---------- Theme ----------
        getTheme: function () {
            return localStorage.getItem("bv-theme") || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
        },
        setTheme: function (theme) {
            document.documentElement.setAttribute("data-theme", theme);
            localStorage.setItem("bv-theme", theme);
        },
        initTheme: function () {
            const t = window.bv.getTheme();
            document.documentElement.setAttribute("data-theme", t);
            return t;
        },

        // ---------- Files ----------
        downloadFile: function (fileName, contentType, base64) {
            const a = document.createElement("a");
            a.href = `data:${contentType};base64,${base64}`;
            a.download = fileName;
            a.click();
        },
        downloadDataUrl: function (fileName, dataUrl) {
            const a = document.createElement("a");
            a.href = dataUrl;
            a.download = fileName;
            a.click();
        },
        copyText: function (text) {
            return navigator.clipboard.writeText(text);
        },
        scrollToBottom: function (elId) {
            const el = document.getElementById(elId);
            if (el) el.scrollTop = el.scrollHeight;
        }
    };
})();

window.addEventListener("resize", () => window.bv && window.bv.resizeCharts());
window.bv.initTheme();
