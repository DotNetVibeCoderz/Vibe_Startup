// Joka - small browser helpers. Loaded once from App.razor.
window.joka = {

    // Theme lives on <html data-theme> so CSS can key off :root and the
    // inline bootstrap in App.razor can set it before first paint.
    getTheme() {
        return document.documentElement.getAttribute('data-theme') || 'light';
    },

    setTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try { localStorage.setItem('joka-theme', theme); } catch { /* private mode */ }
        // Charts read their colours from CSS tokens, so they have to be redrawn
        // when the tokens change. They listen for this event.
        window.dispatchEvent(new CustomEvent('joka:theme', { detail: theme }));
        return theme;
    },

    toggleTheme() {
        return this.setTheme(this.getTheme() === 'dark' ? 'light' : 'dark');
    },

    // Keep the newest chat message in view after a reply lands.
    scrollToEnd(selector) {
        const el = document.querySelector(selector);
        if (el) el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' });
    },

    print() {
        window.print();
    }
};
