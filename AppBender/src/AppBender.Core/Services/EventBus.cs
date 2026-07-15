using AppBender.Core.Models;

namespace AppBender.Core.Services;

public record RecordChangedEvent(string TenantId, string EntityName, string ChangeType, DataRecord Record);
public record FormSubmittedEvent(string TenantId, string FormId, string FormName, Dictionary<string, object?> Values, string? RecordId);

/// <summary>Singleton in-process event bus decoupling Data Hub / forms from workflow triggers.</summary>
public class EventBus
{
    public event Func<RecordChangedEvent, Task>? RecordChanged;
    public event Func<FormSubmittedEvent, Task>? FormSubmitted;

    public Task PublishAsync(RecordChangedEvent evt) => InvokeAll(RecordChanged, evt);
    public Task PublishAsync(FormSubmittedEvent evt) => InvokeAll(FormSubmitted, evt);

    private static async Task InvokeAll<T>(Func<T, Task>? handlers, T evt)
    {
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<Func<T, Task>>())
        {
            try { await handler(evt); }
            catch { /* subscriber failures must not break the publisher */ }
        }
    }
}
