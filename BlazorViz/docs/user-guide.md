# User Guide

> 🇮🇩 Versi Bahasa Indonesia: [user-guide.id.md](user-guide.id.md)

## 1. Signing in

Run the app (`dotnet run --project src/BlazorViz`) and log in with a seeded account
(`admin@blazorviz.local` / `Admin123!`) or register a new one. Roles:

- **Viewer** — view dashboards, chat with the Data Wizard.
- **Analyst** — everything above + create connections, datasets and dashboards.
- **Admin** — everything + user management, audit logs, usage, performance, settings.

Use the 🌙/☀️ button at the bottom of the sidebar to switch dark/light theme.

## 2. Connections

*Connections* hold credentials/locations for data sources. Go to **Connections → New connection**,
pick a kind and fill the JSON config (a hint for each kind is shown under the editor). Click **Test**
to verify. Supported kinds: `sqlite`, `sqlserver`, `postgresql`, `mysql`, `oracle`, `excel`, `csv`,
`rest`, `graphql`.

## 3. Datasets

A *dataset* = source + optional ETL steps + optional script.

1. **Datasets → New dataset**, name it.
2. Either pick a **connection** and write a query (SQL for databases, GraphQL document, REST path,
   or Excel sheet name), or choose **Uploaded file** and upload a CSV/Excel/JSON file.
3. Optionally add **ETL steps** — each step is `op` + parameters like
   `field=Region; op==; value=North`. See [etl-and-scripting.md](etl-and-scripting.md).
4. Optionally add a **script** (C#, JavaScript or Python). Pick a template from the dropdown and modify it.
5. Click **Preview** to see the result, column types, and suggested visualizations. **Save**.

Set **Auto refresh** (seconds) to make dashboard panels re-query the source periodically.

## 4. Dashboards

- **Dashboards → Create**, then you land in the **designer**.
- **＋ Panel** adds a panel; drag by its title, resize from the bottom-right corner.
- **✏️ on a panel** opens the editor: choose dataset, chart type (24 types incl. `custom`),
  X/Y/series fields, aggregation, sort/limit. Maps need lat/lng fields; bubbles a size field.
- **custom** panels: pick ECharts / Chart.js / D3 and write a function body receiving
  `(el, rows, columns, lib, palette)` — render anything into `el`.
- **Tabs**: ＋ Tab to add, rename inline; each tab has its own panel grid.
- **Filters**: add slicer / dropdown / multi-select / date-range bound to a dataset field.
  Filters apply to every panel using that dataset.
- **💡 Suggest** adds a recommended chart based on your data's column types.
- **Save** creates a new version. **🕘 Versions** lists history — **Rollback** restores any version.
- **Public link**: tick *Public link*, then **🔗 Copy link** (`/share/{token}`, no login required)
  or **📋 Embed code** (iframe snippet for other applications).

Each panel header has ⬇️ CSV export and 🖼️ PNG image export; the REST API exports CSV/JSON/Excel/PDF.

## 5. Data Wizard (AI chat)

Open **Data Wizard**. The assistant can:

- query your datasets & dashboards (schemas, previews, aggregations, filters) via built-in tools,
- calculate math, check dates, search the internet, scrape web pages,
- use excerpts from your indexed RAG documents automatically,
- receive **attachments**: images become image content; documents are linked and their text inlined.

Replies stream in and render as markdown (tables, code blocks, images). Sessions are saved per user in
the left column. Provider/model/persona/temperature come from the `Ai` section of `appsettings.json`.

## 6. Documents (RAG)

**Documents (RAG)** → upload PDF / Word / Excel / text files. They are chunked, embedded and indexed
into the configured vector store. Use the *Search test* box to check retrieval. Indexed documents are
used automatically by the Data Wizard as context.

## 7. Predictive analytics

**Predictive** → pick a task:

- **Forecasting** — choose a time column, numeric column and horizon; renders history + forecast.
- **Regression** — choose target and feature columns; shows an actual-vs-predicted scatter with R²/RMSE.
- **Clustering** — choose feature columns and k; appends a `Cluster` column to the result table.

## 8. Admin

- **Users** — create users, toggle roles per user, delete.
- **Audit Logs** — filter by user/category/date, sort by column.
- **Usage** — queries, chats, estimated tokens, API calls; charts per day; top users.
- **Performance** — live response times, traffic per path, memory/CPU/uptime (refreshes every 5 s).
- **Settings** — AI/storage config overview, plugin status + reload, and **API key management**.

## 9. External API

Generate a key in **Admin → Settings**, then call `/api/v1/...` with the `X-Api-Key` header.
Interactive documentation: **`/swagger`**. See [api.md](api.md).
