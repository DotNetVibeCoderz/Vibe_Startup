using BlazorViz.Data;
using BlazorViz.Models;
using BlazorViz.Services;
using BlazorViz.Services.Ai;
using Microsoft.EntityFrameworkCore;

namespace BlazorViz.Api;

/// <summary>
/// Minimal APIs for external integration (documented via Swagger at /swagger).
/// All endpoints require the X-Api-Key header matching an enabled ApiKey row.
/// </summary>
public static class ApiEndpoints
{
    public static void MapBlazorVizApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1")
            .WithTags("BlazorViz API")
            .AddEndpointFilter(async (ctx, next) =>
            {
                var http = ctx.HttpContext;
                var key = http.Request.Headers["X-Api-Key"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(key))
                    return Results.Unauthorized();
                await using var db = await http.RequestServices
                    .GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
                if (!await db.ApiKeys.AnyAsync(k => k.Key == key && k.Enabled))
                    return Results.Unauthorized();
                http.RequestServices.GetRequiredService<UsageService>()
                    .Record("api", 1, meta: http.Request.Path);
                return await next(ctx);
            });

        api.MapGet("/datasets", async (IDbContextFactory<ApplicationDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            return Results.Ok(await db.Datasets
                .Select(d => new { d.Id, d.Name, d.Description, d.SourceKind, d.RefreshIntervalSeconds, d.LastRefreshedUtc })
                .ToListAsync());
        }).WithSummary("List datasets");

        api.MapGet("/datasets/{id:int}/data", async (int id, int? limit, DatasetService datasets) =>
        {
            var data = await datasets.GetDataAsync(id);
            if (limit is int n and > 0) data = data.Head(n);
            return Results.Json(new { columns = data.Columns, rows = data.ToDictionaries() });
        }).WithSummary("Get dataset rows (optionally limited)");

        api.MapGet("/datasets/{id:int}/schema", async (int id, DatasetService datasets) =>
        {
            var data = await datasets.GetDataAsync(id);
            return Results.Json(new { columns = data.Columns, rowCount = data.RowCount });
        }).WithSummary("Get dataset schema");

        api.MapPost("/datasets/{id:int}/query", async (int id, QueryRequest req, DatasetService datasets) =>
        {
            var data = await datasets.GetDataAsync(id);
            if (!string.IsNullOrWhiteSpace(req.GroupBy) && !string.IsNullOrWhiteSpace(req.Aggs))
                data = EtlService.Aggregate(data, req.GroupBy, req.Aggs);
            if (!string.IsNullOrWhiteSpace(req.FilterField))
                data = EtlService.FilterBy(data, req.FilterField!, req.FilterOp ?? "=", req.FilterValue ?? "");
            if (!string.IsNullOrWhiteSpace(req.SortBy))
                data = EtlService.Sort(data, req.SortBy!, req.SortDesc);
            if (req.Limit is int lim and > 0) data = data.Head(lim);
            return Results.Json(new { columns = data.Columns, rows = data.ToDictionaries() });
        }).WithSummary("Query a dataset (filter / aggregate / sort)");

        api.MapGet("/datasets/{id:int}/export", async (int id, string format, DatasetService datasets, ExportService export,
            IDbContextFactory<ApplicationDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            var ds = await db.Datasets.FindAsync(id);
            if (ds is null) return Results.NotFound();
            var data = await datasets.GetDataAsync(id);
            var (bytes, contentType, ext) = export.Export(data, format, ds.Name);
            return Results.File(bytes, contentType, $"{ds.Name}.{ext}");
        }).WithSummary("Export a dataset (format: csv | json | excel | pdf)");

        api.MapGet("/dashboards", async (IDbContextFactory<ApplicationDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            var dashboards = await db.Dashboards.ToListAsync();
            return Results.Ok(dashboards.Select(d => new
            {
                d.Id, d.Name, d.Description, d.IsPublic, d.CurrentVersion, d.UpdatedUtc,
                shareUrl = d.IsPublic && d.ShareToken != null ? $"/share/{d.ShareToken}" : null
            }));
        }).WithSummary("List dashboards");

        api.MapGet("/dashboards/{id:int}", async (int id, IDbContextFactory<ApplicationDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            var d = await db.Dashboards.FindAsync(id);
            return d is null ? Results.NotFound() : Results.Json(new
            {
                d.Id, d.Name, d.Description, d.CurrentVersion,
                layout = DashboardLayout.Parse(d.LayoutJson)
            });
        }).WithSummary("Get a dashboard definition");

        api.MapPost("/chat", async (ChatRequest req, ChatService chat) =>
        {
            var session = await chat.GetOrCreateSessionAsync(req.SessionId, "api");
            var reply = new System.Text.StringBuilder();
            await foreach (var chunk in chat.SendAsync(session.Id, req.Message, [], "api"))
                reply.Append(chunk);
            return Results.Json(new { sessionId = session.Id, reply = reply.ToString() });
        }).WithSummary("Chat with the Data Wizard assistant");
    }

    public sealed record QueryRequest(
        string? GroupBy, string? Aggs,
        string? FilterField, string? FilterOp, string? FilterValue,
        string? SortBy, bool SortDesc = false, int? Limit = null);

    public sealed record ChatRequest(string Message, int? SessionId = null);
}
