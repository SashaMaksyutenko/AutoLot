using System.Reflection;
using AutoLot.Application.Common.Localization;
using AutoLot.Domain.Common;

namespace AutoLot.Tests.Localization;

/// <summary>
/// Повнота словника повідомлень.
///
/// Це той самий захист, що й у фронтенді, тільки іншим способом: там забутий
/// переклад ловить система типів, тут — тест. Причина одна: помилка валідації
/// без перекладу непомітна доти, доки хтось не побачить на екрані сирий код
/// на кшталт «listing.title.required» замість тексту.
/// </summary>
public class MessageCatalogTests
{
    /// <summary>Усі константи з MessageCodes — саме те, що мають знати словники.</summary>
    private static IEnumerable<string> DeclaredCodes =>
        typeof(MessageCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(member => member is { IsLiteral: true, IsInitOnly: false })
            .Select(member => (string)member.GetRawConstantValue()!);

    [Fact]
    public void Every_code_has_a_ukrainian_text()
    {
        var missing = DeclaredCodes
            .Where(code => MessageCatalog.Translate(code, LanguageCodes.Ukrainian) == code)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_code_has_an_english_text()
    {
        var missing = DeclaredCodes
            .Where(code => MessageCatalog.Translate(code, LanguageCodes.English) == code)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void The_two_languages_differ()
    {
        // Зовсім однаковий текст обома мовами майже напевно означає, що
        // англійську забули й скопіювали українську.
        var identical = DeclaredCodes
            .Where(code =>
                MessageCatalog.Translate(code, LanguageCodes.Ukrainian)
                == MessageCatalog.Translate(code, LanguageCodes.English))
            .ToArray();

        Assert.Empty(identical);
    }

    [Fact]
    public void The_catalog_has_nothing_beyond_the_declared_codes()
    {
        // Зайвий запис — це слід від видаленого правила. Сам він нікому не
        // шкодить, але з часом словник заростає мертвими рядками.
        Assert.Empty(MessageCatalog.Codes.Except(DeclaredCodes));
    }

    [Theory]
    [InlineData("uk")]
    [InlineData("en")]
    [InlineData("de")]
    public void An_unknown_string_is_returned_untouched(string language)
    {
        // Так поводиться повідомлення, яке ще не перевели на коди: воно
        // показується як є, замість зникнути або стати порожнім рядком.
        const string Raw = "Текст, якого немає у словнику.";

        Assert.Equal(Raw, MessageCatalog.Translate(Raw, language));
    }

    [Fact]
    public void An_unsupported_language_falls_back_to_ukrainian()
    {
        Assert.Equal(
            MessageCatalog.Translate(MessageCodes.AuthEmailRequired, LanguageCodes.Ukrainian),
            MessageCatalog.Translate(MessageCodes.AuthEmailRequired, "de"));
    }
}
