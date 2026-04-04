using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Contracts.Database;

namespace TrustRent.Modules.Communications.Endpoints;

public static class CommunicationsEndpoints
{
    public static void MapCommunicationsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Communications");

        group.MapGet("/applications/{applicationId:guid}/chat", async (Guid applicationId, CommunicationsDbContext db) =>
        {
            var messages = await db.Messages
                .Where(m => m.ContextId == applicationId && m.ContextType == Models.MessageContextType.Application)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new {
                    m.Id,
                    m.SenderId,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(messages);
        })
        .RequireAuthorization();
    }
}
