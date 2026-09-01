using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Правила самої скарги — без бази. Тут перевіряється те, що має лишатися
/// правдою незалежно від сховища: рішення виносять один раз.
/// </summary>
public class ListingReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_report_waits_for_review()
    {
        var report = new ListingReport();

        Assert.True(report.IsPending);
        Assert.Equal(ListingReportStatus.Pending, report.Status);
        Assert.Null(report.ReviewedAt);
    }

    [Fact]
    public void Accepting_records_who_decided_and_when()
    {
        var report = new ListingReport();

        report.Resolve(accepted: true, moderatorId: 7, Now, "Ціна з іншої планети");

        Assert.Equal(ListingReportStatus.Accepted, report.Status);
        Assert.Equal(7, report.ReviewedById);
        Assert.Equal(Now, report.ReviewedAt);
        Assert.Equal("Ціна з іншої планети", report.ReviewNote);
        Assert.False(report.IsPending);
    }

    [Fact]
    public void Dismissing_is_a_decision_too()
    {
        var report = new ListingReport();

        report.Resolve(accepted: false, moderatorId: 7, Now, note: null);

        Assert.Equal(ListingReportStatus.Dismissed, report.Status);
        Assert.False(report.IsPending);
    }

    [Fact]
    public void A_blank_note_is_stored_as_nothing()
    {
        var report = new ListingReport();

        report.Resolve(accepted: false, moderatorId: 7, Now, "   ");

        // Інакше в базі лежали б рядки з самих пробілів, і перевірка
        // «нотатка є» стала б брехливою.
        Assert.Null(report.ReviewNote);
    }

    [Fact]
    public void A_report_is_reviewed_only_once()
    {
        var report = new ListingReport();

        report.Resolve(accepted: true, moderatorId: 7, Now, note: null);

        // Перерозгляд мовчки розійшовся б із тим, що вже сталося:
        // оголошення знято, а скарга раптом «відхилена».
        var repeated = Assert.Throws<DomainRuleException>(
            () => report.Resolve(accepted: false, moderatorId: 8, Now.AddHours(1), note: null));

        Assert.Contains("вже розглянуто", repeated.Message, StringComparison.Ordinal);
        Assert.Equal(7, report.ReviewedById);
    }
}

/// <summary>Зняття з публікації — новий перехід у життєвому циклі оголошення.</summary>
public class ListingTakeDownTests
{
    [Fact]
    public void A_published_listing_can_be_taken_down()
    {
        var listing = new Listing { Status = ListingStatus.Active };

        listing.TakeDown("Знято за скаргою: Схоже на шахрайство.");

        // Саме Rejected, а не Archived: це єдиний стан, з якого автор може
        // виправити оголошення й подати знову.
        Assert.Equal(ListingStatus.Rejected, listing.Status);
        Assert.Equal("Знято за скаргою: Схоже на шахрайство.", listing.RejectionReason);
        Assert.True(listing.IsEditable);
    }

    [Fact]
    public void A_sold_listing_can_be_taken_down_too()
    {
        // Продане лишається на видноті, отже може й шкодити.
        var listing = new Listing { Status = ListingStatus.Sold };

        listing.TakeDown("Причина");

        Assert.Equal(ListingStatus.Rejected, listing.Status);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.PendingModeration)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Archived)]
    public void What_is_not_published_cannot_be_taken_down(ListingStatus status)
    {
        var listing = new Listing { Status = status };

        Assert.Throws<DomainRuleException>(() => listing.TakeDown("Причина"));
    }
}
