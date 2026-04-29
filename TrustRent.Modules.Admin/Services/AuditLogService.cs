using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AdminDbContext _db;

    public AuditLogService(AdminDbContext db) => _db = db;

    public async Task WriteAsync(Guid adminUserId, string action, string entityType, string? entityId,
        string? beforeJson, string? afterJson, string? reason, string? ip, string? userAgent, string? correlationId,
        CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeJson = MaskSensitive(beforeJson),
            AfterJson = MaskSensitive(afterJson),
            Reason = reason,
            Ip = ip,
            UserAgent = userAgent,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        };
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    private static readonly string[] MaskedKeys = ["password", "passwordhash", "mfasecret", "secret", "token", "tokenhash", "stripe", "citizencard", "phonenumber"];

    private static string? MaskSensitive(string? json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        // Cheap masking: regex replace common keys' values. Best-effort, never throws.
        try
        {
            var result = json;
            foreach (var key in MaskedKeys)
            {
                var pattern = "\"" + key + "\"\\s*:\\s*\"[^\"]*\"";
                result = System.Text.RegularExpressions.Regex.Replace(result, pattern,
                    "\"" + key + "\":\"***\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return result;
        }
        catch
        {
            return json;
        }
    }
}
