namespace BS2.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, string? plainTextBody, CancellationToken ct = default);
}
