using AppBender.Core.Common;
using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Web.Api;

/// <summary>Inbound webhook endpoint that triggers Webhook workflows.</summary>
[ApiController]
[Route("api/webhooks")]
public class WebhookController(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    WorkflowRunner runner) : ControllerBase
{
    /// <summary>
    /// Fires the workflow whose trigger config "webhookKey" matches the key.
    /// The request body, query and headers are available as {{trigger.body}}, {{trigger.query}}, {{trigger.headers}}.
    /// </summary>
    [HttpPost("{key}")]
    [HttpGet("{key}")]
    [AllowAnonymous]
    public async Task<IActionResult> Invoke(string key, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var candidates = await db.WorkflowDefinitions.AsNoTracking()
            .Where(w => w.IsEnabled && w.TriggerType == TriggerType.Webhook)
            .ToListAsync(ct);
        var workflow = candidates.FirstOrDefault(w =>
            string.Equals(w.TriggerConfig.GetValueOrDefault("webhookKey"), key, StringComparison.Ordinal));
        if (workflow is null) return NotFound(new { error = "No enabled workflow matches this webhook key." });

        object? body = null;
        if (Request.ContentLength > 0)
        {
            using var reader = new StreamReader(Request.Body);
            var raw = await reader.ReadToEndAsync(ct);
            try { body = JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(raw).RootElement); }
            catch { body = raw; }
        }

        var trigger = new Dictionary<string, object?>
        {
            ["type"] = "webhook",
            ["method"] = Request.Method,
            ["body"] = body,
            ["query"] = Request.Query.ToDictionary(q => q.Key, q => (object?)q.Value.ToString()),
            ["headers"] = Request.Headers
                .Where(h => !h.Key.StartsWith(':'))
                .ToDictionary(h => h.Key, h => (object?)h.Value.ToString())
        };

        var run = await runner.RunAsync(workflow, trigger, "webhook", ct);
        if (run.Status == RunStatus.Failed)
            return StatusCode(500, new { error = run.Error, runId = run.Id });

        // If the workflow used "Respond to Webhook", return that payload; otherwise a summary.
        var output = JsonUtil.Deserialize<object>(run.OutputJson ?? "null");
        return Ok(output ?? new { ok = true, runId = run.Id });
    }
}
