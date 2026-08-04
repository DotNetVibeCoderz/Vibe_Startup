// Captures the screenshots used in the documentation.
//
// Everything here drives the real console through a real browser: it signs in with the
// seeded admin account and navigates as an operator would, so the images stay honest — they
// are the product, not a mockup of it.
//
//   npm install playwright        (once; then: npx playwright install chromium)
//   node scripts/screenshots.mjs
//
// Expects the API and the console to be running, and ideally a simulator run beforehand so
// the panels have something to show. See docs/images/README.md.

import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';

const BASE = process.env.ADMIN_URL ?? 'http://localhost:5003';
const OUT = process.env.OUT_DIR ?? 'docs/images';

const EMAIL = process.env.ADMIN_EMAIL ?? 'admin@fastride.com';
const PASSWORD = process.env.ADMIN_PASSWORD ?? 'Password123';

mkdirSync(OUT, { recursive: true });

const pages = [
  { name: 'admin-dashboard',         path: '/',                  ready: 'canvas#chart-revenue' },
  { name: 'admin-orders',            path: '/orders',            ready: 'table.grid-table' },
  { name: 'admin-drivers',           path: '/drivers',           ready: 'table.grid-table' },
  { name: 'admin-payments',          path: '/payments',          ready: 'table.grid-table' },
  { name: 'admin-reports',           path: '/reports',           ready: 'canvas#report-revenue' },
  { name: 'admin-fares',             path: '/fares',             ready: '.panel' },
  { name: 'admin-promos',            path: '/promos',            ready: '.panel' },
  { name: 'admin-payment-providers', path: '/payment-providers', ready: '.panel' },
  { name: 'admin-verification',      path: '/verification',      ready: '.panel' },
];

const browser = await chromium.launch();

// 2× so the images stay sharp on a retina display without being enormous.
const viewport = { width: 1440, height: 900 };
const context = await browser.newContext({ viewport, deviceScaleFactor: 2, colorScheme: 'dark' });
const page = await context.newPage();

// ── sign in ──
await page.goto(BASE, { waitUntil: 'networkidle' });

// Blazor Server needs its circuit up before the form reacts to input.
await page.waitForSelector('#email', { timeout: 30_000 });
await page.waitForTimeout(1200);

await page.fill('#email', EMAIL);
await page.fill('#password', PASSWORD);
await page.click('button[type=submit]');
await page.waitForSelector('.console', { timeout: 30_000 });

console.log('signed in');

// The pulse rail and the dashboard both poll; let the first load land so nothing is
// captured mid-skeleton.
await page.waitForTimeout(4000);

// ── each page ──
for (const target of pages) {
  await page.goto(`${BASE}${target.path}`, { waitUntil: 'networkidle' });

  try {
    await page.waitForSelector(target.ready, { timeout: 15_000 });
  } catch {
    console.warn(`  ${target.name}: "${target.ready}" never appeared, capturing anyway`);
  }

  // Charts animate in.
  await page.waitForTimeout(2500);

  await page.screenshot({ path: `${OUT}/${target.name}.png` });
  console.log(`  ${target.name}.png`);
}

// ── light theme, one shot, to show the console supports both ──
await page.goto(`${BASE}/`, { waitUntil: 'networkidle' });
await page.waitForTimeout(1500);

// The theme toggle is the first button in the rail footer. Switching it reloads the page so
// the charts rebuild against the new palette.
await page.locator('.rail__foot button').first().click();
await page.waitForTimeout(3500);
await page.waitForSelector('canvas#chart-revenue', { timeout: 15_000 }).catch(() => {});
await page.waitForTimeout(2500);

await page.screenshot({ path: `${OUT}/admin-dashboard-light.png` });
console.log('  admin-dashboard-light.png');

// ── the sign-in screen itself, from a session that was never signed in ──
const anonymous = await browser.newContext({ viewport, deviceScaleFactor: 2, colorScheme: 'dark' });
const gate = await anonymous.newPage();

await gate.goto(BASE, { waitUntil: 'networkidle' });
await gate.waitForSelector('.gate__card', { timeout: 30_000 });
await gate.waitForTimeout(1500);

await gate.screenshot({ path: `${OUT}/admin-signin.png` });
console.log('  admin-signin.png');

await browser.close();
console.log('done');
