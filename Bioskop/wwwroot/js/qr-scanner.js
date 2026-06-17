/**
 * QR Code scanner for Bioskop - ScanTicket page
 * Uses html5-qrcode library (CDN) or BarcodeDetector API
 */
window.bioskopScan = {
    scanner: null,
    isScanning: false,

    // Start webcam QR scanner
    startScanner: function (dotnetRef, elementId) {
        if (this.isScanning) return;
        this.isScanning = true;

        // Try html5-qrcode first
        if (typeof Html5Qrcode !== 'undefined') {
            this.scanner = new Html5Qrcode(elementId);
            this.scanner.start(
                { facingMode: "environment" },
                { fps: 10, qrbox: { width: 250, height: 250 } },
                (decodedText) => {
                    // QR detected!
                    dotnetRef.invokeMethodAsync('OnQrDetected', decodedText);
                },
                () => { /* ignore scan errors */ }
            ).catch(err => {
                console.error("Scanner error:", err);
                dotnetRef.invokeMethodAsync('OnScannerError', err.message || 'Gagal mengakses kamera');
                this.isScanning = false;
            });
        } else {
            // Fallback: try BarcodeDetector API
            this.tryBarcodeDetector(dotnetRef, elementId);
        }
    },

    // Fallback using BarcodeDetector API
    tryBarcodeDetector: async function (dotnetRef, elementId) {
        try {
            if (!('BarcodeDetector' in window)) {
                dotnetRef.invokeMethodAsync('OnScannerError', 'Browser tidak mendukung QR Scanner. Gunakan upload gambar QR.');
                this.isScanning = false;
                return;
            }

            const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
            const video = document.getElementById(elementId);
            video.srcObject = stream;
            video.play();

            const detector = new BarcodeDetector({ formats: ['qr_code'] });
            const scan = async () => {
                if (!this.isScanning) return;
                try {
                    const barcodes = await detector.detect(video);
                    if (barcodes.length > 0) {
                        dotnetRef.invokeMethodAsync('OnQrDetected', barcodes[0].rawValue);
                        stream.getTracks().forEach(t => t.stop());
                        this.isScanning = false;
                        return;
                    }
                } catch (e) { /* ignore */ }
                if (this.isScanning) requestAnimationFrame(scan);
            };
            scan();
        } catch (err) {
            dotnetRef.invokeMethodAsync('OnScannerError', 'Gagal mengakses kamera: ' + err.message);
            this.isScanning = false;
        }
    },

    // Stop scanner
    stopScanner: function () {
        this.isScanning = false;
        if (this.scanner) {
            this.scanner.stop().then(() => {
                this.scanner.clear();
                this.scanner = null;
            }).catch(() => { });
        }
        // Stop any video tracks
        const video = document.querySelector('#scanner-video video, #scanner-video');
        if (video && video.srcObject) {
            video.srcObject.getTracks().forEach(t => t.stop());
        }
    }
};
