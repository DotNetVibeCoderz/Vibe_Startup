using AppBender.Core.Common;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppBender.Web.Api;

/// <summary>Auto-generated REST API over every Data Hub entity.</summary>
[ApiController]
[Route("api/data")]
[Authorize]
[Produces("application/json")]
public class DataApiController(IDataHubService dataHub, IUsageService usage) : ControllerBase
{
    /// <summary>Lists all entity definitions (schema) for the current tenant.</summary>
    [HttpGet("entities")]
    public async Task<ActionResult<IEnumerable<object>>> GetEntities()
    {
        var entities = await dataHub.GetEntitiesAsync();
        return Ok(entities.Select(e => new
        {
            e.Id, e.Name, e.DisplayName, e.Description, e.Icon, e.Version,
            fields = e.Fields, relationships = e.Relationships
        }));
    }

    /// <summary>Queries records of an entity with filtering, search, sorting and paging.</summary>
    /// <param name="entity">Entity logical name, e.g. "customers".</param>
    /// <param name="filter">Clauses "field op value" joined by " and ", e.g. "status eq Active and total gt 100".</param>
    [HttpGet("{entity}")]
    public async Task<ActionResult<object>> Query(
        string entity,
        [FromQuery] string? search,
        [FromQuery] string? filter,
        [FromQuery] string? sortBy,
        [FromQuery] bool desc = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        usage.TrackFireAndForget("api_query", 1, $"GET {entity}");
        var options = new QueryOptions
        {
            Search = search, SortBy = sortBy, SortDesc = desc, Page = page, PageSize = pageSize
        };
        if (!string.IsNullOrEmpty(filter))
        {
            foreach (var clause in filter.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = clause.Split(' ', 3);
                if (parts.Length >= 2)
                    options.Filters.Add(new FieldFilter { Field = parts[0], Op = parts[1], Value = parts.Length > 2 ? parts[2] : null });
            }
        }

        try
        {
            var result = await dataHub.QueryAsync(entity, options);
            return Ok(new
            {
                result.TotalCount, result.Page, result.PageSize,
                records = result.Records.Select(ToDto)
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Gets a single record by id.</summary>
    [HttpGet("{entity}/{id}")]
    public async Task<ActionResult<object>> GetById(string entity, string id)
    {
        usage.TrackFireAndForget("api_query", 1, $"GET {entity}/{id}");
        var record = await dataHub.GetRecordAsync(entity, id);
        return record is null ? NotFound() : Ok(ToDto(record));
    }

    /// <summary>Creates a record. Body is a JSON object of field values.</summary>
    [HttpPost("{entity}")]
    public async Task<ActionResult<object>> Create(string entity, [FromBody] Dictionary<string, System.Text.Json.JsonElement> body)
    {
        usage.TrackFireAndForget("api_query", 1, $"POST {entity}");
        try
        {
            var data = body.ToDictionary(kv => kv.Key, kv => JsonUtil.ToClr(kv.Value));
            var record = await dataHub.CreateRecordAsync(entity, data);
            return CreatedAtAction(nameof(GetById), new { entity, id = record.Id }, ToDto(record));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Updates a record (partial update: only provided fields change).</summary>
    [HttpPut("{entity}/{id}")]
    public async Task<ActionResult<object>> Update(string entity, string id, [FromBody] Dictionary<string, System.Text.Json.JsonElement> body)
    {
        usage.TrackFireAndForget("api_query", 1, $"PUT {entity}/{id}");
        try
        {
            var data = body.ToDictionary(kv => kv.Key, kv => JsonUtil.ToClr(kv.Value));
            var record = await dataHub.UpdateRecordAsync(entity, id, data);
            return Ok(ToDto(record));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>Deletes a record.</summary>
    [HttpDelete("{entity}/{id}")]
    public async Task<IActionResult> Delete(string entity, string id)
    {
        usage.TrackFireAndForget("api_query", 1, $"DELETE {entity}/{id}");
        try
        {
            await dataHub.DeleteRecordAsync(entity, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    private static object ToDto(DataRecord r)
    {
        var dto = new Dictionary<string, object?> { ["id"] = r.Id };
        foreach (var (k, v) in r.Data) dto[k] = v;
        dto["createdAt"] = r.CreatedAt;
        dto["updatedAt"] = r.UpdatedAt;
        dto["version"] = r.Version;
        return dto;
    }
}

/// <summary>GraphQL-style endpoint over Data Hub entities.</summary>
[ApiController]
[Route("api/graphql")]
[Authorize]
public class GraphQlController(GraphQlExecutor executor, IUsageService usage) : ControllerBase
{
    public record GraphQlRequest(string Query);

    /// <summary>Executes a GraphQL query or mutation. See docs/api.md for the supported grammar.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Execute([FromBody] GraphQlRequest request)
    {
        usage.TrackFireAndForget("api_query", 1, "graphql");
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { errors = new[] { new { message = "Query is required." } } });
        return Ok(await executor.ExecuteAsync(request.Query));
    }
}
