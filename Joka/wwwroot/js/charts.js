// Joka - D3 chart renderers driven from Blazor (Components/Shared/Chart.razor).
//
// Colours are never hardcoded here: every fill and stroke is read from the CSS
// custom properties on :root, so a chart follows the light/dark toggle for free.
// The theme switch fires `joka:theme` (see joka.js) and every live chart redraws.
(function () {

    const charts = new Map();   // element id -> spec, so we can redraw on theme change

    function token(name, fallback) {
        const v = getComputedStyle(document.documentElement).getPropertyValue(name);
        return v && v.trim() ? v.trim() : fallback;
    }

    function palette() {
        return {
            primary: token('--accent-primary', '#FF5C35'),
            secondary: token('--accent-secondary', '#FFB800'),
            success: token('--accent-success', '#22A06B'),
            danger: token('--accent-danger', '#D64545'),
            info: token('--accent-info', '#3B82F6'),
            text: token('--text-primary', '#1A1A1A'),
            muted: token('--text-muted', '#8A8A8A'),
            grid: token('--border-color', '#E5E5E5'),
            strong: token('--border-strong', '#1A1A1A'),
            surface: token('--bg-secondary', '#FFFFFF')
        };
    }

    // Rotating series colours for donut / multi-category charts.
    function seriesColours(p) {
        return [p.primary, p.secondary, p.info, p.success, p.danger, p.muted];
    }

    function shortNumber(n) {
        const abs = Math.abs(n);
        if (abs >= 1e12) return (n / 1e12).toFixed(1).replace('.0', '') + 'T';
        if (abs >= 1e9) return (n / 1e9).toFixed(1).replace('.0', '') + 'M';   // miliar
        if (abs >= 1e6) return (n / 1e6).toFixed(1).replace('.0', '') + 'jt';
        if (abs >= 1e3) return (n / 1e3).toFixed(0) + 'rb';
        return String(n);
    }

    function formatValue(v, format) {
        if (format === 'currency') return 'Rp' + shortNumber(v);
        if (format === 'compact') return shortNumber(v);
        return new Intl.NumberFormat('id-ID').format(v);
    }

    // ---- shared chrome -------------------------------------------------
    function frame(host, height) {
        host.innerHTML = '';
        const width = host.clientWidth || 600;
        const svg = d3.select(host).append('svg')
            .attr('width', '100%')
            .attr('height', height)
            .attr('viewBox', `0 0 ${width} ${height}`)
            .attr('preserveAspectRatio', 'xMidYMid meet');
        return { svg, width, height };
    }

    function tooltip(host) {
        let el = host.querySelector('.chart-tip');
        if (!el) {
            el = document.createElement('div');
            el.className = 'chart-tip';
            host.appendChild(el);
        }
        return {
            show(html, x, y) {
                el.innerHTML = html;
                el.style.opacity = '1';
                el.style.left = x + 'px';
                el.style.top = y + 'px';
            },
            hide() { el.style.opacity = '0'; }
        };
    }

    // ---- bar -----------------------------------------------------------
    function bar(host, spec) {
        const p = palette();
        const { svg, width, height } = frame(host, spec.height);
        const tip = tooltip(host);

        const m = { top: 12, right: 12, bottom: 34, left: 52 };
        const w = Math.max(10, width - m.left - m.right);
        const h = Math.max(10, height - m.top - m.bottom);
        const g = svg.append('g').attr('transform', `translate(${m.left},${m.top})`);

        const x = d3.scaleBand().domain(spec.data.map(d => d.label)).range([0, w]).padding(0.28);
        const max = d3.max(spec.data, d => d.value) || 1;
        const y = d3.scaleLinear().domain([0, max * 1.1]).nice().range([h, 0]);

        g.append('g').call(d3.axisLeft(y).ticks(4).tickFormat(v => formatValue(v, spec.format)))
            .call(s => s.select('.domain').remove())
            .call(s => s.selectAll('line').attr('stroke', p.grid).attr('x2', w))
            .call(s => s.selectAll('text').attr('fill', p.muted).style('font-size', '10px'));

        g.append('g').attr('transform', `translate(0,${h})`)
            .call(d3.axisBottom(x).tickSize(0))
            .call(s => s.select('.domain').attr('stroke', p.grid))
            .call(s => s.selectAll('text').attr('fill', p.muted).style('font-size', '10px').attr('dy', '1em'));

        g.selectAll('rect.bar').data(spec.data).enter().append('rect')
            .attr('class', 'bar')
            .attr('x', d => x(d.label))
            .attr('width', x.bandwidth())
            .attr('y', h)
            .attr('height', 0)
            .attr('fill', p.primary)
            .attr('stroke', p.strong)
            .attr('stroke-width', 1.5)
            .attr('rx', 3)
            .on('mousemove', function (event, d) {
                d3.select(this).attr('fill', p.secondary);
                const [mx, my] = d3.pointer(event, host);
                tip.show(`<strong>${d.label}</strong><br>${formatValue(d.value, spec.format)}`, mx + 12, my - 8);
            })
            .on('mouseleave', function () { d3.select(this).attr('fill', p.primary); tip.hide(); })
            .transition().duration(500).delay((d, i) => i * 28)
            .attr('y', d => y(d.value))
            .attr('height', d => h - y(d.value));
    }

    // ---- line (with area fill) -----------------------------------------
    function line(host, spec) {
        const p = palette();
        const { svg, width, height } = frame(host, spec.height);
        const tip = tooltip(host);

        const m = { top: 12, right: 12, bottom: 34, left: 52 };
        const w = Math.max(10, width - m.left - m.right);
        const h = Math.max(10, height - m.top - m.bottom);
        const g = svg.append('g').attr('transform', `translate(${m.left},${m.top})`);

        const x = d3.scalePoint().domain(spec.data.map(d => d.label)).range([0, w]).padding(0.5);
        const max = d3.max(spec.data, d => d.value) || 1;
        const y = d3.scaleLinear().domain([0, max * 1.15]).nice().range([h, 0]);

        g.append('g').call(d3.axisLeft(y).ticks(4).tickFormat(v => formatValue(v, spec.format)))
            .call(s => s.select('.domain').remove())
            .call(s => s.selectAll('line').attr('stroke', p.grid).attr('x2', w))
            .call(s => s.selectAll('text').attr('fill', p.muted).style('font-size', '10px'));

        // Crowded date axes get every other label, otherwise they overlap.
        const step = spec.data.length > 10 ? Math.ceil(spec.data.length / 8) : 1;
        g.append('g').attr('transform', `translate(0,${h})`)
            .call(d3.axisBottom(x).tickSize(0)
                .tickValues(spec.data.filter((d, i) => i % step === 0).map(d => d.label)))
            .call(s => s.select('.domain').attr('stroke', p.grid))
            .call(s => s.selectAll('text').attr('fill', p.muted).style('font-size', '10px').attr('dy', '1em'));

        const area = d3.area().x(d => x(d.label)).y0(h).y1(d => y(d.value)).curve(d3.curveMonotoneX);
        const path = d3.line().x(d => x(d.label)).y(d => y(d.value)).curve(d3.curveMonotoneX);

        g.append('path').datum(spec.data).attr('d', area).attr('fill', p.primary).attr('opacity', 0.12);

        const stroke = g.append('path').datum(spec.data)
            .attr('d', path).attr('fill', 'none')
            .attr('stroke', p.primary).attr('stroke-width', 2.5)
            .attr('stroke-linecap', 'round');

        const len = stroke.node().getTotalLength();
        stroke.attr('stroke-dasharray', `${len} ${len}`).attr('stroke-dashoffset', len)
            .transition().duration(700).attr('stroke-dashoffset', 0);

        g.selectAll('circle').data(spec.data).enter().append('circle')
            .attr('cx', d => x(d.label)).attr('cy', d => y(d.value))
            .attr('r', 4).attr('fill', p.surface)
            .attr('stroke', p.primary).attr('stroke-width', 2)
            .on('mousemove', function (event, d) {
                d3.select(this).attr('r', 6);
                const [mx, my] = d3.pointer(event, host);
                tip.show(`<strong>${d.label}</strong><br>${formatValue(d.value, spec.format)}`, mx + 12, my - 8);
            })
            .on('mouseleave', function () { d3.select(this).attr('r', 4); tip.hide(); });
    }

    // ---- donut ---------------------------------------------------------
    function donut(host, spec) {
        const p = palette();
        const colours = seriesColours(p);
        const { svg, width, height } = frame(host, spec.height);
        const tip = tooltip(host);

        const radius = Math.min(width, height) / 2 - 6;
        const g = svg.append('g').attr('transform', `translate(${width / 2},${height / 2})`);

        const total = d3.sum(spec.data, d => d.value) || 1;
        const pie = d3.pie().sort(null).value(d => d.value);
        const arc = d3.arc().innerRadius(radius * 0.58).outerRadius(radius);

        g.selectAll('path').data(pie(spec.data)).enter().append('path')
            .attr('fill', (d, i) => colours[i % colours.length])
            .attr('stroke', p.strong).attr('stroke-width', 1.5)
            .on('mousemove', function (event, d) {
                d3.select(this).attr('opacity', 0.82);
                const [mx, my] = d3.pointer(event, host);
                const pct = ((d.data.value / total) * 100).toFixed(1);
                tip.show(`<strong>${d.data.label}</strong><br>${formatValue(d.data.value, spec.format)} · ${pct}%`, mx + 12, my - 8);
            })
            .on('mouseleave', function () { d3.select(this).attr('opacity', 1); tip.hide(); })
            .transition().duration(600)
            .attrTween('d', function (d) {
                const i = d3.interpolate({ startAngle: 0, endAngle: 0 }, d);
                return t => arc(i(t));
            });

        g.append('text').attr('text-anchor', 'middle').attr('dy', '-0.1em')
            .attr('fill', p.text).style('font-size', '1.15rem').style('font-weight', '700')
            .text(formatValue(total, spec.format));

        g.append('text').attr('text-anchor', 'middle').attr('dy', '1.3em')
            .attr('fill', p.muted).style('font-size', '0.7rem')
            .text(spec.totalLabel || 'Total');
    }

    // ---- horizontal bar (long category names) --------------------------
    function hbar(host, spec) {
        const p = palette();
        const colours = seriesColours(p);
        const { svg, width, height } = frame(host, spec.height);
        const tip = tooltip(host);

        const m = { top: 8, right: 56, bottom: 8, left: 130 };
        const w = Math.max(10, width - m.left - m.right);
        const h = Math.max(10, height - m.top - m.bottom);
        const g = svg.append('g').attr('transform', `translate(${m.left},${m.top})`);

        const y = d3.scaleBand().domain(spec.data.map(d => d.label)).range([0, h]).padding(0.25);
        const max = d3.max(spec.data, d => d.value) || 1;
        const x = d3.scaleLinear().domain([0, max * 1.05]).range([0, w]);

        g.append('g').call(d3.axisLeft(y).tickSize(0))
            .call(s => s.select('.domain').remove())
            .call(s => s.selectAll('text').attr('fill', p.text).style('font-size', '10px'));

        g.selectAll('rect').data(spec.data).enter().append('rect')
            .attr('y', d => y(d.label)).attr('height', y.bandwidth())
            .attr('x', 0).attr('width', 0)
            .attr('fill', (d, i) => colours[i % colours.length])
            .attr('stroke', p.strong).attr('stroke-width', 1.5).attr('rx', 3)
            .on('mousemove', function (event, d) {
                const [mx, my] = d3.pointer(event, host);
                tip.show(`<strong>${d.label}</strong><br>${formatValue(d.value, spec.format)}`, mx + 12, my - 8);
            })
            .on('mouseleave', () => tip.hide())
            .transition().duration(500).delay((d, i) => i * 40)
            .attr('width', d => x(d.value));

        g.selectAll('text.val').data(spec.data).enter().append('text')
            .attr('class', 'val')
            .attr('x', d => x(d.value) + 6)
            .attr('y', d => y(d.label) + y.bandwidth() / 2)
            .attr('dy', '0.35em')
            .attr('fill', p.muted).style('font-size', '10px')
            .text(d => formatValue(d.value, spec.format));
    }

    const renderers = { bar, line, donut, hbar };

    function draw(host, spec) {
        if (!host || typeof d3 === 'undefined') return;
        if (!spec.data || spec.data.length === 0) {
            host.innerHTML = `<div class="chart-empty">${spec.emptyText || 'Belum ada data'}</div>`;
            return;
        }
        (renderers[spec.type] || bar)(host, spec);
    }

    window.joka = window.joka || {};
    window.joka.charts = {
        render(host, spec) {
            if (!host) return;
            draw(host, spec);
            charts.set(host, spec);
        },
        dispose(host) {
            charts.delete(host);
        }
    };

    // Redraw everything when the theme flips or the window resizes.
    let resizeTimer;
    function redrawAll() {
        charts.forEach((spec, host) => {
            if (document.body.contains(host)) draw(host, spec);
            else charts.delete(host);
        });
    }

    window.addEventListener('joka:theme', redrawAll);
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(redrawAll, 180);
    });
})();
