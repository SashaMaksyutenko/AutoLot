namespace AutoLot.Application.Seo;

/// <summary>Одна адреса в карті сайту.</summary>
/// <param name="Path">Шлях від кореня, з початковою скісною рискою.</param>
/// <param name="LastModified">Коли сторінка востаннє змінювалася, якщо відомо.</param>
/// <param name="Priority">Вага від 0 до 1: підказка пошуковику, що тут головне.</param>
public sealed record SitemapEntry(string Path, DateTimeOffset? LastModified, double Priority);

/// <summary>
/// Що показувати пошуковикам.
///
/// До карти потрапляє лише те, що бачить сторонній: опубліковані оголошення
/// й вітрини салонів. Чернетки, архів і кабінети — ні, і не через обмеження
/// доступу, а тому, що карта сайту публічна: у ній не має бути навіть натяку
/// на адреси, яких сторонній не відкриє.
/// </summary>
public interface ISitemapSource
{
    Task<IReadOnlyList<SitemapEntry>> GetAsync(CancellationToken cancellationToken = default);
}
