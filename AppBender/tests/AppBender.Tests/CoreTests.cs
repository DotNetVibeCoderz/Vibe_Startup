using AppBender.Core.AI;
using AppBender.Core.Common;
using AppBender.Core.Models;

namespace AppBender.Tests;

public class TemplateEngineTests
{
    private static Dictionary<string, object?> Context() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["trigger"] = new Dictionary<string, object?>
        {
            ["body"] = new Dictionary<string, object?>
            {
                ["name"] = "Budi",
                ["items"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["sku"] = "A-1", ["qty"] = 3L }
                }
            }
        },
        ["vars"] = new Dictionary<string, object?> { ["total"] = 42.5 }
    };

    [Fact]
    public void Renders_nested_paths()
        => Assert.Equal("Halo Budi!", TemplateEngine.Render("Halo {{trigger.body.name}}!", Context()));

    [Fact]
    public void Renders_array_indexing()
        => Assert.Equal("A-1 x3", TemplateEngine.Render("{{trigger.body.items[0].sku}} x{{trigger.body.items[0].qty}}", Context()));

    [Fact]
    public void EvaluateValue_returns_raw_value_for_single_placeholder()
        => Assert.Equal(42.5, TemplateEngine.EvaluateValue("{{vars.total}}", Context()));

    [Fact]
    public void Unknown_path_renders_empty()
        => Assert.Equal("x", TemplateEngine.Render("x{{does.not.exist}}", Context()));

    [Fact]
    public void Builtin_guid_renders_32_chars()
        => Assert.Equal(32, TemplateEngine.Render("{{guid}}", Context()).Length);
}

public class MathEvaluatorTests
{
    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("2 ^ 10", 1024)]
    [InlineData("-5 + 3", -2)]
    [InlineData("round(19.117, 2)", 19.12)]
    [InlineData("max(1, 7, 3)", 7)]
    [InlineData("sqrt(144)", 12)]
    [InlineData("abs(-9) % 4", 1)]
    [InlineData("gte(100, 100)", 1)]
    [InlineData("gt(99, 100)", 0)]
    [InlineData("if(gte(120000000, 114750000), 120000000 * 0.025, 0)", 3000000)]
    [InlineData("if(lt(50, 100), 7, 9)", 7)]
    public void Evaluates_expressions(string expression, double expected)
        => Assert.Equal(expected, MathEvaluator.Evaluate(expression), 6);

    [Fact]
    public void Throws_on_garbage()
        => Assert.ThrowsAny<Exception>(() => MathEvaluator.Evaluate("2 +* 3"));
}

public class FormulaAndSlugTests
{
    [Fact]
    public void Formula_field_computes_from_record_values()
    {
        var data = new Dictionary<string, object?> { ["qty"] = 4, ["unit_price"] = 25000 };
        var result = Core.Services.DataHubService.EvaluateFormula("qty * unit_price", data);
        Assert.Equal(100000d, Convert.ToDouble(result));
    }

    [Theory]
    [InlineData("Hello World", "hello_world")]
    [InlineData("  Café-Menu 2024! ", "café_menu_2024")]
    [InlineData("already_ok", "already_ok")]
    public void Slugify_normalizes(string input, string expected)
        => Assert.Equal(expected, Core.Services.DataHubService.Slugify(input));
}

public class DocumentChunkTests
{
    [Fact]
    public void Short_text_is_single_chunk()
    {
        var chunks = DocumentTextExtractor.Chunk("hello world");
        Assert.Single(chunks);
    }

    [Fact]
    public void Long_text_produces_overlapping_chunks()
    {
        var text = string.Join("\n\n", Enumerable.Range(0, 60).Select(i => $"Paragraph {i} " + new string('x', 80)));
        var chunks = DocumentTextExtractor.Chunk(text, chunkSize: 500, overlap: 50);
        Assert.True(chunks.Count > 3);
        Assert.All(chunks, c => Assert.True(c.Length <= 500));
    }

    [Fact]
    public void Cosine_similarity_is_1_for_identical_vectors()
    {
        float[] v = [0.5f, 0.2f, 0.8f];
        Assert.Equal(1, RagService.CosineSimilarity(v, v), 5);
    }
}

public class ModelDefaultsTests
{
    [Fact]
    public void Workflow_step_roundtrips_through_json()
    {
        var step = new WorkflowStep
        {
            Type = "condition",
            Name = "Check",
            Config = new() { ["left"] = "{{vars.x}}", ["op"] = "gt", ["right"] = "5" },
            TrueSteps = [new WorkflowStep { Type = "log", Config = new() { ["message"] = "big" } }]
        };
        var json = JsonUtil.Serialize(step);
        var back = JsonUtil.Deserialize<WorkflowStep>(json);
        Assert.NotNull(back);
        Assert.Equal("condition", back!.Type);
        Assert.Single(back.TrueSteps);
        Assert.Equal("gt", back.Cfg("op"));
    }

    [Fact]
    public void Entity_fields_roundtrip()
    {
        var entity = new EntityDefinition
        {
            Name = "things",
            Fields = [new FieldDefinition { Name = "price", Type = FieldType.Currency, Required = true }]
        };
        Assert.Single(entity.Fields);
        Assert.Equal(FieldType.Currency, entity.Fields[0].Type);
        Assert.True(entity.Fields[0].Required);
    }
}
