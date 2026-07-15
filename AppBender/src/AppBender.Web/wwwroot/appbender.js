// AppBender client helpers: theme, downloads, scrolling.
window.appBender = {
    initTheme: function () {
        const saved = localStorage.getItem('ab-theme');
        const theme = saved || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
        document.documentElement.setAttribute('data-bs-theme', theme);
        return theme;
    },
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        localStorage.setItem('ab-theme', theme);
    },
    getTheme: function () {
        return document.documentElement.getAttribute('data-bs-theme') || 'light';
    },
    // Pure-JS toggle so it works on statically-rendered layouts too.
    toggleTheme: function (button) {
        const next = window.appBender.getTheme() === 'dark' ? 'light' : 'dark';
        window.appBender.setTheme(next);
        if (button) button.textContent = next === 'dark' ? '🌙' : '☀️';
    },
    themeIcon: function () {
        return window.appBender.getTheme() === 'dark' ? '🌙' : '☀️';
    },
    downloadText: function (fileName, content, mime) {
        const blob = new Blob([content], { type: mime || 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },
    downloadUrl: function (url, fileName) {
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName || '';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
    },
    scrollToBottom: function (element) {
        if (element) element.scrollTop = element.scrollHeight;
    },
    copyText: function (text) {
        return navigator.clipboard.writeText(text);
    }
};
// apply theme immediately to avoid flashes
window.appBender.initTheme();

// keep the toggle button icon in sync on initial load and after enhanced navigation
(function () {
    function syncIcon() {
        const button = document.getElementById('themeToggle');
        if (button) button.textContent = window.appBender.themeIcon();
    }
    document.addEventListener('DOMContentLoaded', syncIcon);
    if (window.Blazor && Blazor.addEventListener) Blazor.addEventListener('enhancedload', syncIcon);
    else document.addEventListener('enhancedload', syncIcon);
})();
