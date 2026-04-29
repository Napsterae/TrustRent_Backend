using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminJobsEndpoints
{
    private static readonly HashSet<string> RunnableRecurringJobs = new(StringComparer.OrdinalIgnoreCase)
    {
        "daily-maintenance"
    };

    public static void MapAdminJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/jobs");

        group.MapGet("/", () =>
        {
            var monitoring = JobStorage.Current.GetMonitoringApi();
            var stats = monitoring.GetStatistics();

            using var connection = JobStorage.Current.GetConnection();
            var recurring = connection.GetRecurringJobs()
                .OrderBy(job => job.Id)
                .Select(job => new
                {
                    job.Id,
                    job.Cron,
                    job.Queue,
                    job.LastExecution,
                    job.NextExecution,
                    job.LastJobId,
                    job.LastJobState,
                    Error = job.Error,
                    CanRun = RunnableRecurringJobs.Contains(job.Id)
                })
                .ToList();

            return Results.Ok(new
            {
                Stats = new
                {
                    stats.Enqueued,
                    stats.Failed,
                    stats.Processing,
                    stats.Scheduled,
                    stats.Succeeded,
                    stats.Deleted,
                    stats.Queues,
                    stats.Servers,
                    stats.Recurring
                },
                RecurringJobs = recurring
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.JobsRead));

        group.MapPost("/{id}/run", async (string id, HttpContext ctx, IAuditLogService audit) =>
        {
            if (!RunnableRecurringJobs.Contains(id))
            {
                return Results.BadRequest(new { error = "Job não permitido para execução manual." });
            }

            RecurringJob.TriggerJob(id);
            var adminId = AdminAuthEndpoints.GetAdminId(ctx);
            if (adminId.HasValue)
            {
                await audit.WriteAsync(adminId.Value, "job.run", "HangfireRecurringJob", id, null, null, null, ctx);
            }

            return Results.Accepted($"/api/admin/jobs/{id}", new { id, message = "Job colocado em execução." });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.JobsRun));
    }
}