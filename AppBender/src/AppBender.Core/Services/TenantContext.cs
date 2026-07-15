namespace AppBender.Core.Services;

/// <summary>Ambient identity/tenant info for the current request or circuit.</summary>
public interface ITenantContext
{
    string TenantId { get; }
    string? UserId { get; }
    string UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    void Set(string tenantId, string? userId, string userName, IEnumerable<string>? roles = null);
}

public class TenantContext : ITenantContext
{
    public const string DefaultTenant = "default";

    public string TenantId { get; private set; } = DefaultTenant;
    public string? UserId { get; private set; }
    public string UserName { get; private set; } = "anonymous";
    public bool IsAuthenticated => UserId is not null;
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public void Set(string tenantId, string? userId, string userName, IEnumerable<string>? roles = null)
    {
        TenantId = string.IsNullOrEmpty(tenantId) ? DefaultTenant : tenantId;
        UserId = userId;
        UserName = userName;
        Roles = roles?.ToList() ?? [];
    }
}
