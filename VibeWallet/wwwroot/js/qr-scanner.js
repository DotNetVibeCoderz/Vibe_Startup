// QR Scanner helper untuk VibeWallet
// Menggunakan Barcode Detection API (built-in di Chrome/Edge modern)
// Fallback: input manual

let _stream = null;
let _scanResult = "";
let _running = false;

export async function startScanning(videoElementId) {
    _scanResult = "";
    const video = document.getElementById(videoElementId);
    if (!video) return;

    try {
        _stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: "environment", width: { ideal: 640 }, height: { ideal: 480 } }
        });
        video.srcObject = _stream;
        await video.play();
        _running = true;

        // Coba pakai BarcodeDetector API (Chrome 88+)
        if ('BarcodeDetector' in window) {
            const detector = new BarcodeDetector({ formats: ['qr_code'] });
            while (_running) {
                try {
                    const barcodes = await detector.detect(video);
                    if (barcodes.length > 0) {
                        _scanResult = barcodes[0].rawValue;
                        break;
                    }
                } catch (e) { }
                await new Promise(r => setTimeout(r, 200));
            }
        }
    } catch (e) {
        console.warn("Camera not available:", e);
    }
}

export function getScanResult() {
    return _scanResult;
}

export function stopScanning() {
    _running = false;
    if (_stream) {
        _stream.getTracks().forEach(t => t.stop());
        _stream = null;
    }
    const video = document.getElementById("qrVideo");
    if (video) video.srcObject = null;
}
