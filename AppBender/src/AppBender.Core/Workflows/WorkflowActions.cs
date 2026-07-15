using System.Text;
using AppBender.Core.AI;
using AppBender.Core.Common;
using AppBender.Core.Connectors;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AppBender.Core.Workflows;

// ------------------------------------------------------------------ variables & basics

public class LogAction : IWorkflowAction
{
    public string Type => "log";
    public Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => Task.FromResult<object?>(ctx.Render(step.Cfg("message")));
}

public class SetVariableAction : IWorkflowAction
{
    public string Type => "set_variable";
    public Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var name = ctx.Render(step.Cfg("name"));
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Variable name is required.");
        var value = ctx.Eval(step.Cfg("value"));
        ctx.Vars[name] = value;
        return Task.FromResult(value);
    }
}

public class ComposeAction : IWorkflowAction
{
    public string Type => "compose";
    public Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => Task.FromResult(ctx.RenderJson(step.Cfg("value")));
}

public class MathAction : IWorkflowAction
{
    public string Type => "math";
    public Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => Task.FromResult<object?>(MathEvaluator.Evaluate(ctx.Render(step.Cfg("expression"))));
}

public class TransformAction : IWorkflowAction
{
    public string Type => "transform";
    public Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var mapping = JsonUtil.DeserializeOrNew<Dictionary<string, string>>(step.Cfg("mapping"));
        var result = new Dictionary<string, object?>();
        foreach (var (key, template) in mapping)
            result[key] = ctx.Eval(template);
        return Task.FromResult<object?>(result);
    }
}

// ------------------------------------------------------------------ data hub

public class DataQueryAction(IDataHubService dataHub) : IWorkflowAction
{
    public string Type => "data_query";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var options = new QueryOptions
        {
            Search = NullIfEmpty(ctx.Render(step.Cfg("search"))),
            SortBy = NullIfEmpty(ctx.Render(step.Cfg("sortBy"))),
            SortDesc = step.Cfg("desc") == "true",
            PageSize = int.TryParse(step.Cfg("top", "50"), out var top) ? top : 50
        };
        var filter = ctx.Render(step.Cfg("filter"));
        foreach (var clause in filter.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = clause.Split(' ', 3);
            if (parts.Length >= 2)
                options.Filters.Add(new FieldFilter { Field = parts[0], Op = parts[1], Value = parts.Length > 2 ? parts[2] : null });
        }
        var result = await dataHub.QueryAsync(ctx.Render(step.Cfg("entity")), options);
        return result.Records.Select(ToDict).ToList();
    }

    internal static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    internal static Dictionary<string, object?> ToDict(DataRecord r)
    {
        var d = r.Data;
        d["id"] = r.Id;
        d["createdAt"] = r.CreatedAt.ToString("O");
        d["updatedAt"] = r.UpdatedAt.ToString("O");
        return d;
    }
}

public class DataGetAction(IDataHubService dataHub) : IWorkflowAction
{
    public string Type => "data_get";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var record = await dataHub.GetRecordAsync(ctx.Render(step.Cfg("entity")), ctx.Render(step.Cfg("id")));
        return record is null ? null : DataQueryAction.ToDict(record);
    }
}

public class DataCreateAction(IDataHubService dataHub) : IWorkflowAction
{
    public string Type => "data_create";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var record = await dataHub.CreateRecordAsync(ctx.Render(step.Cfg("entity")), ctx.RenderJsonObject(step.Cfg("data")));
        return DataQueryAction.ToDict(record);
    }
}

public class DataUpdateAction(IDataHubService dataHub) : IWorkflowAction
{
    public string Type => "data_update";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var record = await dataHub.UpdateRecordAsync(
            ctx.Render(step.Cfg("entity")), ctx.Render(step.Cfg("id")), ctx.RenderJsonObject(step.Cfg("data")));
        return DataQueryAction.ToDict(record);
    }
}

