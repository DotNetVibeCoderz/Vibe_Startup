# Workflows

Power-Automate-style automation: **trigger → actions → conditions → output**.

## Triggers

| Trigger | Config | Fires when |
|---|---|---|
| Manual | — | ▶ button / Test Run |
| Schedule | `cron` (5-field, UTC) | cron matches (checked every 20 s) |
| Webhook | `webhookKey` | `POST/GET /api/webhooks/{key}` |
| EntityCreated/Updated/Deleted | `entityName` (optional) | Data Hub record changes |
| FormSubmitted | `formId` (optional) | a form is submitted |

Trigger data is available as `{{trigger.*}}` — e.g. webhook body `{{trigger.body.x}}`,
entity record `{{trigger.record.name}}`, form values `{{trigger.values.email}}`.

## Templates

Any config value may contain `{{expressions}}`:

- `{{trigger.body.items[0].sku}}` — nested paths and array indexing
- `{{vars.total}}` — variables set with *Set Variable*
- `{{steps.step_name.output}}` — outputs of earlier steps (name is slugified)
- `{{utcNow}}`, `{{now}}`, `{{today}}`, `{{guid}}`, `{{rand}}`

## Actions (35+)

**Control**: Condition (if/else), Switch, For Each, Do Until, Scope, Delay, Terminate
**Data Hub**: Query / Get / Create / Update / Delete records
**Integration**: HTTP Request, Connector Action, Send Email (SMTP), Respond to Webhook, Call Workflow
**Variables**: Set Variable, Compose, Math Expression, Transform (Map), Log
**Code**: Run C# (Roslyn), Run JavaScript (Jint), Run Python (local interpreter)
**AI**: Ask AI (LLM), Summarize, Extract JSON, Classify, Generate Code, Vision/Describe Image,
OCR, Generate Image, Transcribe Audio, Search Internet (Tavily), Scrape URL, Knowledge-Base Query (RAG)
**Files**: Write File, Read File (via the configured storage provider)

### Scripting contract

- **C#**: the run context is `Context` (`IDictionary<string, object?>`); `return` a value.
- **JavaScript**: context is `context`; the last expression is the result.
- **Python**: context is `context` (parsed from stdin); assign to `result`.

## Designer

- Palette (left) → click to add to the current target (root by default; every branch has a
  **➕ add** button that makes it the target).
- Condition steps hold **TRUE/FALSE** branches; Switch holds named cases; For Each / Do Until /
  Scope hold children.
- **{} JSON** opens the raw steps JSON for advanced editing (dual-mode).
- **▶ Test Run** executes immediately and prints per-step logs inline.

## Monitoring

Every run (any trigger) is stored with status, duration, input, output, and step-level logs —
see **Workflow Runs** (`/workflows/runs`). Failures record the error and the failing step.

## Example: retry pattern

```json
{"type":"do_until","name":"Retry until ok",
 "config":{"left":"{{steps.call_api.output.isSuccess}}","op":"eq","right":"true","maxIterations":"5"},
 "children":[
   {"type":"http_request","name":"Call API","config":{"method":"GET","url":"https://example.com/health"}},
   {"type":"delay","config":{"seconds":"2"}}]}
```
