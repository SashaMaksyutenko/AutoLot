using System.Net;
using System.Net.Mail;
using AutoLot.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoLot.Infrastructure.Email;

/// <summary>
/// Надсилає листи через SMTP або складає їх файлами на диск — залежно від
/// налаштувань.
///
/// Режим із текою це НЕ заглушка: лист збирається повністю, з обома тілами
/// й заголовками, і лягає файлом .eml, який відкривається звичайною
/// поштовою програмою. Різниця лише в останньому кроці — у мережу воно не
/// йде. Завдяки цьому в розробці видно точний вміст листа, не потрібні ані
/// SMTP-сервер, ані чужа поштова скринька, а код відправки — той самий, що
/// й у продакшені.
/// </summary>
internal sealed partial class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions settings = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var mail = BuildMessage(message);

        if (!string.IsNullOrWhiteSpace(settings.DropFolder))
        {
            await DropToFolderAsync(mail, message, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            // Ні теки, ні сервера — мовчати не можна: людина чекає на лист,
            // якого ніхто не надішле.
            LogNotConfigured(logger, message.To);
            return;
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseStartTls,
            Credentials = string.IsNullOrWhiteSpace(settings.UserName)
                ? null
                : new NetworkCredential(settings.UserName, settings.Password),
        };

        await client.SendMailAsync(mail, cancellationToken);

        LogSent(logger, message.To, message.Subject);
    }

    private MailMessage BuildMessage(EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = message.Subject,

            // Основне тіло — простий текст, HTML додається окремим виглядом.
            // Так лист читається навіть там, де HTML вимкнено.
            Body = message.TextBody,
            IsBodyHtml = false,
        };

        mail.To.Add(message.To);

        mail.AlternateViews.Add(
            AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, "text/html"));

        return mail;
    }

    /// <summary>
    /// Кладе лист файлом. Ім'я містить час і адресу, щоб у теці було видно,
    /// що і кому пішло, без відкривання кожного файла.
    /// </summary>
    private async Task DropToFolderAsync(
        MailMessage mail,
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        var folder = settings.DropFolder!;
        Directory.CreateDirectory(folder);

        var safeAddress = string.Concat(message.To.Split(Path.GetInvalidFileNameChars()));
        var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{safeAddress}.eml";
        var path = Path.Combine(folder, name);

        var content = $"""
            From: {settings.FromName} <{settings.FromAddress}>
            To: {mail.To}
            Subject: {message.Subject}
            Content-Type: text/plain; charset=utf-8

            {message.TextBody}

            ---8<--- HTML ---8<---

            {message.HtmlBody}
            """;

        await File.WriteAllTextAsync(path, content, cancellationToken);

        LogDropped(logger, message.To, path);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Лист до {To} надіслано: {Subject}")]
    private static partial void LogSent(ILogger logger, string to, string subject);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Лист до {To} збережено файлом: {Path}")]
    private static partial void LogDropped(ILogger logger, string to, string path);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Пошта не налаштована — лист до {To} нікуди не пішов. "
            + "Задайте Email:DropFolder для розробки або Email:Host для надсилання.")]
    private static partial void LogNotConfigured(ILogger logger, string to);
}
