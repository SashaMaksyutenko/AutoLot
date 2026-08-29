namespace AutoLot.Application.Common.Abstractions;

/// <summary>
/// Надсилає лист. Оголошено інтерфейсом, бо спосіб доставки — подробиця
/// оточення: у розробці листи лягають файлами на диск, у продакшені йдуть
/// через SMTP, і сценаріям байдуже, що саме з них увімкнене.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Лист. Тіло — у двох виглядах: HTML для поштових програм і простий текст
/// для тих, хто HTML не показує. Обидва обов'язкові: лист лише з HTML
/// частина фільтрів вважає за спам.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);
