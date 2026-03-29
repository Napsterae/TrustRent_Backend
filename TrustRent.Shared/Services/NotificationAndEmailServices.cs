using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class EmailService : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // Aqui integras com SendGrid, AWS SES ou Mailgun
        Console.WriteLine($"[EMAIL ENVIADO] Para: {to} | Assunto: {subject}");
        await Task.CompletedTask;
    }
}

public class NotificationService : INotificationService
{
    public async Task SendNotificationAsync(Guid userId, string type, string message)
    {
        // Futuramente: Gravar na tabela de Notificações ou enviar via WebSockets (SignalR)
        Console.WriteLine($"[NOTIFICAÇÃO] User: {userId} | Tipo: {type} | Msg: {message}");
        await Task.CompletedTask;
    }
}