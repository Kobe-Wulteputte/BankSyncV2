using BS2.Application.Abstractions;
using FluentEmail.Core;
using Microsoft.Extensions.Logging;

namespace BS2.Infrastructure.Mail;

public sealed class MailOptions
{
    public const string Section = "Mail";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}

public sealed class SmtpEmailSender(IFluentEmailFactory factory, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody, CancellationToken ct = default)
    {
        logger.LogInformation("Sending mail '{Subject}' to {To}.", subject, to);

        var email = factory.Create().To(to).Subject(subject).Body(htmlBody, isHtml: true);
        if (!string.IsNullOrWhiteSpace(plainTextBody)) email = email.PlaintextAlternativeBody(plainTextBody);

        var response = await email.SendAsync(ct);
        if (!response.Successful)
            throw new InvalidOperationException($"Mail to {to} failed: {string.Join("; ", response.ErrorMessages)}");
    }
}

/// <summary>Registered when <c>Mail:SmtpHost</c> is empty: logs instead of sending.</summary>
public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody, CancellationToken ct = default)
    {
        logger.LogWarning("Mail not configured; dropping '{Subject}' to {To}.", subject, to);
        return Task.CompletedTask;
    }
}
