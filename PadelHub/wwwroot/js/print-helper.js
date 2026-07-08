export function printElement(elementId, title) {
    const element = document.getElementById(elementId);
    if (!element) {
        console.error(`printElement: element dengan id '${elementId}' tidak ditemukan.`);
        return;
    }

    const printWindow = window.open('', '_blank');
    if (!printWindow) {
        console.error('printElement: gagal membuka window print.');
        return;
    }

    printWindow.document.write(`
        <html>
        <head>
            <title>${title || 'Print'}</title>
            <style>
                body { font-family: 'Inter', Arial, sans-serif; padding: 24px; }
                .print-wrap { display: flex; justify-content: center; }
            </style>
        </head>
        <body>
            <div class="print-wrap">${element.outerHTML}</div>
        </body>
        </html>`);
    printWindow.document.close();
    printWindow.focus();
    printWindow.print();
    printWindow.close();
}
