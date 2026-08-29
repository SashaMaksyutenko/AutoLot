using AutoLot.Tests.TestDoubles;

namespace AutoLot.Tests.Email;

/// <summary>
/// Тексти й посилання в листах. Перевіряються без бази й без пошти — це
/// чисте складання рядків, і найцінніше тут те, що легко зіпсувати:
/// кодування токена в адресі.
/// </summary>
public class AccountEmailsTests
{
    private const string Recipient = "person@example.com";

    [Fact]
    public void The_reset_letter_carries_a_working_link()
    {
        var letter = TestEmails.Create().PasswordReset(Recipient, "simple-token");

        Assert.Equal(Recipient, letter.To);
        Assert.Contains("https://autolot.test/reset-password?", letter.TextBody, StringComparison.Ordinal);
        Assert.Contains("token=simple-token", letter.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_token_with_awkward_characters_is_encoded()
    {
        // Identity видає токени у форматі Base64, де трапляються «+» та «/».
        // В адресі «+» означає пробіл, тож без кодування половина посилань
        // просто не спрацювала б — і людина бачила б «посилання недійсне».
        var letter = TestEmails.Create().PasswordReset(Recipient, "aa+bb/cc=dd");

        Assert.Contains("token=aa%2Bbb%2Fcc%3Ddd", letter.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("token=aa+bb", letter.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void The_address_is_encoded_too()
    {
        var letter = TestEmails.Create().PasswordReset("person+tag@example.com", "token");

        Assert.Contains("email=person%2Btag%40example.com", letter.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_letter_has_both_bodies()
    {
        var emails = TestEmails.Create();

        // Лист лише з HTML частина фільтрів вважає за спам, а частина людей
        // читає пошту без розмітки взагалі.
        foreach (var letter in new[]
        {
            emails.PasswordReset(Recipient, "t"),
            emails.EmailConfirmation(Recipient, "t"),
            emails.Outbid(Recipient, "BMW X5", "10 000 Usd", 42),
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(letter.HtmlBody));
            Assert.False(string.IsNullOrWhiteSpace(letter.TextBody));
            Assert.False(string.IsNullOrWhiteSpace(letter.Subject));
        }
    }

    [Fact]
    public void The_outbid_letter_names_the_car_and_leads_to_the_lot()
    {
        var letter = TestEmails.Create().Outbid(Recipient, "BMW X5", "10 000 Usd", 42);

        Assert.Contains("BMW X5", letter.Subject, StringComparison.Ordinal);
        Assert.Contains("10 000 Usd", letter.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://autolot.test/listing/42", letter.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void A_car_name_cannot_smuggle_markup_into_the_letter()
    {
        // Назву лота пише продавець. Без екранування «<script>» потрапив би
        // у HTML листа як розмітка, а не як текст.
        var letter = TestEmails.Create().Outbid(Recipient, "<script>alert(1)</script>", "1", 1);

        Assert.DoesNotContain("<script>", letter.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", letter.HtmlBody, StringComparison.Ordinal);
    }
}
