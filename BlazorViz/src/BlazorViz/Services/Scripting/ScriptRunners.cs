using System.Diagnostics;
using System.Text.Json;
using BlazorViz.Models;
using Jint;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace BlazorViz.Services.Scripting;

/// <summary>Globals exposed to C# dataset scripts.</summary>
public class CSharpScriptGlobals
{
    public TableData Data { get; set; } = new();
    /// <summary>Convenience row view: list of dictionaries (column → value).</summary>
    public List<Dictionary<string, object?>> Rows => Data.ToDictionaries();
}

public interface IScriptRunner
{
    string Language { get; }
    /// <summary>Transforms the table. Script errors surface as InvalidOperationException with a friendly message.</summary>
    Task<TableData> RunAsync(string script, TableData input, CancellationToken ct = default);
}

/// <summary>Runs C# scripts via Roslyn. Script receives `Data` (TableData) / `Rows` and returns TableData or row list.</summary>
public sealed class CSharpScriptRunner : IScriptRunner
{
    public string Language => "csharp";

    private static readonly ScriptOptions Options = ScriptOptions.Default
        .AddReferences(typeof(TableData).Assembly, typeof(Enumerable).Assembly, typeof(JsonSerializer).Assembly)
        .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Text.Json", "BlazorViz.Models", "System.Math");

    public async Task<TableData> RunAsync(string script, TableData input, CancellationToken ct = default)
    {
        try
        {
            var result = await CSharpScript.EvaluateAsync<object?>(script, Options,
                new CSharpScriptGlobals { Data = input }, cancellationToken: ct);
            return Coerce(result, input);
        }
        catch (CompilationErrorException ex)
        {
            throw new InvalidOperationException("C# script compile error: " + string.Join("\n", ex.Diagnostics));
        }
    }

    private static TableData Coerce(object? result, TableData fallback) => result switch
    {
        TableData t => t,
        IEnumerable<Dictionary<string, object?>> rows => TableData.FromDictionaries(rows),
        IEnumerable<IDictionary<string, object?>> rows => TableData.FromDictionaries(rows),
        null => fallback,
        _ => throw new InvalidOperationException($"C# script must return TableData or List<Dictionary<string,object?>> (got {result.GetType().Name}).")
    };
}

/// <summary>Runs JavaScript via Jint. Script receives `rows` (array of objects) and returns an array of objects.</summary>
public sealed class JsScriptRunner : IScriptRunner
{
    public string Language => "javascript";

    public Task<TableData> RunAsync(string script, TableData input, CancellationToken ct = default)
    {
        var engine = new Jint.Engine(o => o
            .TimeoutInterval(TimeSpan.FromSeconds(30))
            .LimitMemory(256 * 1024 * 1024)
            .LimitRecursion(256));
        engine.SetValue("rows", input.ToDictionaries());
        object? result;
        try
        {
            var wrapped = $"(function() {{ {script} }})()";
            result = engine.Evaluate(wrapped).ToObject();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("JavaScript error: " + ex.Message);
        }
        return Task.FromResult(Coerce(result, input));
    }

    private static TableData Coerce(object? result, TableData fallback)
    {
        if (result is null) return fallback;
        if (result is object?[] arr)
        {
            var rows = new List<IDictionary<string, object?>>();
            foreach (var item in arr)
                if (item is IDictionary<string, object?> d) rows.Add(d);
                else if (item is Dictionary<string, object> d2) rows.Add(d2.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
            if (rows.Count > 0 || arr.Length == 0) return TableData.FromDictionaries(rows);
        }
        throw new InvalidOperationException("JavaScript script must `return` an array of objects (rows).");
    }
}

/// <summary>
/// Runs Python via an external `python` process (must be on PATH).
/// Rows are passed as JSON on stdin; the script must print the transformed rows as JSON to stdout.
/// </summary>
public sealed class PythonScriptRunner : IScriptRunner
{
    public string Language => "python";

    public async Task<TableData> RunAsync(string script, TableData input, CancellationToken ct = default)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"blazorviz_{Guid.NewGuid():N}.py");
        var bootstrap = "import sys, json\nrows = json.load(sys.stdin)\n" + script;
        await File.WriteAllTextAsync(scriptPath, bootstrap, ct);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                ArgumentList = { scriptPath },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start python.");
            await proc.StandardInput.WriteAsync(input.ToJson());
            proc.StandardInput.Close();
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0)
                throw new InvalidOperationException("Python error: " + (stderr.Length > 0 ? stderr : $"exit code {proc.ExitCode}"));

            var start = stdout.IndexOf('[');
            if (start < 0) throw new InvalidOperationException("Python script must print a JSON array of rows (use print(json.dumps(rows))).");
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(stdout[start..]) ?? [];
            return TableData.FromDictionaries(rows.Select(r =>
                (IDictionary<string, object?>)r.ToDictionary(kv => kv.Key, kv => TableData.FromJsonElement(kv.Value))));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("Python is not installed or not on PATH. Install Python 3 to use python scripts.");
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }
    }
}

public sealed class ScriptRunnerFactory(IEnumerable<IScriptRunner> runners)
{
    private readonly Dictionary<string, IScriptRunner> _byLang =
        runners.ToDictionary(r => r.Language, StringComparer.OrdinalIgnoreCase);

    public IScriptRunner Get(string language) =>
        _byLang.TryGetValue(language, out var r) ? r : throw new InvalidOperationException($"Unknown script language '{language}'.");
}
