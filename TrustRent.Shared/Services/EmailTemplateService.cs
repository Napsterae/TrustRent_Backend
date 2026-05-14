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
               <html lang="pt" xmlns="http://www.w3.org/1999/xhtml">
                 <head>
                   <meta charset="utf-8" />
                   <meta name="viewport" content="width=device-width,initial-scale=1" />
                   <meta name="color-scheme" content="light only" />
                   <meta name="supported-color-schemes" content="light only" />
                   <title>{WebUtility.HtmlEncode(subject)}</title>
                 </head>
                 <body style="margin:0;padding:0;background-color:#f5f1eb;font-family:'Segoe UI',Arial,sans-serif;color:#1f2937;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%">
                   <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all">{WebUtility.HtmlEncode(previewText)}</div>
                   <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;background-color:#f5f1eb;mso-table-lspace:0pt;mso-table-rspace:0pt">
                     <tr>
                       <td align="center" style="padding:24px 12px">
                         <!--[if mso]>
                         <table role="presentation" width="680" cellspacing="0" cellpadding="0" border="0">
                           <tr>
                             <td>
                         <![endif]-->
                         <table role="presentation" width="680" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;width:100%;max-width:680px;background-color:#ffffff;border:1px solid #ddd4c8;mso-table-lspace:0pt;mso-table-rspace:0pt">
                           <tr>
                             <td style="padding:0;background-color:#1b232c">
                               <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt">
                                 <tr>
                                   <td style="padding:28px 32px 30px">
                                     <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt">
                                       <tr>
                                         <td style="padding:8px 14px;border:1px solid #8f7862;background-color:#2a333d;font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#f6ebdf">
                                           WEKAZA
                                         </td>
                                       </tr>
                                     </table>
                                     <h1 style="margin:18px 0 8px;font-size:30px;line-height:1.15;color:#ffffff;font-weight:700">{WebUtility.HtmlEncode(subject)}</h1>
                                     <p style="margin:0;font-size:15px;line-height:1.7;color:#dbe5ea">Comunicação transacional segura enviada pela plataforma Wekaza.</p>
                                   </td>
                                 </tr>
                               </table>
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:32px 28px 28px;background-color:#ffffff">
                               {normalizedBody}
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:0 28px 24px;background-color:#ffffff">
                               <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;background-color:#f7f3ee;border:1px solid #e2d8cc;mso-table-lspace:0pt;mso-table-rspace:0pt">
                                 <tr>
                                   <td style="padding:18px 20px;font-size:13px;line-height:1.7;color:#4b5563">
                                     Este email foi enviado automaticamente pela Wekaza. Se não reconheces esta ação, ignora a mensagem.
                                   </td>
                                 </tr>
                               </table>
                             </td>
                           </tr>
                           <tr>
                             <td style="padding:0 28px 28px;background-color:#ffffff">
                               <div style="padding-top:20px;border-top:1px solid #e5ddd3;font-size:12px;line-height:1.7;color:#8b735f">
                                 Wekaza · Plataforma de arrendamento · Email transacional
                               </div>
                             </td>
                           </tr>
                         </table>
                         <!--[if mso]>
                             </td>
                           </tr>
                         </table>
                         <![endif]-->
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
              $"<p style=\"margin:0 0 16px;font-size:15px;line-height:1.75;color:#374151\">{WebUtility.HtmlEncode(paragraph).Replace("&lt;br /&gt;", "<br />")}</p>");

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