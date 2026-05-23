using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Services;

namespace TrustRent.Tests.Shared;

public class EmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_UsesResendProvider_WhenConfigured()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["EmailSettings:UseSmtp"] = "false",
            ["EmailSettings:Provider"] = "Resend",
            ["EmailSettings:FromAddress"] = "noreply@example.com",
            ["EmailSettings:FromName"] = "TrustRent",
            ["EmailSettings:ReplyToAddress"] = "reply@example.com"
        });

        var templateService = new Mock<IEmailTemplateService>();
        templateService
            .Setup(service => service.RenderTransactionalEmail("Subject", "<p>Hello</p>"))
            .Returns("<html><body>Hello</body></html>");

        var resendSender = new Mock<IResendEmailSender>();
        var sesSender = new Mock<IAmazonSesEmailSender>();

        var sut = new EmailService(
            config,
            templateService.Object,
            resendSender.Object,
            sesSender.Object,
            NullLogger<EmailService>.Instance);

        await sut.SendEmailAsync("dest@example.com", "Subject", "<p>Hello</p>");

        resendSender.Verify(
            sender => sender.SendAsync(
                It.Is<EmailProviderMessage>(message =>
                    message.To == "dest@example.com"
                    && message.Subject == "Subject"
                    && message.HtmlBody == "<html><body>Hello</body></html>"
                    && message.TextBody == "Hello"
                    && message.FromAddress == "noreply@example.com"
                    && message.FromName == "TrustRent"
                    && message.ReplyToAddress == "reply@example.com"),
                It.Is<int>(timeout => timeout == 15),
                It.IsAny<CancellationToken>()),
            Times.Once);

        sesSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailProviderMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_UsesSesProvider_WhenConfigured()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["EmailSettings:UseSmtp"] = "false",
            ["EmailSettings:Provider"] = "Ses",
            ["EmailSettings:FromAddress"] = "noreply@example.com",
            ["EmailSettings:FromName"] = "TrustRent"
        });

        var templateService = new Mock<IEmailTemplateService>();
        templateService
            .Setup(service => service.RenderTransactionalEmail("Subject", "Hello"))
            .Returns("<html><body>Hello</body></html>");

        var resendSender = new Mock<IResendEmailSender>();
        var sesSender = new Mock<IAmazonSesEmailSender>();

        var sut = new EmailService(
            config,
            templateService.Object,
            resendSender.Object,
            sesSender.Object,
            NullLogger<EmailService>.Instance);

        await sut.SendEmailAsync("dest@example.com", "Subject", "Hello");

        sesSender.Verify(
            sender => sender.SendAsync(
                It.Is<EmailProviderMessage>(message =>
                    message.To == "dest@example.com"
                    && message.Subject == "Subject"
                    && message.TextBody == "Hello"),
                It.Is<int>(timeout => timeout == 15),
                It.IsAny<CancellationToken>()),
            Times.Once);

        resendSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailProviderMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendEmailAsync_Throws_WhenSmtpEnabledButConfigMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["EmailSettings:UseSmtp"] = "true",
            ["EmailSettings:FromAddress"] = "noreply@example.com"
        });

        var templateService = new Mock<IEmailTemplateService>();
        templateService
            .Setup(service => service.RenderTransactionalEmail("Subject", "Hello"))
            .Returns("<html><body>Hello</body></html>");

        var resendSender = new Mock<IResendEmailSender>();
        var sesSender = new Mock<IAmazonSesEmailSender>();

        var sut = new EmailService(
            config,
            templateService.Object,
            resendSender.Object,
            sesSender.Object,
            NullLogger<EmailService>.Instance);

        var act = () => sut.SendEmailAsync("dest@example.com", "Subject", "Hello");

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*SMTP está ativo*EmailSettings:Host*EmailSettings:Username*EmailSettings:Password*");

        resendSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailProviderMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        sesSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailProviderMessage>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendEmailSender_SendsDocumentedHttpPayload()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"email_123\"}", Encoding.UTF8, "application/json")
        }));

        var httpClient = new HttpClient(handler);
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["EmailSettings:Resend:ApiKey"] = "re_test_key",
            ["EmailSettings:Resend:BaseUrl"] = "https://resend.test"
        });

        var sut = new ResendEmailSender(httpClient, config, NullLogger<ResendEmailSender>.Instance);

        await sut.SendAsync(
            new EmailProviderMessage(
                "dest@example.com",
                "Subject",
                "<p>Hello</p>",
                "Hello",
                "noreply@example.com",
                "TrustRent",
                "reply@example.com"),
            sendTimeoutSeconds: 15);

        handler.LastRequestUri.Should().Be(new Uri("https://resend.test/emails"));
        handler.LastAuthorizationScheme.Should().Be("Bearer");
        handler.LastAuthorizationParameter.Should().Be("re_test_key");
        handler.LastUserAgent.Should().Contain("TrustRent.Backend/EmailService");

        using var payload = JsonDocument.Parse(handler.LastContent!);
        payload.RootElement.GetProperty("from").GetString().Should().Be("TrustRent <noreply@example.com>");
        payload.RootElement.GetProperty("subject").GetString().Should().Be("Subject");
        payload.RootElement.GetProperty("html").GetString().Should().Be("<p>Hello</p>");
        payload.RootElement.GetProperty("text").GetString().Should().Be("Hello");
        payload.RootElement.GetProperty("reply_to").GetString().Should().Be("reply@example.com");
        payload.RootElement.GetProperty("to")[0].GetString().Should().Be("dest@example.com");
    }

    [Fact]
    public async Task ResendEmailSender_ThrowsHelpfulMessage_WhenApiRejectsRequest()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"name\":\"validation_error\",\"message\":\"domain not verified\"}", Encoding.UTF8, "application/json")
        }));

        var httpClient = new HttpClient(handler);
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["EmailSettings:Resend:ApiKey"] = "re_test_key"
        });

        var sut = new ResendEmailSender(httpClient, config, NullLogger<ResendEmailSender>.Instance);

        var act = () => sut.SendAsync(
            new EmailProviderMessage(
                "dest@example.com",
                "Subject",
                "<p>Hello</p>",
                "Hello",
                "noreply@example.com",
                "TrustRent",
                null),
            sendTimeoutSeconds: 15);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resend rejeitou o pedido*");
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Uri? LastRequestUri { get; private set; }
        public string? LastAuthorizationScheme { get; private set; }
        public string? LastAuthorizationParameter { get; private set; }
        public string? LastUserAgent { get; private set; }
        public string? LastContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
            LastAuthorizationParameter = request.Headers.Authorization?.Parameter;
            LastUserAgent = request.Headers.UserAgent.ToString();
            LastContent = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return await _responseFactory(request, cancellationToken);
        }
    }
}