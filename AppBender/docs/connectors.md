# Connectors

Connectors let workflows and the sync service talk to external systems. A **provider** is the
implementation; a **connection** (ConnectorDefinition) is a configured instance with credentials.

## Built-in providers

| Provider | Category | Actions | Config |
|---|---|---|---|
| `rest` | API | get/post/put/patch/delete | baseUrl, headers, bearerToken |
| `graphql` | API | query | endpoint, headers, bearerToken |
| `sqlserver` / `postgres` / `mysql` / `sqlite` | Database | query / execute / scalar (named `@params` from input) | connectionString |
| `s3` (MinIO-compatible) | Storage | list / read_text / write_text / delete | serviceUrl, region, accessKey, secretKey, bucket |
| `azureblob` | Storage | list / read_text / write_text / delete | connectionString, container |
| `filesystem` | Storage | list / read_text / write_text / delete | basePath (sandboxed) |
| `smtp` | Messaging | send | Host, Port, Username, Password, From, UseSsl |
| `webhook` | Messaging | post_message / post_json (Slack, Teams, Discord) | url, format |
| `tavily` | AI | search | apiKey |
| `custom` | Custom | defined by the JSON spec | apiKey / username / password |

## Custom Connector Builder

Create HTTP connectors declaratively — no code. A `CustomConnectorSpec`:

```json
{
  "baseUrl": "https://api.github.com",
  "authType": "bearer",                    // none | apikey_header | apikey_query | bearer | basic
  "authParamName": "X-Api-Key",            // for apikey_* types
  "defaultHeaders": { "User-Agent": "AppBender" },
  "actions": [
    {
      "key": "get_repo",
      "name": "Get repository info",
      "method": "GET",
      "pathTemplate": "/repos/{{owner}}/{{repo}}",   // {{param}} from action input
      "inputs": ["owner", "repo"]
    },
    {
      "key": "create_issue",
      "method": "POST",
      "pathTemplate": "/repos/{{owner}}/{{repo}}/issues",
      "bodyTemplate": "{\"title\": \"{{title}}\", \"body\": \"{{body}}\"}",
      "inputs": ["owner", "repo", "title", "body"]
    }
  ]
}
```

Seeded examples: **Open-Meteo Weather** (no auth) and **GitHub API** (bearer).

## Using connectors

- **Test console** — 🧪 on any connection: pick an action, pass JSON input, see the result.
- **Workflows** — the *Connector Action* step (`connectorId`, `action`, `input` JSON with templates).
- **Data sync** — `IDataSyncService.PullAsync/PushAsync` (see [data-hub.md](data-hub.md)).

## Adding a new built-in provider

Implement `IConnector` (one class: `Provider` key, `ConfigKeys`, `Actions`, `ExecuteAsync`)
and register it in `Program.cs`. ~50 lines for a typical API.
