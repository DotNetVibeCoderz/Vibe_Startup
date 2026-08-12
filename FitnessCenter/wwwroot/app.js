/* ============================================================
   FITNESS CENTER — micro-interaction runtime
   ------------------------------------------------------------
   Tanpa dependensi. Aman untuk Blazor Server: seluruh efek
   dipasang lewat event delegation + MutationObserver, sehingga
   node yang dirender ulang tetap ikut hidup.
   ============================================================ */
(function () {
    'use strict';

    var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    window.matchMedia('(prefers-reduced-motion: reduce)')
        .addEventListener('change', function (e) { reduceMotion = e.matches; });

    var nf = new Intl.NumberFormat('id-ID');

    /* ---------- Tema ---------- */

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try { localStorage.setItem('fitness-theme', theme); } catch (e) { /* mode privat */ }
    }

    window.toggleTheme = function () {
        var next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        // View Transition membuat pergantian tema menyapu, bukan berkedip
        if (document.startViewTransition && !reduceMotion) {
            document.startViewTransition(function () { applyTheme(next); });
        } else {
            applyTheme(next);
        }
    };

    /* ---------- Hitung naik: angka statistik seperti pencacah repetisi ---------- */

    function countUp(el) {
        if (el.dataset.counted === '1') return;
        el.dataset.counted = '1';

        var target = parseFloat(el.dataset.countup);
        if (isNaN(target)) return;

        var decimals = parseInt(el.dataset.decimals || '0', 10);
        var prefix = el.dataset.prefix || '';
        var suffix = el.dataset.suffix || '';
        var fmt = new Intl.NumberFormat('id-ID', {
            minimumFractionDigits: decimals,
            maximumFractionDigits: decimals
        });

        var render = function (v) { el.textContent = prefix + fmt.format(v) + suffix; };

        if (reduceMotion || target === 0) { render(target); return; }

        var duration = 900;
        var start = null;
        el.classList.add('counting');

        function frame(ts) {
            if (start === null) start = ts;
            var p = Math.min((ts - start) / duration, 1);
            // easeOutExpo — cepat di awal, mendarat halus seperti beban diletakkan
            var eased = p === 1 ? 1 : 1 - Math.pow(2, -10 * p);
            render(target * eased);
            if (p < 1) requestAnimationFrame(frame);
            else { render(target); el.classList.remove('counting'); }
        }
        requestAnimationFrame(frame);
    }

    /* ---------- Meter gaya plate: isi segmen sesuai persentase ---------- */

    function fillMeter(el) {
        if (el.dataset.filled === '1') return;
        el.dataset.filled = '1';

        var pct = Math.max(0, Math.min(100, parseFloat(el.dataset.meter || '0')));
        var segments = el.children.length || 0;
        var on = Math.round((pct / 100) * segments);

        Array.prototype.forEach.call(el.children, function (seg, i) {
            var delay = reduceMotion ? 0 : 120 + i * 45;
            setTimeout(function () { seg.classList.toggle('on', i < on); }, delay);
        });
    }

    /* ---------- Muncul saat masuk layar ---------- */

    var io = 'IntersectionObserver' in window
        ? new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) return;
                var el = entry.target;
                io.unobserve(el);
                if (el.hasAttribute('data-countup')) countUp(el);
                if (el.hasAttribute('data-meter')) fillMeter(el);
                if (el.hasAttribute('data-reveal')) el.classList.add('reveal');
            });
        }, { rootMargin: '0px 0px -8% 0px', threshold: .1 })
        : null;

    function enhance(root) {
        var nodes = (root || document).querySelectorAll('[data-countup],[data-meter],[data-reveal]');
        Array.prototype.forEach.call(nodes, function (el) {
            if (el.dataset.enhanced === '1') return;
            el.dataset.enhanced = '1';
            if (io) io.observe(el);
            else {
                if (el.hasAttribute('data-countup')) countUp(el);
                if (el.hasAttribute('data-meter')) fillMeter(el);
            }
        });

        // Beri indeks pada anak .reveal agar masuknya berurutan
        var groups = (root || document).querySelectorAll('[data-stagger]');
        Array.prototype.forEach.call(groups, function (group) {
            if (group.dataset.staggered === '1') return;
            group.dataset.staggered = '1';
            Array.prototype.forEach.call(group.children, function (child, i) {
                child.style.setProperty('--i', i);
                child.classList.add('reveal');
            });
        });
    }

    /* ---------- Riak sentuh pada tombol ---------- */

    document.addEventListener('pointerdown', function (e) {
        if (reduceMotion) return;
        var btn = e.target.closest('.brutal-btn');
        if (!btn || btn.disabled) return;

        var rect = btn.getBoundingClientRect();
        var size = Math.max(rect.width, rect.height);
        var ink = document.createElement('span');
        ink.className = 'ripple';
        ink.style.width = ink.style.height = size + 'px';
        ink.style.left = (e.clientX - rect.left - size / 2) + 'px';
        ink.style.top = (e.clientY - rect.top - size / 2) + 'px';
        btn.appendChild(ink);
        setTimeout(function () { ink.remove(); }, 600);
    }, { passive: true });

    /* ---------- Tutup modal & rail dengan Escape ---------- */

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var overlay = document.querySelector('.brutal-modal-overlay');
        if (overlay) overlay.click();
    });

    /* ---------- Helper yang dipanggil dari Blazor ---------- */

    window.fitnessUI = {
        refresh: function () { enhance(document); },
        scrollToBottom: function (selector) {
            var el = document.querySelector(selector);
            if (el) el.scrollTo({ top: el.scrollHeight, behavior: reduceMotion ? 'auto' : 'smooth' });
        },
        openInNewTab: function (url) { window.open(url, '_blank', 'noopener'); },
        copy: function (text) {
            if (navigator.clipboard) return navigator.clipboard.writeText(text);
            return Promise.resolve();
        },
        format: function (n) { return nf.format(n); }
    };

    /* ---------- Pasang & pantau perubahan DOM dari Blazor ---------- */

    function boot() {
        enhance(document);
        new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                if (mutations[i].addedNodes.length) { enhance(document); return; }
            }
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();
})();
