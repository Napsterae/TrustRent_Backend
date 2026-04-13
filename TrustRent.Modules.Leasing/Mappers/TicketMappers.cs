using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Contracts.DTOs;

namespace TrustRent.Modules.Leasing.Mappers;

public static class TicketMappers
{
    public static TicketDto ToDto(this Ticket ticket)
    {
        return new TicketDto
        {
            Id = ticket.Id,
            LeaseId = ticket.LeaseId,
            TenantId = ticket.TenantId,
            LandlordId = ticket.LandlordId,
            Title = ticket.Title,
            Description = ticket.Description,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            ResolvedAt = ticket.ResolvedAt,
            UpdatedAt = ticket.UpdatedAt,
            Comments = ticket.Comments?
                .GroupBy(c => c.Id)
                .Select(group => group.First())
                .Select(c => c.ToCommentDto())
                .OrderBy(c => c.CreatedAt)
                .ToList() ?? new(),
            Attachments = ticket.Attachments?
                .GroupBy(a => a.Id)
                .Select(group => group.First())
                .Select(a => a.ToAttachmentDto())
                .OrderBy(a => a.UploadedAt)
                .ToList() ?? new()
        };
    }

    public static TicketListItemDto ToListDto(this Ticket ticket)
    {
        return new TicketListItemDto
        {
            Id = ticket.Id,
            LeaseId = ticket.LeaseId,
            Title = ticket.Title,
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = ticket.CreatedAt,
            CommentCount = ticket.Comments?.Count ?? 0,
            AttachmentCount = ticket.Attachments?.Count ?? 0
        };
    }

    public static TicketCommentDto ToCommentDto(this TicketComment comment)
    {
        return new TicketCommentDto
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            AuthorId = comment.AuthorId,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        };
    }

    public static TicketAttachmentDto ToAttachmentDto(this TicketAttachment attachment)
    {
        return new TicketAttachmentDto
        {
            Id = attachment.Id,
            TicketId = attachment.TicketId,
            StorageUrl = attachment.StorageUrl,
            FileName = attachment.FileName,
            UploadedAt = attachment.UploadedAt
        };
    }
}
