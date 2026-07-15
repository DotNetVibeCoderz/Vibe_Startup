# Data API

Swagger UI: **`/swagger`**. Authentication: session cookie (browser) or `X-Api-Key` header
(keys under `Api:Keys` in appsettings; mapped to a Developer-role principal scoped to the key's tenant).

## REST

| Method & path | Description |
|---|---|
| `GET /api/data/entities` | entity definitions (schema) |
| `GET /api/data/{entity}` | query records |
| `GET /api/data/{entity}/{id}` | one record |
| `POST /api/data/{entity}` | create (JSON body of field values) |
| `PUT /api/data/{entity}/{id}` | partial update |
| `DELETE /api/data/{entity}/{id}` | delete |

Query parameters for `GET /api/data/{entity}`:

- `search` — free text across all fields
- `filter` — clauses `field op value` joined with ` and `; ops: `eq neq gt gte lt lte contains startswith isnull notnull`
  e.g. `filter=status eq Active and total gt 100000`
- `sortBy`, `desc`, `page`, `pageSize`

```bash
curl -H "X-Api-Key: dev-api-key-change-me" \
  "http://localhost:5210/api/data/orders?filter=status eq New&sortBy=order_date&desc=true"
```

## GraphQL (`POST /api/graphql`)

Body: `{ "query": "..." }`. Grammar (lightweight, entity-oriented):

```graphql
# query: entity name as field; args: id, top, page, search, sortBy, desc, filter
{ customers(top: 10, search: "budi", filter: "status eq Active") { id name email } }
{ orders(id: "abc123") { * } }          # * or empty selection = all fields

# mutations: create_/update_/delete_ + entity name
mutation {
  create_customers(data: { name: "Jo", email: "jo@x.io" }) { id }
  update_customers(id: "abc", data: { status: "Active" }) { id status }
  delete_customers(id: "abc") { id }
}
```

## Webhooks

`POST|GET /api/webhooks/{key}` fires the enabled Webhook-trigger workflow whose `webhookKey`
matches. The workflow sees `{{trigger.body}}`, `{{trigger.query}}`, `{{trigger.headers}}`,
`{{trigger.method}}`. If the workflow runs a **Respond to Webhook** step, its payload becomes
the HTTP response.

## Files & packages

- `POST /api/files/upload` (multipart) → `{ fileName, url, contentType, isImage }`
- `GET /api/package/export?includeRecords=true` → full workspace JSON
- `POST /api/package/import` (multipart JSON file) → import summary

## Errors

`400` validation (`{ "error": "..." }`), `401/403` auth, `404` unknown entity/record.
