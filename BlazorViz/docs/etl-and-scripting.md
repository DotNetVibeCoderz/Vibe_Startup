# ETL & Scripting

Datasets are transformed in two optional stages after the source is loaded:
**ETL steps** (declarative, composed in the UI) then a **script** (C#, JavaScript or Python).

## ETL steps

Each step is an operation plus parameters (`key=value` pairs separated by `;` in the editor).

| Op | Parameters | Example |
|----|-----------|---------|
| `filter` | `field`, `op`, `value` | `field=Region; op==; value=North` |
| `select` | `fields` (comma separated, keep) | `fields=Region,Revenue` |
| `drop` | `fields` (comma separated, remove) | `fields=InternalId` |
| `rename` | `from`, `to` | `from=Qty; to=Quantity` |
| `compute` | `name`, `expr` (JavaScript, row fields as variables) | `name=Margin; expr=Profit / Revenue` |
| `sort` | `field`, `desc` | `field=Revenue; desc=true` |
| `aggregate` | `groupBy`, `aggs` | `groupBy=Region; aggs=sum:Revenue,avg:Price,count:*` |
| `join` | `rightDatasetId`, `leftKey`, `rightKey`, `type` (inner/left), `prefix` | `rightDatasetId=2; leftKey=Id; rightKey=CustomerId; type=left` |
| `limit` | `n` | `n=100` |
| `distinct` | — | |
| `pivot` | `rowField`, `columnField`, `valueField`, `agg` | `rowField=Region; columnField=Category; valueField=Revenue; agg=sum` |

Filter operators: `=`, `!=`, `>`, `>=`, `<`, `<=`, `contains`, `startswith`, `endswith`,
`in` (comma-separated list), `notnull`, `isnull`.

Aggregate functions: `sum`, `avg`, `min`, `max`, `count` (use `count:*` for row count).
Aggregated columns are named `sum_Revenue`, `avg_Price`, `count`, ….

`join` pulls another dataset by id (circular joins are detected and rejected).

## Scripts

Scripts run **after** ETL steps and must produce the transformed rows. The editor offers 15+ built-in
templates per language (clean nulls, top-N per group, moving average, unpivot, outlier removal,
group & aggregate, date parts, dedupe, normalize, pandas pipeline, sampling, …) — pick one and modify.

### C# (Roslyn)

Globals: `Data` (`TableData`) and `Rows` (`List<Dictionary<string, object?>>`).
Return a `TableData` or a `List<Dictionary<string, object?>>`. Imports available:
`System`, `System.Linq`, `System.Collections.Generic`, `System.Text.Json`, `BlazorViz.Models`, `System.Math`.

```csharp
var top = Rows
    .GroupBy(r => r["Category"])
    .SelectMany(g => g.OrderByDescending(r => Convert.ToDouble(r["Revenue"] ?? 0)).Take(3))
    .ToList();
return top;
```

### JavaScript (Jint, sandboxed: 30 s timeout, 256 MB memory cap)

`rows` is an array of objects; `return` the transformed array.

```javascript
return rows
  .filter(r => (r.Revenue ?? 0) > 100)
  .map(r => ({ ...r, RevenueK: Math.round((r.Revenue ?? 0) / 100) / 10 }));
```

### Python (external `python` process)

`rows` (list of dicts) is pre-loaded; **print** the result as JSON. Any installed package (pandas, numpy…)
is available.

```python
import pandas as pd
df = pd.DataFrame(rows)
df = df.groupby(["Region"], as_index=False)["Revenue"].sum()
print(df.to_json(orient="records"))
```

## Tips

- Use **Preview** in the dataset editor to inspect the result and inferred column types at every stage.
- Column types (`string` / `integer` / `number` / `datetime` / `boolean`) are re-inferred after each stage;
  numeric/date strings are converted to real types automatically.
- Prefer ETL steps for simple shaping (they are cheaper and self-documenting); scripts for logic that
  steps can't express.
