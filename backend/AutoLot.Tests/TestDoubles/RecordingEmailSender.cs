using AutoLot.Application.Common.Abstractions;
using AutoLot.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace AutoLot.Tests.TestDoubles;

/// <summary>
/// Відправник, який нікуди нічого не шле, а лише запам'ятовує листи. Так тест
/// може перевірити, ЩО саме пішло б людині, не піднімаючи ані SMTP, ані
/// файлової теки.
/// </summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> messages = [];

    public IReadOnlyList<EmailMessage> Messages => messages;

    public EmailMessage? Last => messages.Count > 0 ? messages[^1] : null;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // Блокування, бо в тесті конкурентності сюди пишуть із півсотні
        // потоків одночасно, а List такого не терпить.
        lock (messages)
        {
            messages.Add(message);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Відправник, який завжди падає — щоб довести, що ставка від цього не зникає.</summary>
internal sealed class FailingEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Поштовий сервер недоступний.");
    }
}

/// <summary>Тексти листів із передбачуваними налаштуваннями.</summary>
internal static class TestEmails
{
    public static AccountEmails Create() => new(Options.Create(new EmailOptions
    {
        FromAddress = "no-reply@autolot.test",
        SiteUrl = "https://autolot.test",
    }));
}
