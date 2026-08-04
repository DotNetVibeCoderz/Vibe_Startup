// FastRide dispatch console — chart rendering and theme handling.
//
// Chart.js was already being loaded by the old dashboard but never called; every chart
// was hand-rolled with div heights. These helpers wire it up properly and keep every
// chart on the same palette as the rest of the console.

window.fastride = (() => {
    const charts = new Map();

    const readToken = (name, fallback) => {
        const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        return value || fallback;
    };

    const palette = () => ({
        lampu: readToken('--lampu', '#FFB020'),
        jalan: readToken('--jalan', '#23C48E'),
        sirene: readToken('--sirene', '#FF5A45'),
        lintas: readToken('--lintas', '#5B9DFF'),
        garis: readToken('--garis', '#26324D'),
        kabut: readToken('--kabut', '#8FA0C0'),
        aspal: readToken('--aspal', '#161F33')
    });

    const baseOptions = () => {
        const c = palette();
        return {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    labels: {
                        color: c.kabut,
                        boxWidth: 10,
                        boxHeight: 10,
                        usePointStyle: true,
                        font: { family: 'IBM Plex Sans', size: 11 }
                    }
                },
                tooltip: {
                    backgroundColor: c.aspal,
                    borderColor: c.garis,
                    borderWidth: 1,
                    titleColor: readToken('--kapur', '#EEF3FB'),
                    bodyColor: c.kabut,
                    titleFont: { family: 'IBM Plex Sans', size: 12 },
                    bodyFont: { family: 'IBM Plex Mono', size: 11 },
                    padding: 10,
                    displayColors: true
                }
            },
            scales: {
                x: {
                    grid: { color: c.garis, drawBorder: false },
                    ticks: { color: c.kabut, font: { family: 'IBM Plex Mono', size: 10 } }
                },
                y: {
                    beginAtZero: true,
                    grid: { color: c.garis, drawBorder: false },
                    ticks: { color: c.kabut, font: { family: 'IBM Plex Mono', size: 10 } }
                }
            }
        };
    };

    const destroy = (id) => {
        const existing = charts.get(id);
        if (existing) {
            existing.destroy();
            charts.delete(id);
        }
    };

    const mount = (id, config) => {
        const canvas = document.getElementById(id);
        if (!canvas || typeof Chart === 'undefined') return;

        // Blazor re-renders can hand us the same canvas twice; the old chart owns the
        // context until it is disposed.
        destroy(id);
        charts.set(id, new Chart(canvas.getContext('2d'), config));
    };

    const rupiah = (value) => 'Rp ' + Number(value ?? 0).toLocaleString('id-ID');

    return {
        revenueSeries(id, labels, revenue, orders) {
            const c = palette();
            mount(id, {
                type: 'line',
                data: {
                    labels,
                    datasets: [
                        {
                            label: 'Pendapatan',
                            data: revenue,
                            borderColor: c.lampu,
                            backgroundColor: c.lampu + '22',
                            borderWidth: 2,
                            fill: true,
                            tension: 0.32,
                            pointRadius: 0,
                            pointHoverRadius: 4,
                            yAxisID: 'y'
                        },
                        {
                            label: 'Order',
                            data: orders,
                            borderColor: c.lintas,
                            borderWidth: 1.5,
                            borderDash: [4, 3],
                            fill: false,
                            tension: 0.32,
                            pointRadius: 0,
                            pointHoverRadius: 4,
                            yAxisID: 'y1'
                        }
                    ]
                },
                options: {
                    ...baseOptions(),
                    scales: {
                        ...baseOptions().scales,
                        y: {
                            ...baseOptions().scales.y,
                            ticks: {
                                ...baseOptions().scales.y.ticks,
                                callback: (v) => 'Rp ' + (v / 1000) + 'rb'
                            }
                        },
                        y1: {
                            position: 'right',
                            beginAtZero: true,
                            grid: { drawOnChartArea: false },
                            ticks: { color: palette().kabut, font: { family: 'IBM Plex Mono', size: 10 } }
                        }
                    },
                    plugins: {
                        ...baseOptions().plugins,
                        tooltip: {
                            ...baseOptions().plugins.tooltip,
                            callbacks: {
                                label: (ctx) => ctx.datasetIndex === 0
                                    ? 'Pendapatan: ' + rupiah(ctx.parsed.y)
                                    : 'Order: ' + ctx.parsed.y
                            }
                        }
                    }
                }
            });
        },

        statusDoughnut(id, labels, values, colours) {
            const c = palette();
            const options = baseOptions();
            mount(id, {
                type: 'doughnut',
                data: {
                    labels,
                    datasets: [{
                        data: values,
                        backgroundColor: colours.map(name => c[name] ?? c.garis),
                        borderColor: c.aspal,
                        borderWidth: 2,
                        hoverOffset: 6
                    }]
                },
                options: {
                    ...options,
                    cutout: '62%',
                    scales: {},
                    plugins: { ...options.plugins, legend: { ...options.plugins.legend, position: 'right' } }
                }
            });
        },

        categoryBars(id, labels, values) {
            const c = palette();
            const options = baseOptions();
            mount(id, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [{
                        label: 'Order',
                        data: values,
                        backgroundColor: c.lintas,
                        borderRadius: 3,
                        maxBarThickness: 34
                    }]
                },
                options: { ...options, plugins: { ...options.plugins, legend: { display: false } } }
            });
        },

        paymentBars(id, labels, values) {
            const c = palette();
            const options = baseOptions();
            mount(id, {
                type: 'bar',
                data: {
                    labels,
                    datasets: [{
                        label: 'Nilai',
                        data: values,
                        backgroundColor: c.jalan,
                        borderRadius: 3,
                        maxBarThickness: 28
                    }]
                },
                options: {
                    ...options,
                    indexAxis: 'y',
                    plugins: {
                        ...options.plugins,
                        legend: { display: false },
                        tooltip: {
                            ...options.plugins.tooltip,
                            callbacks: { label: (ctx) => rupiah(ctx.parsed.x) }
                        }
                    }
                }
            });
        },

        dispose(id) { destroy(id); },

        setTheme(theme) {
            document.documentElement.setAttribute('data-theme', theme);
            try { localStorage.setItem('fastride-theme', theme); } catch { /* private mode */ }
            // Charts bake their colours in at construction time, so they have to be rebuilt.
            charts.forEach((chart, id) => { chart.destroy(); charts.delete(id); });
        },

        currentTheme() {
            try { return localStorage.getItem('fastride-theme') || 'dark'; } catch { return 'dark'; }
        },

        download(fileName, contentType, base64) {
            const link = document.createElement('a');
            link.href = `data:${contentType};base64,${base64}`;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
        }
    };
})();

// Apply the stored theme before Blazor connects, so the page never flashes the wrong one.
(() => {
    try {
        const stored = localStorage.getItem('fastride-theme');
        if (stored) document.documentElement.setAttribute('data-theme', stored);
    } catch { /* ignore */ }
})();