public class DataDeleteAction(IDataHubService dataHub) : IWorkflowAction
{
    public string Type => "data_delete";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        await dataHub.DeleteRecordAsync(ctx.Render(step.Cfg("entity")), ctx.Render(step.Cfg("id")));
        return true;
    }
}

// ------------------------------------------------------------------ integration

public class HttpRequestAction(IHttpClientFactory httpFactory) : IWorkflowAction
{
    public string Type => "http_request";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var client = httpFactory.CreateClient("workflow");
        var method = new HttpMethod(step.Cfg("method", "GET"));
        using var request = new HttpRequestMessage(method, ctx.Render(step.Cfg("url")));

        var headers = JsonUtil.DeserializeOrNew<Dictionary<string, string>>(ctx.Render(step.Cfg("headers")));
        string? contentType = null;
        foreach (var (k, v) in headers)
        {
            if (k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) contentType = v;
            else request.Headers.TryAddWithoutValidation(k, v);
        }

        var body = ctx.Render(step.Cfg("body"));
        if (!string.IsNullOrEmpty(body) && method != HttpMethod.Get)
            request.Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json");

        using var response = await client.SendAsync(request, ctx.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(ctx.CancellationToken);
        object? parsed = text;
        try { parsed = JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(text).RootElement); } catch { }

        return new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["isSuccess"] = response.IsSuccessStatusCode,
            ["body"] = parsed
        };
    }
}

public class ConnectorActionAction(IConnectorRuntime connectors) : IWorkflowAction
{
    public string Type => "connector_action";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await connectors.ExecuteAsync(
            ctx.Render(step.Cfg("connectorId")),
            ctx.Render(step.Cfg("action")),
            ctx.RenderJsonObject(step.Cfg("input")),
            ctx.CancellationToken);
}

public class SendEmailAction(IEmailService email) : IWorkflowAction
{
    public string Type => "send_email";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        await email.SendAsync(
            ctx.Render(step.Cfg("to")),
            ctx.Render(step.Cfg("subject")),
            ctx.Render(step.Cfg("body")),
            DataQueryAction.NullIfEmpty(ctx.Render(step.Cfg("cc")))?.ToString());
        return "sent";
    }
}

public class RespondAction : IWorkflowAction
{
    public string Type => "respond";
    public Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        ctx.Response = ctx.RenderJson(step.Cfg("body"));
        return Task.FromResult(ctx.Response);
    }
}

public class CallWorkflowAction(IWorkflowService workflows, WorkflowRunner runner) : IWorkflowAction
{
    public string Type => "call_workflow";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var id = ctx.Render(step.Cfg("workflowId"));
        var target = await workflows.GetAsync(id)
            ?? throw new InvalidOperationException($"Workflow '{id}' not found.");
        if (target.Id == ctx.Workflow.Id)
            throw new InvalidOperationException("A workflow cannot call itself.");
        var input = new Dictionary<string, object?> { ["body"] = ctx.RenderJson(step.Cfg("input")) };
        var run = await runner.RunAsync(target, input, $"workflow:{ctx.Workflow.Name}", ctx.CancellationToken);
        if (run.Status == RunStatus.Failed)
            throw new InvalidOperationException($"Called workflow failed: {run.Error}");
        return JsonUtil.Deserialize<object>(run.OutputJson ?? "null");
    }
}

// ------------------------------------------------------------------ code

public class RunCSharpAction(IScriptingService scripting) : IWorkflowAction
{
    public string Type => "run_csharp";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await scripting.RunCSharpAsync(step.Cfg("code"), ctx.Root, ctx.CancellationToken);
}

public class RunJavaScriptAction(IScriptingService scripting) : IWorkflowAction
{
    public string Type => "run_javascript";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await scripting.RunJavaScriptAsync(step.Cfg("code"), ctx.Root, ctx.CancellationToken);
}

public class RunPythonAction(IScriptingService scripting) : IWorkflowAction
{
    public string Type => "run_python";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await scripting.RunPythonAsync(step.Cfg("code"), ctx.Root, ctx.CancellationToken);
}

// ------------------------------------------------------------------ files

