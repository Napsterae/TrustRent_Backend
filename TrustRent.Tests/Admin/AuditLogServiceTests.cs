using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Services;

namespace TrustRent.Tests.Admin;

public class AuditLogServiceTests
{
    private static AdminDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .Options;
        return new AdminDbContext(opts);
    }

    [Fact]
    public async Task Write_Persiste_Entrada_Com_Ip_UA_E_CorrelationId()
    {
        await using var db = NewDb();
        var svc = new AuditLogService(db);
        var adminId = Guid.NewGuid();

        await svc.WriteAsync(adminId, "user.suspend", "User", "u-1",
            beforeJson: "{\"x\":1}", afterJson: "{\"x\":2}", reason: "abuso",
            ip: "10.0.0.5", userAgent: "Mozilla/5.0", correlationId: "trace-abc");

        var log = await db.AuditLogs.SingleAsync();
        log.AdminUserId.Should().Be(adminId);
        log.Action.Should().Be("user.suspend");
        log.EntityType.Should().Be("User");
        log.EntityId.Should().Be("u-1");
        log.Reason.Should().Be("abuso");
        log.Ip.Should().Be("10.0.0.5");
        log.UserAgent.Should().Be("Mozilla/5.0");
        log.CorrelationId.Should().Be("trace-abc");
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Write_Mascara_Campos_Sensiveis_No_Json()
    {
        await using var db = NewDb();
        var svc = new AuditLogService(db);
        var adminId = Guid.NewGuid();

        await svc.WriteAsync(adminId, "user.update", "User", "u-1",
            beforeJson: "{\"PasswordHash\":\"secret-old\",\"Name\":\"A\"}",
            afterJson: "{\"PasswordHash\":\"secret-new\",\"Name\":\"B\"}",
            reason: null, ip: null, userAgent: null, correlationId: null);

        var log = await db.AuditLogs.SingleAsync();
        log.BeforeJson.Should().NotContain("secret-old");
        log.AfterJson.Should().NotContain("secret-new");
        log.AfterJson.Should().Contain("Name");
    }
}
