# REST API Reference

Base URL: `/api/v1` — interactive docs at **`/swagger`**.

## Authentication

Every request needs an `X-Api-Key` header. Keys are managed in **Admin → Settings** (a demo key is seeded
on first run). Requests without a valid, enabled key get `401`.

```bash
KEY="bv-…"
curl -H "X-Api-Key: $KEY" http://localhost:5199/api/v1/datasets
```

## Endpoints

### `GET /datasets`
Lists datasets: `[{ id, name, description, sourceKind, refreshIntervalSeconds, lastRefreshedUtc }]`.

### `GET /datasets/{id}/schema`
Column names/types and row count.

### `GET /datasets/{id}/data?limit=100`
Executes the dataset (source → ETL → script, cached) and returns
`{ columns: [{name, type}], rows: [{col: value, …}] }`.

### `POST /datasets/{id}/query`
Ad-hoc query on top of the dataset result.

```json
{
  "groupBy": "Region,Category",          // optional — with "aggs"
  "aggs": "sum:Revenue,avg:Price,count:*",
  "filterField": "Region",               // optional filter
  "filterOp": "=",                       // = != > >= < <= contains startswith endswith in notnull isnull
  "filterValue": "North",
  "sortBy": "sum_Revenue",
  "sortDesc": true,
  "limit": 100
}
```

Example:

```bash
curl -X POST -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"groupBy":"Region","aggs":"sum:Revenue,count:*","sortBy":"sum_Revenue","sortDesc":true}' \
  http://localhost:5199/api/v1/datasets/1/query
```

### `GET /datasets/{id}/export?format=csv|json|excel|pdf`
Streams the dataset as a file download.

### `GET /dashboards`
Lists dashboards incl. `shareUrl` for public ones.

### `GET /dashboards/{id}`
Full dashboard definition (tabs, panels, filters).

### `POST /chat`
One-shot Data Wizard call (non-streaming).

```json
{ "message": "Which region has the highest revenue?", "sessionId": null }
```

Response: `{ "sessionId": 12, "reply": "…markdown…" }`. Pass the returned `sessionId` in follow-up calls
to keep the conversation context.

## Usage metering

Every API call is recorded as a `usage` metric (kind `api`) and shows up in **Admin → Usage**.
