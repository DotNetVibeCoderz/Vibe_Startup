# Plugin System

Plugins add **custom kernel functions** to the Data Wizard — tools the AI can call during a chat,
next to the built-in math / datetime / web / data tools.

## How it works

- Every `*.csx` file in the `plugins/` folder (next to the app's content root) is compiled with Roslyn
  at startup.
- The script must **end with an expression returning an object**. Every public method on that object
  decorated with `[KernelFunction]` becomes a tool.
- The plugin name is the file name (dashes become underscores).
- Load status and errors are visible in **Admin → Settings**, where you can also **Reload plugins**
  without restarting.

## Anatomy of a plugin

```csharp
// plugins/my-tools.csx
using System.ComponentModel;
using Microsoft.SemanticKernel;

public class MyTools
{
    [KernelFunction("slugify")]
    [Description("Converts a title into a URL-friendly slug.")]
    public string Slugify([Description("the text to slugify")] string text) =>
        string.Join("-", text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    [KernelFunction("fiscal_quarter")]
    [Description("Returns the fiscal quarter (FY starts in July) for a date yyyy-MM-dd.")]
    public string FiscalQuarter(string date)
    {
        if (!DateTime.TryParse(date, out var d)) return "Invalid date.";
        var q = ((d.Month + 5) % 12) / 3 + 1;
        return $"FQ{q}";
    }
}

return new MyTools();
```

Guidelines:

- Always add `[Description]` to the function **and** its parameters — that's what the LLM reads to decide
  when and how to call your tool.
- Keep return values short and textual (the result is inserted into the model's context).
- Functions may be `async Task<string>`.
- Available imports by default: `System`, `System.Linq`, `System.Collections.Generic`,
  `System.ComponentModel`, `Microsoft.SemanticKernel`.

## Included sample

[`plugins/sample-formatter.csx`](../src/BlazorViz/plugins/sample-formatter.csx) ships with the app:
`format_currency` (IDR/USD/EUR), `format_bytes`, and `check_indonesian_holiday`. Ask the Data Wizard
*"format 1234567.89 as IDR"* to see it in action.
