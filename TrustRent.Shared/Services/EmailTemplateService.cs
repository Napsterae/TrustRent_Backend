using System.Net;
using System.Text.RegularExpressions;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class EmailTemplateService : IEmailTemplateService
{
    public string RenderTransactionalEmail(string subject, string body)
    {
        var normalizedBody = NormalizeBody(body);
        var previewText = BuildPreviewText(subject, body);

        return $"""
               <!doctype html>
               <html lang="pt">
                 <head>
                   <meta charset="utf-8" />
                   <meta name="viewport" content="width=device-width,initial-scale=1" />
                   <title>{WebUtility.HtmlEncode(subject)}</title>
                 </head>
                 <body style="margin:0;padding:0;background:#eef7f5;font-family:'Segoe UI',Arial,sans-serif;color:#12312e">
                   <div style="display:none;max-height:0;overflow:hidden;opacity:0">{WebUtility.HtmlEncode(previewText)}</div>
                   <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse;background:#eef7f5;padding:24px 0">
                     <tr>
                       <td align="center" style="padding:24px 16px">
                         <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse;max-width:680px;background:#ffffff;border:1px solid #d8ece8;border-radius:28px;overflow:hidden;box-shadow:0 20px 54px rgba(30,107,102,.16)">
                           <tr>
                             <td style="padding:0;background:linear-gradient(135deg,#7a3605 0%,#f29b4b 18%,#1e6b66 58%,#41b0a8 100%)">
                               <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse">
                                 <tr>
                                   <td style="padding:28px 32px 30px">
                                     <div style="display:inline-block;padding:8px 14px;border-radius:999px;background:rgba(255,245,234,.18);border:1px solid rgba(255,217,176,.22);font-size:12px;font-weight:700;letter-spacing:.16em;text-transform:uppercase;color:#fff6eb">Wekaza</div>
                                     <h1 style="margin:18px 0 8px;font-size:30px;line-height:1.15;color:#ffffff">{WebUtility.HtmlEncode(subject)}</h1>
                                     <p style="margin:0;font-size:15px;line-height:1.7;color:#d6fbf5;max-width:520px">Comunicação transacional segura enviada pela plataforma Wekaza.</p>
                                   </td>
                                 </tr>
                               </table>
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:34px 32px 30px;background:#ffffff">
                               {normalizedBody}
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:0 32px 32px;background:#ffffff">
                               <div style="padding:18px 20px;border:1px solid #e6dfd5;border-radius:20px;background:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);font-size:13px;line-height:1.7;color:#45625f">
                                 Este email foi enviado automaticamente pela Wekaza. Se não reconheces esta ação, ignora a mensagem.
                               </div>
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:0 32px 28px;background:#ffffff">
                               <div style="padding-top:20px;border-top:1px solid #eadfd1;font-size:12px;line-height:1.7;color:#a58b76">
                                 Wekaza · Plataforma de arrendamento · Email transacional
                               </div>
                             </td>
                           </tr>
                         </table>
                       </td>
                     </tr>
                   </table>
                 </body>
               </html>
               """;
    }

    private static string NormalizeBody(string body)
    {
        var trimmed = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return "<p style=\"margin:0;font-size:15px;line-height:1.7;color:#334155\">Sem conteúdo.</p>";

        if (trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        if (LooksLikeHtml(trimmed))
            return trimmed;

        var paragraphs = trimmed
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(paragraph => paragraph.Replace("\n", "<br />"))
            .Select(paragraph =>
                $"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.75;color:#334155\">{WebUtility.HtmlEncode(paragraph).Replace("&lt;br /&gt;", "<br />")}</p>");

        return string.Join(string.Empty, paragraphs);
    }

    private static bool LooksLikeHtml(string body)
        => Regex.IsMatch(body, @"<\s*[a-zA-Z][^>]*>", RegexOptions.CultureInvariant);

    private static string BuildPreviewText(string subject, string body)
    {
        var source = string.IsNullOrWhiteSpace(body) ? subject : body;
        var noTags = Regex.Replace(source, "<[^>]+>", " ");
        var normalized = Regex.Replace(WebUtility.HtmlDecode(noTags), @"\s+", " ").Trim();
        if (normalized.Length <= 120) return normalized;
        return normalized[..117] + "...";
    }
}