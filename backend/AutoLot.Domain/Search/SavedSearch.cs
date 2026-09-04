using AutoLot.Domain.Common;
using AutoLot.Domain.Identity;

namespace AutoLot.Domain.Search;

/// <summary>
/// Збережений пошук: набір фільтрів каталогу під власною назвою.
///
/// Потрібен тому, що покупець шукає авто тижнями й щоразу відтворює той
/// самий десяток фільтрів руками. А ще це підготовка до сповіщень про нові
/// збіги (пункт 15) і до чатботів (пункт 14), яким за спекою треба вміти
/// «шукати за збереженими фільтрами».
/// </summary>
public sealed class SavedSearch : Entity
{
    /// <summary>Скільки збережених пошуків можна мати.</summary>
    /// <remarks>
    /// Межа не від жадібності: кожен збережений пошук згодом стане ще й
    /// підпискою на сповіщення, тобто регулярним запитом до бази. Двадцять
    /// — це стільки, скільки людина здатна осмислено переглядати.
    /// </remarks>
    public const int PerUserLimit = 20;

    public const int MaxNameLength = 60;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Назва, яку дала людина: «Дизельні універсали до 8000».</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Фільтри у вигляді JSON.
    /// </summary>
    /// <remarks>
    /// Один текстовий стовпець замість тридцяти п'яти колонок — по одній на
    /// кожен фільтр каталогу. Колонки довелося б додавати міграцією щоразу,
    /// коли в пошуку з'являється новий параметр, і жодного запиту «знайди
    /// збережені пошуки з таким-то кузовом» проєкт не робить. Натомість
    /// збережене читається цілком і виконується як звичайний запит каталогу.
    ///
    /// Плата за це чесна: база не перевіряє вміст. Тому розбір JSON завжди
    /// має бути захищеним — зіпсований рядок не повинен ламати весь список.
    /// </remarks>
    public string QueryJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Коли востаннє змінювали фільтри або назву.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Перейменовує. Порожня назва заборонена: список без назв — це список
    /// однакових рядків, з якого нічого не вибереш.
    /// </summary>
    public void Rename(string name, DateTimeOffset now)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainRuleException("Назва пошуку не може бути порожньою.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainRuleException($"Назва задовга — до {MaxNameLength} символів.");
        }

        Name = trimmed;
        UpdatedAt = now;
    }
}
