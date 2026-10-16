using Boilerate.Application.Common.Mailing;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Boilerate.Infrastructure.Mailing;

public class SmtpMailService : IMailService
{
    private readonly SMTPEmailSettings _settings;

    public SmtpMailService(IOptions<SMTPEmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(MailRequest request, CancellationToken ct)
    {
        using var email = new MimeMessage();

        email.From.Add(new MailboxAddress(_settings.DisplayName, request.From ?? _settings.From));

        foreach (string address in request.To)
            email.To.Add(MailboxAddress.Parse(address));

        if (!string.IsNullOrEmpty(request.ReplyTo))
            email.ReplyTo.Add(new MailboxAddress(request.ReplyToName, request.ReplyTo));

        if (request.Bcc != null)
        {
            foreach (string address in request.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
                email.Bcc.Add(MailboxAddress.Parse(address.Trim()));
        }

        if (request.Cc != null)
        {
            foreach (string? address in request.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
                email.Cc.Add(MailboxAddress.Parse(address.Trim()));
        }

        if (request.Headers != null)
        {
            foreach (var header in request.Headers)
                email.Headers.Add(header.Key, header.Value);
        }

        var builder = new BodyBuilder();

        email.Sender = new MailboxAddress(request.DisplayName ?? _settings.DisplayName, request.From ?? _settings.From);
        email.Subject = request.Subject;
        builder.HtmlBody = request.Body;

        if (request.AttachmentData != null)
        {
            foreach (var attachment in request.AttachmentData)
                builder.Attachments.Add(attachment.Key, attachment.Value);
        }

        email.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        // Credentials are never sent over opportunistic/plain SMTP.
        SecureSocketOptions security = _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(_settings.SMTPServer, _settings.Port, security, ct);
        await smtp.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await smtp.SendAsync(email, ct);
        await smtp.DisconnectAsync(true, ct);

    }
}
