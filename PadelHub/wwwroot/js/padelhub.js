// ==================================================================
// Helper JS global PadelHub.
//
// Berkas ini SENGAJA eksternal, bukan <script> inline di App.razor:
// enhanced navigation Blazor mem-patch DOM dari respons halaman baru,
// dan isi skrip inline di dalam <body> bisa ikut ter-render sebagai
// teks biasa saat proses itu. Skrip dengan src tidak punya masalah itu.
// ==================================================================

// Unduh berkas yang dihasilkan server (CSV/Excel) dari stream .NET.
window.downloadFileFromStream = async function (fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
};

// Cetak satu elemen saja (kartu member, struk pembayaran).
window.printElement = function (elementId, title) {
    const element = document.getElementById(elementId);
    if (!element) return;

    const printWindow = window.open('', '_blank');
    if (!printWindow) return;

    const doc = printWindow.document;
    doc.open();
    doc.write('<!doctype html><html><head><title>' +
        (title || 'Print') +
        '</title><style>body{font-family:"Instrument Sans",Arial,sans-serif;padding:24px}' +
        '.print-wrap{display:flex;justify-content:center}</style></head>' +
        '<body><div class="print-wrap">' + element.outerHTML + '</div></body></html>');
    doc.close();

    printWindow.focus();
    printWindow.print();
    printWindow.close();
};

// Preferensi tema tersimpan antar sesi.
window.padelHub = {
    getTheme: function () {
        try { return localStorage.getItem('padelhub-theme'); } catch (e) { return null; }
    },
    setTheme: function (value) {
        try {
            localStorage.setItem('padelhub-theme', value);
            document.documentElement.setAttribute('data-pd-theme', value);
        } catch (e) { }
    },
    prefersDark: function () {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    },
    openExternal: function (url) {
        window.location.href = url;
    }
};
