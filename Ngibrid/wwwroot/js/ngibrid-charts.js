/**
 * Ngibrid Logistics — D3.js chart library
 *
 * Every chart is an SVG built with D3, redraws on container resize, and reads its colours from the
 * CSS custom properties in ngibrid.css so light/dark theme switching needs no JS changes.
 */
(function () {
    'use strict';

    const registry = new Map();   // elementId -> redraw function
    let resizeObserver = null;

    // ─── helpers ───

    function cssVar(name, fallback) {
        const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        return value || fallback;
    }

    function palette() {
        return {
            accent: cssVar('--accent', '#1877f2'),
            success: cssVar('--success', '#31a24c'),
            warning: cssVar('--warning', '#f59e0b'),
            danger: cssVar('--danger', '#dc3545'),
            info: cssVar('--info', '#17a2b8'),
            muted: cssVar('--text-secondary', '#65676b'),
            text: cssVar('--text-primary', '#1c1e21'),
            grid: cssVar('--border-light', '#e4e6eb'),
            surface: cssVar('--bg-card', '#ffffff')
        };
    }

    /** Ordered, colour-blind-safe series colours for categorical data. */
    const categorical = ['#1877f2', '#31a24c', '#f59e0b', '#8b5cf6', '#17a2b8', '#dc3545', '#64748b', '#ec4899'];

    const statusColors = {
        DELIVERED: '--success',
        IN_TRANSIT: '--accent',
        OUT_FOR_DELIVERY: '--info',
        AT_WAREHOUSE: '--info',
        CREATED: '--text-tertiary',
        PICKED_UP: '--warning',
        FAILED: '--danger',
        RETURNED: '--danger',
        CANCELLED: '--text-tertiary'
    };

    function statusColor(status, index) {
        const varName = statusColors[status];
        return varName ? cssVar(varName, categorical[index % categorical.length])
            : categorical[index % categorical.length];
    }

    function prepare(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return null;

        d3.select(el).selectAll('*').remove();

        const width = el.clientWidth || 400;
        const height = el.clientHeight || 260;
        if (width < 20 || height < 20) return null;

        const svg = d3.select(el).append('svg')
            .attr('width', '100%')
            .attr('height', '100%')
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('preserveAspectRatio', 'xMidYMid meet')
            .style('overflow', 'visible');

        return { el, svg, width, height, c: palette() };
    }

    /** One shared tooltip element, positioned near the cursor. */
    function tooltip() {
        let tip = d3.select('body').select('.ngibrid-chart-tooltip');
        if (tip.empty()) {
            tip = d3.select('body').append('div')
                .attr('class', 'ngibrid-chart-tooltip')
                .style('position', 'fixed')
                .style('pointer-events', 'none')
                .style('opacity', 0)
                .style('z-index', 3000);
        }
        return tip;
    }

    function showTip(event, html) {
        tooltip().html(html)
            .style('opacity', 1)
            .style('left', (event.clientX + 14) + 'px')
            .style('top', (event.clientY - 10) + 'px');
    }

    function hideTip() {
        tooltip().style('opacity', 0);
    }

    function emptyState(ctx, message) {
        ctx.svg.append('text')
            .attr('x', ctx.width / 2)
            .attr('y', ctx.height / 2)
            .attr('text-anchor', 'middle')
            .attr('fill', ctx.c.muted)
            .attr('font-size', '13px')
            .text(message || 'Belum ada data');
    }

    /** Register a chart so it redraws on container resize and theme change. */
    function register(elementId, draw) {
        registry.set(elementId, draw);

        if (!resizeObserver && typeof ResizeObserver !== 'undefined') {
            resizeObserver = new ResizeObserver(entries => {
                for (const entry of entries) {
                    const redraw = registry.get(entry.target.id);
                    if (redraw) redraw();
                }
            });
        }

        const el = document.getElementById(elementId);
        if (el && resizeObserver) {
            resizeObserver.unobserve(el);
            resizeObserver.observe(el);
        }

        draw();
    }

    // ─── charts ───

    /**
     * Time-series area + line chart. Data: { "yyyy-MM-dd": number, ... }
     */
    function lineChart(elementId, data, options) {
        const opts = Object.assign({ label: 'Order', format: d => d3.format(',')(d) }, options || {});

        register(elementId, function () {
            const ctx = prepare(elementId);
            if (!ctx) return;

            const entries = Object.entries(data || {})
                .map(([k, v]) => ({ date: new Date(k), value: +v }))
                .filter(d => !isNaN(d.date))
                .sort((a, b) => a.date - b.date);

            if (entries.length === 0) return emptyState(ctx);

            const margin = { top: 12, right: 16, bottom: 28, left: 44 };
            const w = ctx.width - margin.left - margin.right;
            const h = ctx.height - margin.top - margin.bottom;
            const g = ctx.svg.append('g').attr('transform', `translate(${margin.left},${margin.top})`);

            const x = d3.scaleTime().domain(d3.extent(entries, d => d.date)).range([0, w]);
            const y = d3.scaleLinear()
                .domain([0, d3.max(entries, d => d.value) * 1.15 || 1])
                .nice().range([h, 0]);

            // horizontal grid only — vertical lines add noise without aiding reading
            g.append('g')
                .call(d3.axisLeft(y).ticks(5).tickSize(-w).tickFormat(d3.format('~s')))
                .call(s => s.select('.domain').remove())
                .call(s => s.selectAll('.tick line').attr('stroke', ctx.c.grid))
                .call(s => s.selectAll('text').attr('fill', ctx.c.muted).attr('font-size', '10px'));

            g.append('g')
                .attr('transform', `translate(0,${h})`)
                .call(d3.axisBottom(x).ticks(Math.min(6, entries.length)).tickFormat(d3.timeFormat('%d %b')))
                .call(s => s.select('.domain').attr('stroke', ctx.c.grid))
                .call(s => s.selectAll('.tick line').attr('stroke', ctx.c.grid))
                .call(s => s.selectAll('text').attr('fill', ctx.c.muted).attr('font-size', '10px'));

            const gradientId = `grad-${elementId}`;
            const gradient = ctx.svg.append('defs').append('linearGradient')
                .attr('id', gradientId).attr('x1', 0).attr('y1', 0).attr('x2', 0).attr('y2', 1);
            gradient.append('stop').attr('offset', '0%').attr('stop-color', ctx.c.accent).attr('stop-opacity', 0.35);
            gradient.append('stop').attr('offset', '100%').attr('stop-color', ctx.c.accent).attr('stop-opacity', 0.02);

            g.append('path')
                .datum(entries)
                .attr('fill', `url(#${gradientId})`)
                .attr('d', d3.area().x(d => x(d.date)).y0(h).y1(d => y(d.value)).curve(d3.curveMonotoneX));

            g.append('path')
                .datum(entries)
                .attr('fill', 'none')
                .attr('stroke', ctx.c.accent)
                .attr('stroke-width', 2)
                .attr('d', d3.line().x(d => x(d.date)).y(d => y(d.value)).curve(d3.curveMonotoneX));

            g.selectAll('.pt').data(entries).enter().append('circle')
                .attr('class', 'pt')
                .attr('cx', d => x(d.date))
                .attr('cy', d => y(d.value))
                .attr('r', entries.length > 40 ? 0 : 3)
                .attr('fill', ctx.c.surface)
                .attr('stroke', ctx.c.accent)
                .attr('stroke-width', 2);

            // Invisible hit area so hovering anywhere in the column shows that day's value.
            const bandWidth = w / entries.length;
            g.selectAll('.hit').data(entries).enter().append('rect')
                .attr('class', 'hit')
                .attr('x', d => x(d.date) - bandWidth / 2)
                .attr('y', 0).attr('width', bandWidth).attr('height', h)
                .attr('fill', 'transparent')
                .on('mousemove', (event, d) => showTip(event,
                    `<strong>${d3.timeFormat('%d %b %Y')(d.date)}</strong><br/>${opts.label}: ${opts.format(d.value)}`))
                .on('mouseleave', hideTip);
        });
    }

    /**
     * Donut chart with legend. Data: { "STATUS": count, ... }
     */
    function donutChart(elementId, data) {
        register(elementId, function () {
            const ctx = prepare(elementId);
            if (!ctx) return;

            const entries = Object.entries(data || {})
                .map(([key, value]) => ({ key, value: +value }))
                .filter(d => d.value > 0)
                .sort((a, b) => b.value - a.value);

            if (entries.length === 0) return emptyState(ctx);

            const total = d3.sum(entries, d => d.value);
            const legendWidth = ctx.width > 320 ? 130 : 0;
            const chartWidth = ctx.width - legendWidth;
            const radius = Math.min(chartWidth, ctx.height) / 2 - 8;
            if (radius <= 0) return emptyState(ctx);

            const g = ctx.svg.append('g')
                .attr('transform', `translate(${chartWidth / 2},${ctx.height / 2})`);

            const arcs = d3.pie().sort(null).value(d => d.value)(entries);
            const arc = d3.arc().innerRadius(radius * 0.58).outerRadius(radius);
            const arcHover = d3.arc().innerRadius(radius * 0.58).outerRadius(radius + 5);

            g.selectAll('path').data(arcs).enter().append('path')
                .attr('d', arc)
                .attr('fill', (d, i) => statusColor(d.data.key, i))
                .attr('stroke', ctx.c.surface)
                .attr('stroke-width', 2)
                .on('mousemove', function (event, d) {
                    d3.select(this).transition().duration(120).attr('d', arcHover);
                    showTip(event, `<strong>${d.data.key}</strong><br/>${d.data.value} order ` +
                        `(${(d.data.value / total * 100).toFixed(1)}%)`);
                })
                .on('mouseleave', function () {
                    d3.select(this).transition().duration(120).attr('d', arc);
                    hideTip();
                });

            g.append('text').attr('text-anchor', 'middle').attr('dy', '-0.1em')
                .attr('fill', ctx.c.text).attr('font-size', '22px').attr('font-weight', '800')
                .text(d3.format(',')(total));
            g.append('text').attr('text-anchor', 'middle').attr('dy', '1.4em')
                .attr('fill', ctx.c.muted).attr('font-size', '11px')
                .text('total order');

            if (legendWidth > 0) {
                const legend = ctx.svg.append('g')
                    .attr('transform', `translate(${chartWidth + 8},${Math.max(12, ctx.height / 2 - entries.length * 9)})`);

                entries.slice(0, 8).forEach((d, i) => {
                    const row = legend.append('g').attr('transform', `translate(0,${i * 18})`);
                    row.append('rect').attr('width', 10).attr('height', 10).attr('rx', 2)
                        .attr('fill', statusColor(d.key, i));
                    row.append('text').attr('x', 15).attr('y', 9)
                        .attr('fill', ctx.c.muted).attr('font-size', '10px')
                        .text(`${d.key.length > 13 ? d.key.slice(0, 12) + '…' : d.key} (${d.value})`);
                });
            }
        });
    }

    /**
     * Horizontal bar chart. Data: [{ label, value }]
     */
    function barChart(elementId, data, options) {
        const opts = Object.assign({ format: d => d3.format(',')(d), color: null }, options || {});

        register(elementId, function () {
            const ctx = prepare(elementId);
            if (!ctx) return;

            const entries = (data || []).filter(d => d && d.value > 0);
            if (entries.length === 0) return emptyState(ctx);

            const margin = { top: 8, right: 48, bottom: 8, left: 96 };
            const w = ctx.width - margin.left - margin.right;
            const h = ctx.height - margin.top - margin.bottom;
            const g = ctx.svg.append('g').attr('transform', `translate(${margin.left},${margin.top})`);

            const x = d3.scaleLinear().domain([0, d3.max(entries, d => d.value)]).nice().range([0, w]);
            const y = d3.scaleBand().domain(entries.map(d => d.label)).range([0, h]).padding(0.25);

            g.append('g').call(d3.axisLeft(y).tickSize(0))
                .call(s => s.select('.domain').remove())
                .call(s => s.selectAll('text').attr('fill', ctx.c.muted).attr('font-size', '10px'));

            g.selectAll('.bar').data(entries).enter().append('rect')
                .attr('class', 'bar')
                .attr('x', 0).attr('y', d => y(d.label))
                .attr('height', y.bandwidth()).attr('rx', 4)
                .attr('fill', (d, i) => opts.color || categorical[i % categorical.length])
                .attr('width', 0)
                .on('mousemove', (event, d) => showTip(event, `<strong>${d.label}</strong><br/>${opts.format(d.value)}`))
                .on('mouseleave', hideTip)
                .transition().duration(500)
                .attr('width', d => Math.max(x(d.value), 2));

            g.selectAll('.val').data(entries).enter().append('text')
                .attr('class', 'val')
                .attr('x', d => Math.max(x(d.value), 2) + 6)
                .attr('y', d => y(d.label) + y.bandwidth() / 2 + 4)
                .attr('fill', ctx.c.muted).attr('font-size', '10px')
                .text(d => opts.format(d.value));
        });
    }

    /**
     * Forecast chart: historical line plus predicted band.
     * data = { history: {date: value}, forecast: [{ forecastDate, predictedOrders, lowerBound, upperBound, isPeakSeason }] }
     */
    function forecastChart(elementId, data) {
        register(elementId, function () {
            const ctx = prepare(elementId);
            if (!ctx) return;

            const history = Object.entries((data && data.history) || {})
                .map(([k, v]) => ({ date: new Date(k), value: +v }))
                .filter(d => !isNaN(d.date))
                .sort((a, b) => a.date - b.date);

            const forecast = ((data && data.forecast) || []).map(f => ({
                date: new Date(f.forecastDate),
                value: +f.predictedOrders,
                lower: +f.lowerBound,
                upper: +f.upperBound,
                peak: !!f.isPeakSeason
            })).filter(d => !isNaN(d.date));

            if (history.length === 0 && forecast.length === 0) return emptyState(ctx);

            const margin = { top: 12, right: 16, bottom: 28, left: 44 };
            const w = ctx.width - margin.left - margin.right;
            const h = ctx.height - margin.top - margin.bottom;
            const g = ctx.svg.append('g').attr('transform', `translate(${margin.left},${margin.top})`);

            const allDates = history.concat(forecast).map(d => d.date);
            const maxValue = d3.max(history.concat(forecast), d => Math.max(d.value, d.upper || 0)) || 1;

            const x = d3.scaleTime().domain(d3.extent(allDates)).range([0, w]);
            const y = d3.scaleLinear().domain([0, maxValue * 1.15]).nice().range([h, 0]);

            g.append('g').call(d3.axisLeft(y).ticks(5).tickSize(-w).tickFormat(d3.format('~s')))
                .call(s => s.select('.domain').remove())
                .call(s => s.selectAll('.tick line').attr('stroke', ctx.c.grid))
                .call(s => s.selectAll('text').attr('fill', ctx.c.muted).attr('font-size', '10px'));

            g.append('g').attr('transform', `translate(0,${h})`)
                .call(d3.axisBottom(x).ticks(6).tickFormat(d3.timeFormat('%d %b')))
                .call(s => s.select('.domain').attr('stroke', ctx.c.grid))
                .call(s => s.selectAll('.tick line').attr('stroke', ctx.c.grid))
                .call(s => s.selectAll('text').attr('fill', ctx.c.muted).attr('font-size', '10px'));

            if (forecast.length > 0) {
                // Confidence band first so the lines draw on top of it.
                g.append('path').datum(forecast)
                    .attr('fill', ctx.c.warning).attr('fill-opacity', 0.15)
                    .attr('d', d3.area().x(d => x(d.date)).y0(d => y(d.lower)).y1(d => y(d.upper))
                        .curve(d3.curveMonotoneX));

                g.append('path').datum(forecast)
                    .attr('fill', 'none').attr('stroke', ctx.c.warning)
                    .attr('stroke-width', 2).attr('stroke-dasharray', '5,4')
                    .attr('d', d3.line().x(d => x(d.date)).y(d => y(d.value)).curve(d3.curveMonotoneX));

                g.selectAll('.peak').data(forecast.filter(d => d.peak)).enter().append('circle')
                    .attr('class', 'peak')
                    .attr('cx', d => x(d.date)).attr('cy', d => y(d.value)).attr('r', 4)
                    .attr('fill', ctx.c.danger);
            }

            if (history.length > 0) {
                g.append('path').datum(history)
                    .attr('fill', 'none').attr('stroke', ctx.c.accent).attr('stroke-width', 2)
                    .attr('d', d3.line().x(d => x(d.date)).y(d => y(d.value)).curve(d3.curveMonotoneX));
            }

            const points = history.map(d => Object.assign({ kind: 'Aktual' }, d))
                .concat(forecast.map(d => Object.assign({ kind: 'Prediksi' }, d)));
            const band = w / Math.max(points.length, 1);

            g.selectAll('.hit').data(points).enter().append('rect')
                .attr('class', 'hit')
                .attr('x', d => x(d.date) - band / 2).attr('y', 0)
                .attr('width', band).attr('height', h).attr('fill', 'transparent')
                .on('mousemove', (event, d) => showTip(event,
                    `<strong>${d3.timeFormat('%d %b %Y')(d.date)}</strong><br/>${d.kind}: ${d.value.toFixed(0)} order` +
                    (d.kind === 'Prediksi' ? `<br/>Rentang: ${d.lower.toFixed(0)}–${d.upper.toFixed(0)}` +
                        (d.peak ? '<br/>🔥 Peak season' : '') : '')))
                .on('mouseleave', hideTip);
        });
    }

    /**
     * Sparkline for stat tiles. values = [numbers]
     */
    function sparkline(elementId, values, colorVar) {
        register(elementId, function () {
            const ctx = prepare(elementId);
            if (!ctx || !values || values.length < 2) return;

            const color = cssVar(colorVar || '--accent', '#1877f2');
            const x = d3.scaleLinear().domain([0, values.length - 1]).range([1, ctx.width - 1]);
            const y = d3.scaleLinear().domain(d3.extent(values)).range([ctx.height - 2, 2]);

            ctx.svg.append('path').datum(values)
                .attr('fill', 'none').attr('stroke', color).attr('stroke-width', 1.5)
                .attr('d', d3.line().x((d, i) => x(i)).y(d => y(d)).curve(d3.curveMonotoneX));
        });
    }

    /** Redraw every registered chart, e.g. after a theme toggle. */
    function refreshAll() {
        registry.forEach(draw => draw());
    }

    /** Drop charts whose container has left the DOM (Blazor page navigation). */
    function dispose(elementId) {
        if (elementId) {
            registry.delete(elementId);
            return;
        }
        registry.forEach((_, id) => {
            if (!document.getElementById(id)) registry.delete(id);
        });
    }

    window.ngibridCharts = {
        lineChart, donutChart, barChart, forecastChart, sparkline, refreshAll, dispose
    };
})();
