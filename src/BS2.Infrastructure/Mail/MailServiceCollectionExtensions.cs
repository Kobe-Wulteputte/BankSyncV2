using System.Net;
using System.Net.Mail;
using BS2.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BS2.Infrastructure.Mail;

public static class MailServiceCollectionExtensions
{
    /// <summary>SMTP via FluentEmail from the <c>Mail</c> section; a logging no-op when <c>SmtpHost</c> is empty.</summary>
    public static IServiceCollection AddMail(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(MailOptions.Section).Get<MailOptions>() ?? new MailOptions();

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            services.AddSingleton<IEmailSender, NullEmailSender>();
            return services;
        }

        services
            .AddFluentEmail(options.From, "BankSync")
            .AddSmtpSender(() => new SmtpClient(options.SmtpHost, options.SmtpPort)
            {
                EnableSsl = true,
                Credentials = string.IsNullOrEmpty(options.SmtpUser) ? null : new NetworkCredential(options.SmtpUser, options.SmtpPassword)
            });
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
