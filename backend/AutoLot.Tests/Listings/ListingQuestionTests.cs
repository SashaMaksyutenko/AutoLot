using AutoLot.Domain.Common;
using AutoLot.Domain.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Правила самої відповіді живуть у сутності, тож перевіряються без бази —
/// достатньо об'єкта в пам'яті.
/// </summary>
public class ListingQuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_fresh_question_has_no_answer()
    {
        var question = new ListingQuestion { Text = "Чи бита машина?" };

        Assert.False(question.IsAnswered);
        Assert.Null(question.AnsweredAt);
    }

    [Fact]
    public void Replying_records_the_text_and_the_time()
    {
        var question = new ListingQuestion { Text = "Чи бита машина?" };

        question.Reply("Ні, лише бампер фарбували.", Now);

        Assert.True(question.IsAnswered);
        Assert.Equal("Ні, лише бампер фарбували.", question.Answer);
        Assert.Equal(Now, question.AnsweredAt);
    }

    [Fact]
    public void Replying_trims_the_text()
    {
        var question = new ListingQuestion { Text = "Пробіг рідний?" };

        question.Reply("   Так, є сервісна книжка.  ", Now);

        Assert.Equal("Так, є сервісна книжка.", question.Answer);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_reply_is_refused(string text)
    {
        var question = new ListingQuestion { Text = "Скільки власників?" };

        // Порожня відповідь гірша за жодну: питання виглядало б розв'язаним.
        Assert.Throws<DomainRuleException>(() => question.Reply(text, Now));
    }

    [Fact]
    public void A_reply_can_be_corrected()
    {
        var question = new ListingQuestion { Text = "Який рік?" };

        question.Reply("2018", Now);
        question.Reply("Перепрошую, 2019", Now.AddMinutes(2));

        // Виправити щойно написане має бути можна — надто коли йдеться про
        // характеристики авто.
        Assert.Equal("Перепрошую, 2019", question.Answer);
        Assert.Equal(Now.AddMinutes(2), question.AnsweredAt);
    }
}
