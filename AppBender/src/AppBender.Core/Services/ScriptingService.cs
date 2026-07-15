using System.Diagnostics;
using AppBender.Core.Common;
using Jint;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace AppBender.Core.Services;

public interface IScriptingService
{
    /// <summary>Runs C# script. The context dictionary is available as "Context"; return a value with "return".</summary>
    Task<object?> RunCSharpAsync(string code, IDictionary<string, object?> context, CancellationToken ct = default);
    /// <summary>Runs JavaScript with Jint. The context is available as "context"; the last expression is the result.</summary>
    Task<object?> RunJavaScriptAsync(string code, IDictionary<string, object?> context, CancellationToken ct = default);
    /// <summary>Runs Python via the local "python" executable when installed. Context passed as JSON on stdin.</summary>
    Task<object?> RunPythonAsync(string code, IDictionary<string, object?> context, CancellationToken ct = default);
}

public class CSharpScriptGlobals
{
    public IDictionary<string, object?> Context { get; set; } = new Dictionary<string, object?>();
}

public class ScriptingService : IScriptingService
{
    private static readonly ScriptOptions CSharpOptions = ScriptOptions.Default
        .AddReferences(typeof(object).Assembly, typeof(Enumerable).Assembly, typeof(System.Text.Json.JsonSerializer).Assembly)
        .AddImports("System", "System.Linq", "System.Collections.Generic", "System.Text", "System.Text.Json", "System.Math");

    public async Task<object?> RunCSharpAsync(string code, IDictionary<string, object?> context, CancellationToken ct = default)
    {
        var globals = new CSharpScriptGlobals { Context = context };
        var state = await CSharpScript.RunAsync(code, CSharpOptions, globals, typeof(CSharpScriptGlobals), ct);
        return state.ReturnValue;
    }

    public Task<object?> RunJavaScriptAsync(string code, IDictionary<string, object?> context, CancellationToken ct = default)
    {
        var engine = new Jint.Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(30));
            options.LimitRecursion(256);
            options.MaxStatements(500_000);
            options.CancellationToken(ct);
        });
        engine.SetValue("context", context);
        var result = engine.Evaluate(code);
        return Task.FromResult(FromJsValue(result));
    }

    private static object? FromJsValue(Jint.Native.JsValue value)
    {
        var clr = value.ToObject();
        // normalize ExpandoObject/arrays into plain dictionaries/lists via JSON round-trip
        if (clr is null || clr is string || clr.GetType().IsPrimitive || clr is double or bool) return clr;
        try
        {
            var json = JsonUtil.Serialize(clr);
            return JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(json).RootElement);
        }
        catch { return clr; }
    }

    public async Task<object?> RunPythonAsync(string code, IDictionary<string, object?> context, CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"appbender_{Guid.NewGuid():N}.py");
        // Wrapper: parse context from stdin, run user code, print result as JSON.
        var wrapper = """
            import sys, json
            context = json.loads(sys.stdin.read() or "{}")
            result = None
            """ + "\n" + code + "\n" + """
            print(json.dumps(result, default=str))
            """;
        await File.WriteAllTextAsync(tempFile, wrapper, ct);
        try
        {
            var psi = new ProcessStartInfo("python", $"\"{tempFile}\"")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start python.");
            await process.StandardInput.WriteAsync(JsonUtil.Serialize(context));
            process.StandardInput.Close();
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Python exited with {process.ExitCode}: {stderr}");
            var lastLine = stdout.Trim().Split('\n').LastOrDefault() ?? "null";
            try { return JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(lastLine).RootElement); }
            catch { return stdout.Trim(); }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("Python is not installed or not on PATH.");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
}