public class FileWriteAction(IStorageService storage) : IWorkflowAction
{
    public string Type => "file_write";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var path = ctx.Render(step.Cfg("path"));
        await storage.WriteTextAsync(path, ctx.Render(step.Cfg("content")));
        return path;
    }
}

public class FileReadAction(IStorageService storage) : IWorkflowAction
{
    public string Type => "file_read";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await storage.ReadTextAsync(ctx.Render(step.Cfg("path")));
}

// ------------------------------------------------------------------ AI

public abstract class LlmActionBase(ILlmClient llm) : IWorkflowAction
{
    protected ILlmClient Llm { get; } = llm;
    public abstract string Type { get; }
    public abstract Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx);

    protected async Task<string> AskAsync(WorkflowContext ctx, string prompt, string? system = null,
        string? provider = null, string? model = null, bool json = false)
    {
        var messages = new List<LlmMessage>();
        if (!string.IsNullOrWhiteSpace(system)) messages.Add(new LlmMessage("system", system));
        messages.Add(new LlmMessage("user", prompt));
        var result = await Llm.CompleteAsync(messages, new LlmRequestOptions
        {
            Provider = string.IsNullOrWhiteSpace(provider) ? null : provider,
            Model = string.IsNullOrWhiteSpace(model) ? null : model,
            JsonMode = json
        }, ctx.CancellationToken);
        return result.Text;
    }
}

public class AiChatAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_chat";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await AskAsync(ctx, ctx.Render(step.Cfg("prompt")), ctx.Render(step.Cfg("system")),
            step.Cfg("provider"), step.Cfg("model"));
}

public class AiSummarizeAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_summarize";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var length = step.Cfg("length", "short");
        return await AskAsync(ctx,
            $"Summarize the following text. Length: {length}.\n\n{ctx.Render(step.Cfg("text"))}",
            "You are a precise summarizer. Reply with only the summary.");
    }
}

public class AiExtractAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_extract";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var text = await AskAsync(ctx,
            $"Extract these fields as a JSON object: {ctx.Render(step.Cfg("schema"))}\n\nText:\n{ctx.Render(step.Cfg("text"))}",
            "Reply with only a valid JSON object, no prose.", json: true);
        try { return JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(StripFences(text)).RootElement); }
        catch { return text; }
    }

    internal static string StripFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) text = text[..lastFence];
        }
        return text.Trim();
    }
}

public class AiClassifyAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_classify";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await AskAsync(ctx,
            $"Classify the following text into exactly one of these categories: {ctx.Render(step.Cfg("categories"))}.\n" +
            $"Reply with only the category name.\n\nText:\n{ctx.Render(step.Cfg("text"))}");
}

public class AiGenerateCodeAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_generate_code";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var language = ctx.Render(step.Cfg("language", "csharp"));
        var text = await AskAsync(ctx,
            $"Write {language} code for: {ctx.Render(step.Cfg("description"))}\nReply with only the code.",
            "You are an expert programmer. Output only code, no explanations.");
        return AiExtractAction.StripFences(text);
    }
}

public class AiVisionAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_vision";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var result = await Llm.CompleteAsync(
            [new LlmMessage("user", ctx.Render(step.Cfg("prompt", "Describe this image in detail.")),
                [ctx.Render(step.Cfg("imageUrl"))])],
            null, ctx.CancellationToken);
        return result.Text;
    }
}

public class AiOcrAction(ILlmClient llm) : LlmActionBase(llm)
{
    public override string Type => "ai_ocr";
    public override async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var result = await Llm.CompleteAsync(
            [new LlmMessage("user",
                "Extract ALL text from this image exactly as written. Preserve layout with line breaks. Reply with only the extracted text.",
                [ctx.Render(step.Cfg("imageUrl"))])],
            null, ctx.CancellationToken);
        return result.Text;
    }
}

