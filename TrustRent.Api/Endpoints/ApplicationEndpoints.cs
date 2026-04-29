using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        // Tenant submits an application
        group.MapPost("/properties/{propertyId:guid}/applications", async (Guid propertyId, [FromBody] SubmitApplicationDto dto, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            try
            {
                var application = await service.SubmitApplicationAsync(propertyId, tenantId, dto);
                return Results.Ok(application);
            }
            catch (KeyNotFoundException e) { return Results.NotFound(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (Exception e) { return Results.BadRequest(new { error = e.Message }); }
        }).RequireAuthorization();

        // Landlord gets applications for a property
        group.MapGet("/properties/{propertyId:guid}/applications", async (Guid propertyId, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid landlordId))
                return Results.Unauthorized();

            try
            {
                var applications = await service.GetApplicationsForPropertyAsync(propertyId, landlordId);
                return Results.Ok(applications);
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        }).RequireAuthorization();

        // Tenant gets their own applications
        group.MapGet("/applications/tenant", async (IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            var applications = await service.GetApplicationsForTenantAsync(tenantId);
            return Results.Ok(applications);
        }).RequireAuthorization();

        // Specific Application Action (Landlord or Tenant update visit logic)
        group.MapGet("/applications/{id:guid}", async (Guid id, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            try
            {
                var application = await service.GetApplicationByIdAsync(id, userId);
                if (application == null) return Results.NotFound();
                return Results.Ok(application);
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        }).RequireAuthorization();

        group.MapPut("/applications/{id:guid}/visit", async (Guid id, [FromBody] UpdateApplicationVisitDto dto, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            try
            {
                var result = await service.UpdateVisitStatusAsync(id, userId, dto);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception e) { return Results.BadRequest(new { error = e.Message }); }
        }).RequireAuthorization();

        // ===== Income Validation (recibos de vencimento via IA) =====

        // Senhorio pede ao inquilino para validar rendimentos
        group.MapPost("/applications/{id:guid}/income-validation/request",
            async (Guid id, IIncomeValidationService svc, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid landlordId))
                return Results.Unauthorized();

            try
            {
                await svc.RequestValidationAsync(id, landlordId);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { Error = ex.Message }); }
        }).RequireAuthorization();

        // Inquilino faz upload dos documentos de validação de rendimentos.
        // Campos multipart suportados:
        //   - employmentType: "Employee" (default) | "SelfEmployed"
        //   - payslips: 1-3 ficheiros (recibos de vencimento OU recibos verdes consoante o tipo)
        //   - employerDeclaration: 1 ficheiro (apenas Employee, obrigatório se payslips < 3)
        //   - activityDeclaration: 1 ficheiro (apenas SelfEmployed, obrigatório)
        group.MapPost("/applications/{id:guid}/income-validation",
            async (Guid id, HttpRequest request, IIncomeValidationService svc, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            if (!request.HasFormContentType)
                return Results.BadRequest(new { Error = "Pedido tem de ser multipart/form-data." });

            var form = await request.ReadFormAsync();

            // Tipo de relação laboral
            var typeRaw = form["employmentType"].ToString();
            EmploymentType empType = EmploymentType.Employee;
            if (!string.IsNullOrWhiteSpace(typeRaw)
                && !Enum.TryParse<EmploymentType>(typeRaw, ignoreCase: true, out empType))
                return Results.BadRequest(new { Error = $"Tipo de emprego inválido: '{typeRaw}'." });

            const long maxBytes = 8L * 1024 * 1024;
            var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf", "image/jpeg", "image/png", "image/webp"
            };

            IResult? ValidateFile(IFormFile f)
            {
                if (f.Length <= 0 || f.Length > maxBytes)
                    return Results.BadRequest(new { Error = $"Cada ficheiro tem de ter entre 1 byte e 8 MB ('{f.FileName}')." });
                if (!allowedMime.Contains(f.ContentType))
                    return Results.BadRequest(new { Error = $"Formato não suportado para '{f.FileName}'. Usa PDF, JPG, PNG ou WEBP." });
                return null;
            }

            var payslipFiles = form.Files.GetFiles("payslips");
            var declFiles = form.Files.GetFiles("employerDeclaration");
            var actFiles = form.Files.GetFiles("activityDeclaration");

            if (payslipFiles.Count == 0)
                return Results.BadRequest(new { Error = "Tens de enviar pelo menos um recibo." });
            if (payslipFiles.Count > svc.MaxPayslipCount)
                return Results.BadRequest(new { Error = $"Máximo de {svc.MaxPayslipCount} recibos." });
            if (declFiles.Count > 1)
                return Results.BadRequest(new { Error = "Apenas uma declaração de empregador é aceite." });
            if (actFiles.Count > 1)
                return Results.BadRequest(new { Error = "Apenas uma declaração de atividade é aceite." });

            foreach (var f in payslipFiles.Concat(declFiles).Concat(actFiles))
            {
                var err = ValidateFile(f);
                if (err != null) return err;
            }

            var openedStreams = new List<Stream>();
            try
            {
                (Stream, string) Open(IFormFile f)
                {
                    var s = f.OpenReadStream();
                    openedStreams.Add(s);
                    return (s, f.FileName);
                }

                var payslips = payslipFiles.Select(Open).ToList();
                (Stream, string)? employerDecl = declFiles.Count == 1 ? Open(declFiles[0]) : null;
                (Stream, string)? activityDecl = actFiles.Count == 1 ? Open(actFiles[0]) : null;

                var submission = new IncomeValidationSubmissionDto(empType, payslips, employerDecl, activityDecl);
                var result = await svc.ValidateAsync(id, tenantId, submission);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { Error = ex.Message }); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return Results.BadRequest(new { Error = ex.Message }); }
            finally
            {
                foreach (var s in openedStreams) s.Dispose();
            }
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .RequireRateLimiting("incomeValidation");

        // DEV-ONLY: simular validação de rendimentos sem ficheiros nem chamada à IA.
        // Query opcional ?scenario=employee | employee-declaration | self-employed
        group.MapPost("/applications/{id:guid}/income-validation/simulate",
            async (Guid id, string? scenario, IIncomeValidationService svc, ClaimsPrincipal user, IWebHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            try
            {
                var result = await svc.SimulateValidationAsync(id, tenantId, scenario);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return Results.BadRequest(new { Error = ex.Message }); }
        }).RequireAuthorization();
    }
}
