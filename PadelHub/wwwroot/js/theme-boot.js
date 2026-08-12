// Mengembalikan tema pilihan pengguna sebelum halaman digambar, supaya mode
// gelap tidak berkedip putih dulu. Dimuat sinkron di <head>.
(function () {
    try {
        var theme = localStorage.getItem('padelhub-theme');
        if (theme) document.documentElement.setAttribute('data-pd-theme', theme);
    } catch (e) { }
})();
