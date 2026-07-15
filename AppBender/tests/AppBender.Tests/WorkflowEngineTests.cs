using AppBender.Core.Models;
using AppBender.Core.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppBender.Tests;

public class WorkflowEngineTests
{
    private static WorkflowEngine Engine() => new(
        [new LogAction(), new SetVariableAction(), new ComposeAction(), new MathAction(), new TransformAction()],
        NullLogger<WorkflowEngine>.Instance);

    private static WorkflowDefinition Workflow(params WorkflowStep[] steps) => new()
    {
        Name = "test",
        Steps = steps.ToList()
    };

    [Fact]
    public async Task Set_variable_and_math_flow()
    {
        var workflow = Workflow(
            new WorkflowStep { Type = "set_variable", Config = new() { ["name"] = "qty", ["value"] = "4" } },
            new WorkflowStep { Type = "math", Name = "calc", Config = new() { ["expression"] = "{{vars.qty}} * 25000" } },
            new WorkflowStep { Type = "set_variable", Config = new() { ["name"] = "total", ["value"] = "{{steps.calc.output}}" } });

        var ctx = await Engine().ExecuteAsync(workflow, null);

        Assert.Equal(100000d, Convert.ToDouble(ctx.Vars["total"]));
        Assert.Equal(3, ctx.Logs.Count);
        Assert.All(ctx.Logs, l => Assert.Equal("succeeded", l.Status));
    }

    [Fact]
    public async Task Condition_branches_correctly()
    {
        var workflow = Workflow(new WorkflowStep
        {
            Type = "condition",
            Config = new() { ["left"] = "{{trigger.amount}}", ["op"] = "gt", ["right"] = "100" },
            TrueSteps = [new WorkflowStep { Type = "set_variable", Config = new() { ["name"] = "branch", ["value"] = "big" } }],
            FalseSteps = [new WorkflowStep { Type = "set_variable", Config = new() { ["name"] = "branch", ["value"] = "small" } }]
        });

        var big = await Engine().ExecuteAsync(workflow, new Dictionary<string, object?> { ["amount"] = 500 });
        var small = await Engine().ExecuteAsync(workflow, new Dictionary<string, object?> { ["amount"] = 7 });

        Assert.Equal("big", big.Vars["branch"]);
        Assert.Equal("small", small.Vars["branch"]);
    }

    [Fact]
    public async Task Foreach_iterates_items_with_index()
    {
        var workflow = Workflow(
            new WorkflowStep { Type = "compose", Name = "items", Config = new() { ["value"] = """["a","b","c"]""" } },
            new WorkflowStep
            {
                Type = "foreach",
                Config = new() { ["items"] = "{{steps.items.output}}" },
                Children = [new WorkflowStep
                {
                    Type = "set_variable",
                    Config = new() { ["name"] = "last", ["value"] = "{{vars.index}}:{{vars.item}}" }
                }]
            });

        var ctx = await Engine().ExecuteAsync(workflow, null);
        Assert.Equal("2:c", ctx.Vars["last"]);
    }

    [Fact]
    public async Task Transform_builds_object_from_templates()
    {
        var workflow = Workflow(
            new WorkflowStep { Type = "set_variable", Config = new() { ["name"] = "first", ["value"] = "Budi" } },
            new WorkflowStep
            {
                Type = "transform",
                Name = "mapped",
                Config = new() { ["mapping"] = """{"greeting": "Hi {{vars.first}}!", "when": "{{today}}"}""" }
            });

        var ctx = await Engine().ExecuteAsync(workflow, null);
        var output = Assert.IsType<Dictionary<string, object?>>(
            ((Dictionary<string, object?>)ctx.StepOutputs["mapped"]!)["output"]);
        Assert.Equal("Hi Budi!", output["greeting"]);
    }

    [Fact]
    public async Task Unknown_action_fails_the_run()
    {
        var workflow = Workflow(new WorkflowStep { Type = "does_not_exist" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Engine().ExecuteAsync(workflow, null));
    }

    [Fact]
    public async Task Disabled_steps_are_skipped()
    {
        var workflow = Workflow(
            new WorkflowStep { Type = "set_variable", Disabled = true, Config = new() { ["name"] = "x", ["value"] = "1" } },
            new WorkflowStep { Type = "log", Config = new() { ["message"] = "ok" } });

        var ctx = await Engine().ExecuteAsync(workflow, null);
        Assert.False(ctx.Vars.ContainsKey("x"));
        Assert.Single(ctx.Logs);
    }
}
