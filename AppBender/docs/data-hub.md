# Data Hub

The Data Hub is AppBender's Dataverse-like data layer: define entities visually, store records,
and get REST/GraphQL APIs for free.

## Entities & fields

Create entities at **Data Hub → New Entity** (or let AI do it in **AI Studio → Schema Generator**).

Field types:

| Type | Notes |
|---|---|
| Text / LongText / RichText | `MaxLength` supported |
| Number / Decimal / Currency | `Min`/`Max` validation |
| Boolean | accepts `true/false/1/0/yes` |
| Date / DateTime / Time | stored ISO-8601 |
| Choice / MultiChoice | options validated on write |
| Lookup | stores the id of a record in the target entity |
| Email / Url / Phone | basic format validation |
| File / Image | stores a URL |
| Json | free-form |
| **Formula** | computed on write, e.g. `qty * unit_price`, `round(subtotal * 1.11, 2)` |
| **AutoNumber** | sequential per entity |

## Relationships

Declared on the parent entity: `FromEntity` (parent) → `ToEntity` (child) with `LookupField`
naming the child's lookup field. Lookup fields alone already give you working dropdowns in forms.

## Records

- Browse/edit at **Data Hub → (entity)** with search, sort, paging, and validation errors inline.
- Every write is validated against the schema, audited, versioned (`record.Version`), and
  raises `EntityCreated/Updated/Deleted` events that can trigger workflows.

## Schema versioning & rollback

Every save creates a `VersionSnapshot`. In the entity designer press **🕘 Versions** to view
history and **↩ Restore** any version (the restore itself becomes a new version).

## Cross-connector sync

`IDataSyncService` moves data between connectors and entities:

- **Pull**: run any connector action that returns a list of objects and upsert into an entity by a
  key field, e.g. pull rows from PostgreSQL (`query` action) into `customers` keyed by `email`.
- **Push**: send every record of an entity through a connector action (one call per record).

Use it from a workflow (`run_csharp` step) or your own code:

```csharp
await sync.PullAsync("Local SQLite (AppBender DB)", "query",
    new() { ["sql"] = "SELECT ..." }, entityName: "customers", keyField: "email");
```

## API

See [api.md](api.md) — every entity is automatically available at `/api/data/{entity}` (REST)
and `/api/graphql` (GraphQL), documented in Swagger at `/swagger`.
