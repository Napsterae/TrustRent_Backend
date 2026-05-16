using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Models;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminCommunicationsEndpoints
{
    private static readonly Regex SemanticVersionRegex = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")!.Value);

    public record BroadcastRequest(string Title, string Body, string Audience, string Channel, DateTime? ScheduledAt);
    public record EmailTemplateCreateRequest(string Key, string Name, string Version, string Subject, string BodyHtml, string? BodyText, string Locale, string? Description, bool IsActive);
    public record EmailTemplateUpdateRequest(string Name, string Version, string Subject, string BodyHtml, string? BodyText, string Locale, string? Description, bool IsActive);
    public record LegalDocumentVersionRequest(string DocumentType, string Version, string Title, string Summary, string ChangeSummary, string BodyHtml, string? BodyText, Guid? BasedOnVersionId, bool NotifyUsers);
    public record BannerRequest(string Title, string Body, string Severity, string Audience, bool IsActive, DateTime? StartsAt, DateTime? EndsAt);

    public static void MapAdminCommunicationsEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Broadcasts =====
        var bc = app.MapGroup("/api/admin/broadcasts");

        bc.MapGet("/", async (CommunicationsDbContext db) =>
        {
            var items = await db.Broadcasts.AsNoTracking().OrderByDescending(b => b.CreatedAt).ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        bc.MapPost("/", async ([FromBody] BroadcastRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Body))
                return Results.BadRequest(new { error = "Título e corpo obrigatórios." });
            var adminId = GetAdminId(ctx);
            var b = new Broadcast
            {
                Id = Guid.NewGuid(),
                Title = req.Title.Trim(),
                Body = req.Body,
                Audience = req.Audience ?? "all",
                Channel = req.Channel ?? "in_app",
                ScheduledAt = req.ScheduledAt,
                Status = req.ScheduledAt.HasValue ? "scheduled" : "draft",
                CreatedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow
            };
            db.Broadcasts.Add(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(adminId, "broadcast.create", "Broadcast", b.Id.ToString(), null, JsonSerializer.Serialize(b), null, ctx);
            return Results.Created($"/api/admin/broadcasts/{b.Id}", b);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        bc.MapPost("/{id:guid}/send", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Broadcasts.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            if (b.Status == "sent") return Results.BadRequest(new { error = "Já enviado." });
            b.Status = "sent";
            b.SentAt = DateTime.UtcNow;
            b.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "broadcast.send", "Broadcast", id.ToString(), null, JsonSerializer.Serialize(new { b.Status, b.SentAt }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        bc.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Broadcasts.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            if (b.Status == "sent") return Results.BadRequest(new { error = "Não é possível eliminar broadcasts enviados." });
            db.Broadcasts.Remove(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "broadcast.delete", "Broadcast", id.ToString(), JsonSerializer.Serialize(b), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        // ===== Templates =====
        var tp = app.MapGroup("/api/admin/templates");

        tp.MapGet("/", async (CommunicationsDbContext db) =>
        {
            var items = await db.EmailTemplates
                .AsNoTracking()
                .OrderBy(t => t.Key)
                .ThenBy(t => t.Locale)
                .ThenByDescending(t => t.IsActive)
                .ThenByDescending(t => t.UpdatedAt)
                .ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapGet("/catalog", (ICommunicationContentService content) =>
        {
            var items = content.GetEmailTemplateCatalog()
                .Select(definition => new
                {
                    definition.Key,
                    definition.DisplayName,
                    definition.Category,
                    definition.Description,
                    definition.DefaultVersion,
                    Variables = content.GetVariableDefinitions(definition.Key)
                        .Select(variable => new
                        {
                            variable.Key,
                            variable.Label,
                            variable.Description,
                            variable.Category,
                            variable.IsConfigurable,
                            variable.SettingKey,
                            variable.ExampleValue
                        })
                });

            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapPost("/", async ([FromBody] EmailTemplateCreateRequest req, HttpContext ctx, CommunicationsDbContext db, ICommunicationContentService content, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Key)) return Results.BadRequest(new { error = "Key obrigatória." });
            if (!content.GetEmailTemplateCatalog().Any(item => item.Key == req.Key))
                return Results.BadRequest(new { error = "Tipo de template inválido." });
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.BodyHtml))
                return Results.BadRequest(new { error = "Nome, versão, assunto e corpo HTML são obrigatórios." });
            if (!SemanticVersionRegex.IsMatch(req.Version.Trim()))
                return Results.BadRequest(new { error = "A versão deve seguir o formato semântico x.y.z." });

            var locale = string.IsNullOrWhiteSpace(req.Locale) ? "pt-PT" : req.Locale.Trim();
            var version = req.Version.Trim();

            if (await db.EmailTemplates.AnyAsync(t => t.Key == req.Key && t.Locale == locale && t.Version == version))
                return Results.BadRequest(new { error = "Já existe um template para esta chave, locale e versão." });

            if (req.IsActive)
            {
                var activeTemplates = await db.EmailTemplates
                    .Where(template => template.Key == req.Key && template.Locale == locale && template.IsActive)
                    .ToListAsync();
                foreach (var activeTemplate in activeTemplates)
                    activeTemplate.IsActive = false;
            }

            var adminId = GetAdminId(ctx);
            var created = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                Key = req.Key.Trim(),
                Name = req.Name.Trim(),
                Version = version,
                Subject = req.Subject.Trim(),
                BodyHtml = req.BodyHtml.Trim(),
                BodyText = string.IsNullOrWhiteSpace(req.BodyText) ? null : req.BodyText,
                Locale = locale,
                Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
                IsActive = req.IsActive,
                IsSystemDefault = false,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = adminId,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByAdminId = adminId
            };

            db.EmailTemplates.Add(created);
            await db.SaveChangesAsync();

            await audit.WriteAsync(adminId, "template.create", "EmailTemplate", created.Id.ToString(), null, JsonSerializer.Serialize(created), null, ctx);
            return Results.Created($"/api/admin/templates/{created.Id}", created);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapPut("/{id:guid}", async (Guid id, [FromBody] EmailTemplateUpdateRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id);
            if (template is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Version) || string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.BodyHtml))
                return Results.BadRequest(new { error = "Nome, versão, assunto e corpo HTML são obrigatórios." });
            if (!SemanticVersionRegex.IsMatch(req.Version.Trim()))
                return Results.BadRequest(new { error = "A versão deve seguir o formato semântico x.y.z." });

            var locale = string.IsNullOrWhiteSpace(req.Locale) ? template.Locale : req.Locale.Trim();
            var version = req.Version.Trim();

            var duplicate = await db.EmailTemplates.AnyAsync(entry =>
                entry.Id != id && entry.Key == template.Key && entry.Locale == locale && entry.Version == version);
            if (duplicate)
                return Results.BadRequest(new { error = "Já existe um template para esta chave, locale e versão." });

            if (req.IsActive)
            {
                var activeTemplates = await db.EmailTemplates
                    .Where(entry => entry.Id != id && entry.Key == template.Key && entry.Locale == locale && entry.IsActive)
                    .ToListAsync();
                foreach (var activeTemplate in activeTemplates)
                    activeTemplate.IsActive = false;
            }

            var before = JsonSerializer.Serialize(template);
            template.Name = req.Name.Trim();
            template.Version = version;
            template.Subject = req.Subject.Trim();
            template.BodyHtml = req.BodyHtml.Trim();
            template.BodyText = string.IsNullOrWhiteSpace(req.BodyText) ? null : req.BodyText;
            template.Locale = locale;
            template.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
            template.IsActive = req.IsActive;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedByAdminId = GetAdminId(ctx);

            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "template.update", "EmailTemplate", template.Id.ToString(), before, JsonSerializer.Serialize(template), null, ctx);
            return Results.Ok(template);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapPost("/{id:guid}/activate", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var template = await db.EmailTemplates.FirstOrDefaultAsync(entry => entry.Id == id);
            if (template is null) return Results.NotFound();

            var activeTemplates = await db.EmailTemplates
                .Where(entry => entry.Id != id && entry.Key == template.Key && entry.Locale == template.Locale && entry.IsActive)
                .ToListAsync();
            foreach (var activeTemplate in activeTemplates)
                activeTemplate.IsActive = false;

            template.IsActive = true;
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedByAdminId = GetAdminId(ctx);
            await db.SaveChangesAsync();

            await audit.WriteAsync(GetAdminId(ctx), "template.activate", "EmailTemplate", template.Id.ToString(), null, JsonSerializer.Serialize(new { template.IsActive }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var t = await db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            if (t.IsActive) return Results.BadRequest(new { error = "Não é possível eliminar o template ativo. Ativa outro primeiro." });
            db.EmailTemplates.Remove(t);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "template.delete", "EmailTemplate", id.ToString(), JsonSerializer.Serialize(t), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        // ===== Legal documents =====
        var ld = app.MapGroup("/api/admin/legal-documents");

        ld.MapGet("/", async (CommunicationsDbContext db, ICommunicationContentService content) =>
        {
            var versions = await db.LegalDocumentVersions
                .AsNoTracking()
                .OrderBy(entry => entry.DocumentType)
                .ThenByDescending(entry => entry.PublishedAt)
                .ToListAsync();

            var items = content.GetLegalDocumentCatalog()
                .Select(definition => new
                {
                    definition.DocumentType,
                    definition.Title,
                    definition.DefaultVersion,
                    definition.Summary,
                    Variables = content.GetVariableDefinitions(documentType: definition.DocumentType)
                        .Select(variable => new
                        {
                            variable.Key,
                            variable.Label,
                            variable.Description,
                            variable.Category,
                            variable.IsConfigurable,
                            variable.SettingKey,
                            variable.ExampleValue
                        }),
                    Versions = versions
                        .Where(entry => entry.DocumentType == definition.DocumentType)
                        .Select(entry => new
                        {
                            entry.Id,
                            entry.DocumentType,
                            entry.Version,
                            entry.Title,
                            entry.Summary,
                            entry.ChangeSummary,
                            entry.BodyHtml,
                            entry.BodyText,
                            entry.IsCurrent,
                            entry.NotifyUsers,
                            entry.BasedOnVersionId,
                            entry.CreatedAt,
                            entry.PublishedAt,
                            entry.NotificationRequestedAt,
                            entry.NotificationSentAt
                        })
                });

            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        ld.MapPost("/", async ([FromBody] LegalDocumentVersionRequest req, HttpContext ctx, CommunicationsDbContext db, ICommunicationContentService content, IBackgroundJobClient backgroundJobs, IAuditLogService audit) =>
        {
            var documentType = NormalizeLegalDocumentType(req.DocumentType);
            if (documentType is null)
                return Results.BadRequest(new { error = "Tipo de documento inválido." });
            if (string.IsNullOrWhiteSpace(req.Version) || !SemanticVersionRegex.IsMatch(req.Version.Trim()))
                return Results.BadRequest(new { error = "A versão deve seguir o formato semântico x.y.z." });
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.BodyHtml))
                return Results.BadRequest(new { error = "Título e conteúdo HTML são obrigatórios." });

            var version = req.Version.Trim();
            if (await db.LegalDocumentVersions.AnyAsync(entry => entry.DocumentType == documentType && entry.Version == version))
                return Results.BadRequest(new { error = "Já existe esta versão para o documento selecionado." });

            var adminId = GetAdminId(ctx);
            var currentVersions = await db.LegalDocumentVersions
                .Where(entry => entry.DocumentType == documentType && entry.IsCurrent)
                .ToListAsync();
            foreach (var currentVersion in currentVersions)
                currentVersion.IsCurrent = false;

            var created = new LegalDocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentType = documentType,
                Version = version,
                Title = req.Title.Trim(),
                Summary = string.IsNullOrWhiteSpace(req.Summary) ? string.Empty : req.Summary.Trim(),
                ChangeSummary = string.IsNullOrWhiteSpace(req.ChangeSummary) ? string.Empty : req.ChangeSummary.Trim(),
                BodyHtml = req.BodyHtml.Trim(),
                BodyText = string.IsNullOrWhiteSpace(req.BodyText) ? null : req.BodyText,
                IsCurrent = true,
                NotifyUsers = req.NotifyUsers,
                BasedOnVersionId = req.BasedOnVersionId,
                CreatedAt = DateTime.UtcNow,
                CreatedByAdminId = adminId,
                PublishedAt = DateTime.UtcNow,
                PublishedByAdminId = adminId,
                NotificationRequestedAt = req.NotifyUsers ? DateTime.UtcNow : null,
                NotificationRequestedByAdminId = req.NotifyUsers ? adminId : null
            };

            db.LegalDocumentVersions.Add(created);
            await db.SaveChangesAsync();

            await audit.WriteAsync(adminId, "legal-document.publish", "LegalDocumentVersion", created.Id.ToString(), null, JsonSerializer.Serialize(created), null, ctx);

            if (req.NotifyUsers)
                backgroundJobs.Enqueue<ILegalDocumentNotificationJob>(job => job.SendPublishedVersionNotificationAsync(created.Id));

            var rendered = await content.GetLegalDocumentAsync(documentType, created.Version);
            object response = rendered is null
                ? created
                : new
                {
                    created.Id,
                    rendered.DocumentType,
                    rendered.Title,
                    rendered.Version,
                    rendered.Summary,
                    rendered.ChangeSummary,
                    rendered.BodyHtml,
                    rendered.BodyText,
                    rendered.IsCurrent,
                    rendered.PublishedAt,
                    created.NotifyUsers,
                    created.BasedOnVersionId,
                    created.NotificationRequestedAt,
                    created.NotificationSentAt
                };

            return Results.Created($"/api/admin/legal-documents/{created.Id}", response);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        app.MapGet("/api/admin/communications/builder-config", (ICommunicationContentService content) =>
        {
            return Results.Ok(new
            {
                EmailTemplates = content.GetEmailTemplateCatalog().Select(definition => new
                {
                    definition.Key,
                    definition.DisplayName,
                    definition.Category,
                    definition.Description,
                    definition.DefaultVersion,
                    Variables = content.GetVariableDefinitions(definition.Key)
                        .Select(variable => new
                        {
                            variable.Key,
                            variable.Label,
                            variable.Description,
                            variable.Category,
                            variable.IsConfigurable,
                            variable.SettingKey,
                            variable.ExampleValue
                        })
                }),
                LegalDocuments = content.GetLegalDocumentCatalog().Select(definition => new
                {
                    definition.DocumentType,
                    definition.Title,
                    definition.DefaultVersion,
                    definition.Summary,
                    Variables = content.GetVariableDefinitions(documentType: definition.DocumentType)
                        .Select(variable => new
                        {
                            variable.Key,
                            variable.Label,
                            variable.Description,
                            variable.Category,
                            variable.IsConfigurable,
                            variable.SettingKey,
                            variable.ExampleValue
                        })
                }),
                Variables = content.GetVariableDefinitions()
                    .Select(variable => new
                    {
                        variable.Key,
                        variable.Label,
                        variable.Description,
                        variable.Category,
                        variable.IsConfigurable,
                        variable.SettingKey,
                        variable.ExampleValue,
                        variable.AppliesToKeys
                    })
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        // ===== Banners =====
        var bn = app.MapGroup("/api/admin/banners");

        bn.MapGet("/", async (CommunicationsDbContext db) =>
        {
            var items = await db.Banners.AsNoTracking().OrderByDescending(b => b.CreatedAt).ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));

        bn.MapPost("/", async ([FromBody] BannerRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest(new { error = "Título obrigatório." });
            var b = new Banner
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Body = req.Body ?? string.Empty,
                Severity = req.Severity ?? "info",
                Audience = req.Audience ?? "all",
                IsActive = req.IsActive,
                StartsAt = req.StartsAt,
                EndsAt = req.EndsAt,
                CreatedByAdminId = GetAdminId(ctx),
                CreatedAt = DateTime.UtcNow
            };
            db.Banners.Add(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "banner.create", "Banner", b.Id.ToString(), null, JsonSerializer.Serialize(b), null, ctx);
            return Results.Created($"/api/admin/banners/{b.Id}", b);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));

        bn.MapPut("/{id:guid}", async (Guid id, [FromBody] BannerRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Banners.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(b);
            b.Title = req.Title;
            b.Body = req.Body ?? string.Empty;
            b.Severity = req.Severity ?? "info";
            b.Audience = req.Audience ?? "all";
            b.IsActive = req.IsActive;
            b.StartsAt = req.StartsAt;
            b.EndsAt = req.EndsAt;
            b.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "banner.update", "Banner", id.ToString(), before, JsonSerializer.Serialize(b), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));

        bn.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Banners.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            db.Banners.Remove(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "banner.delete", "Banner", id.ToString(), JsonSerializer.Serialize(b), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));
    }

    private static string? NormalizeLegalDocumentType(string? documentType)
    {
        var normalized = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            LegalDocumentTypes.PrivacyPolicy => LegalDocumentTypes.PrivacyPolicy,
            LegalDocumentTypes.TermsOfUse => LegalDocumentTypes.TermsOfUse,
            "privacy-policy" => LegalDocumentTypes.PrivacyPolicy,
            "terms-of-use" => LegalDocumentTypes.TermsOfUse,
            _ => null
        };
    }
}
