namespace AutoLot.Domain.Common;

/// <summary>
/// Назва значення перелічення однією мовою. Одна таблиця на всі перелічення
/// відразу: рядок «BodyType / Sedan / uk / Седан» описує, як показати кузов
/// седан українською.
///
/// Чому не окрема таблиця-довідник на кожен тип: кузовів, палив і приводів
/// разом менше півсотні, вони не змінюються роками, і жоден із них не має
/// власних полів, окрім назви. П'ять майже однакових пар таблиць коштували б
/// дорожче, ніж дають.
/// </summary>
public class EnumTranslation : Translation
{
    /// <summary>Ім'я типу перелічення, наприклад «BodyType».</summary>
    public string EnumName { get; set; } = string.Empty;

    /// <summary>Назва значення в коді, наприклад «Sedan».</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Порядок у випадаючому списку: спершу найчастіші варіанти.</summary>
    public int SortOrder { get; set; }
}
