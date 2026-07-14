using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.SemanticKernel;

namespace BlazorViz.Services.Ai;

/// <summary>
/// Plugin system: loads custom kernel functions from the "plugins" folder at startup.
/// Each .csx file must return (or end with) an object instance; its [KernelFunction]
/// methods become Data Wizard tools. See docs/plugins.md and plugins/sample-formatter.csx.
/// </summary>
public sealed class PluginService(IWebHostEnvironment env, ILogger<PluginService> log)
{
    private readonly List<(string Name, object Instance)> _plugins = [];
    public IReadOnlyList<(string Name, object Instance)> Loaded => _plugins;
    public List<string> Errors { get; } = [];

    public async Task LoadAsync()
    {
        _plugins.Clear();
        Errors.Clear();
        var dir = Path.Combine(env.ContentRootPath, "plugins");
        if (!Directory.Exists(dir)) return;

        var options = ScriptOptions.Default
            .AddReferences(typeof(KernelFunctionAttribute).Assembly, typeof(Enumerable).Assembly)
            .AddImports("System", "System.Linq", "System.Collections.Generic", "System.ComponentModel", "Microsoft.SemanticKernel");

        foreach (var file in Directory.GetFiles(dir, "*.csx"))
        {
            var name = Path.GetFileNameWithoutExtension(file).Replace("-", "_");
            try
            {
                var code = await File.ReadAllTextAsync(file);
                var result = await CSharpScript.EvaluateAsync<object?>(code, options);
                if (result is null)
                {
                    Errors.Add($"{Path.GetFileName(file)}: script must end with an expression returning a plugin object.");
                    continue;
                }
                if (!result.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Any(m => m.GetCustomAttribute<KernelFunctionAttribute>() is not null))
                {
                    Errors.Add($"{Path.GetFileName(file)}: returned object has no [KernelFunction] methods.");
                    continue;
                }
                _plugins.Add((name, result));
                log.LogInformation("Loaded plugin {Plugin} from {File}", name, file);
            }
            catch (Exception ex)
            {
                Errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                log.LogWarning(ex, "Plugin load failed: {File}", file);
            }
        }
    }

    public void RegisterInto(Kernel kernel)
    {
        foreach (var (name, instance) in _plugins)
        {
            try { kernel.Plugins.AddFromObject(instance, name); }
            catch (Exception ex) { Errors.Add($"{name}: {ex.Message}"); }
        }
    }
}