public class AiGenerateImageAction(ILlmClient llm) : IWorkflowAction
{
    public string Type => "ai_generate_image";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await llm.GenerateImageAsync(ctx.Render(step.Cfg("prompt")), step.Cfg("size", "1024x1024"), ctx.CancellationToken);
}

public class AiTranscribeAction(ILlmClient llm) : IWorkflowAction
{
    public string Type => "ai_transcribe";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await llm.TranscribeAudioAsync(ctx.Render(step.Cfg("audioUrl")), ctx.CancellationToken);
}

public class AiSearchInternetAction(IWebSearchClient search) : IWorkflowAction
{
    public string Type => "ai_search_internet";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var max = int.TryParse(step.Cfg("maxResults", "5"), out var m) ? m : 5;
        var results = await search.SearchAsync(ctx.Render(step.Cfg("query")), max, ctx.CancellationToken);
        return results.Select(r => new Dictionary<string, object?>
        {
            ["title"] = r.Title, ["url"] = r.Url, ["snippet"] = r.Snippet
        }).ToList();
    }
}

public class AiScrapeAction(IWebSearchClient search) : IWorkflowAction
{
    public string Type => "ai_scrape";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
        => await search.ScrapeAsync(ctx.Render(step.Cfg("url")), ctx.CancellationToken);
}

public class AiRagQueryAction(IRagService rag) : IWorkflowAction
{
    public string Type => "ai_rag_query";
    public async Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext ctx)
    {
        var answer = await rag.AskAsync(ctx.Render(step.Cfg("question")), ctx.CancellationToken);
        return new Dictionary<string, object?> { ["answer"] = answer.Answer, ["sources"] = answer.Sources };
    }
}

// ------------------------------------------------------------------ registration

public static class WorkflowActionRegistration
{
    public static IServiceCollection AddWorkflowActions(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowAction, LogAction>();
        services.AddScoped<IWorkflowAction, SetVariableAction>();
        services.AddScoped<IWorkflowAction, ComposeAction>();
        services.AddScoped<IWorkflowAction, MathAction>();
        services.AddScoped<IWorkflowAction, TransformAction>();
        services.AddScoped<IWorkflowAction, DataQueryAction>();
        services.AddScoped<IWorkflowAction, DataGetAction>();
        services.AddScoped<IWorkflowAction, DataCreateAction>();
        services.AddScoped<IWorkflowAction, DataUpdateAction>();
        services.AddScoped<IWorkflowAction, DataDeleteAction>();
        services.AddScoped<IWorkflowAction, HttpRequestAction>();
        services.AddScoped<IWorkflowAction, ConnectorActionAction>();
        services.AddScoped<IWorkflowAction, SendEmailAction>();
        services.AddScoped<IWorkflowAction, RespondAction>();
        services.AddScoped<IWorkflowAction, CallWorkflowAction>();
        services.AddScoped<IWorkflowAction, RunCSharpAction>();
        services.AddScoped<IWorkflowAction, RunJavaScriptAction>();
        services.AddScoped<IWorkflowAction, RunPythonAction>();
        services.AddScoped<IWorkflowAction, FileWriteAction>();
        services.AddScoped<IWorkflowAction, FileReadAction>();
        services.AddScoped<IWorkflowAction, AiChatAction>();
        services.AddScoped<IWorkflowAction, AiSummarizeAction>();
        services.AddScoped<IWorkflowAction, AiExtractAction>();
        services.AddScoped<IWorkflowAction, AiClassifyAction>();
        services.AddScoped<IWorkflowAction, AiGenerateCodeAction>();
        services.AddScoped<IWorkflowAction, AiVisionAction>();
        services.AddScoped<IWorkflowAction, AiOcrAction>();
        services.AddScoped<IWorkflowAction, AiGenerateImageAction>();
        services.AddScoped<IWorkflowAction, AiTranscribeAction>();
        services.AddScoped<IWorkflowAction, AiSearchInternetAction>();
        services.AddScoped<IWorkflowAction, AiScrapeAction>();
        services.AddScoped<IWorkflowAction, AiRagQueryAction>();
        return services;
    }
}
