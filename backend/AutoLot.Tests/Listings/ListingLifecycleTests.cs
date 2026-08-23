using AutoLot.Domain.Common;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Переходи між статусами живуть у самій сутності, тож і перевіряються без
/// бази та сервісів — достатньо об'єкта в пам'яті.
/// </summary>
public class ListingLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Rejected)]
    public void Draft_and_rejected_can_be_submitted(ListingStatus status)
    {
        var listing = Listing(status);

        listing.SubmitForModeration();

        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
    }

    [Fact]
    public void Submitting_clears_the_previous_rejection_reason()
    {
        var listing = Listing(ListingStatus.Rejected);
        listing.RejectionReason = "Фото не відповідають авто";

        listing.SubmitForModeration();

        Assert.Null(listing.RejectionReason);
    }

    [Theory]
    [InlineData(ListingStatus.Active)]
    [InlineData(ListingStatus.PendingModeration)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Archived)]
    public void Anything_else_cannot_be_submitted(ListingStatus status)
    {
        var listing = Listing(status);

        Assert.Throws<DomainRuleException>(listing.SubmitForModeration);
    }

    [Fact]
    public void Approving_publishes_and_sets_the_expiry()
    {
        var listing = Listing(ListingStatus.PendingModeration);

        listing.Approve(Now, Lifetime);

        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Equal(Now, listing.PublishedAt);
        Assert.Equal(Now.Add(Lifetime), listing.ExpiresAt);
    }

    [Fact]
    public void Re_approving_keeps_the_original_publication_date()
    {
        // Оголошення вже колись публікували, потім відхилили й виправили.
        var listing = Listing(ListingStatus.PendingModeration);
        var firstPublication = Now.AddDays(-30);
        listing.PublishedAt = firstPublication;

        listing.Approve(Now, Lifetime);

        Assert.Equal(firstPublication, listing.PublishedAt);
        Assert.Equal(Now.Add(Lifetime), listing.ExpiresAt);
    }

    [Fact]
    public void Only_a_pending_listing_can_be_approved()
    {
        var listing = Listing(ListingStatus.Draft);

        Assert.Throws<DomainRuleException>(() => listing.Approve(Now, Lifetime));
    }

    [Fact]
    public void Rejecting_stores_the_reason()
    {
        var listing = Listing(ListingStatus.PendingModeration);

        listing.Reject("Ціна не відповідає опису");

        Assert.Equal(ListingStatus.Rejected, listing.Status);
        Assert.Equal("Ціна не відповідає опису", listing.RejectionReason);
    }

    [Fact]
    public void Only_an_active_listing_can_be_marked_sold()
    {
        Listing(ListingStatus.Active).MarkSold();

        Assert.Throws<DomainRuleException>(Listing(ListingStatus.Draft).MarkSold);
        Assert.Throws<DomainRuleException>(Listing(ListingStatus.PendingModeration).MarkSold);
    }

    [Fact]
    public void A_draft_is_deleted_rather_than_archived()
    {
        Assert.Throws<DomainRuleException>(Listing(ListingStatus.Draft).Archive);
    }

    [Fact]
    public void An_active_listing_can_be_archived_and_restored_as_a_draft()
    {
        var listing = Listing(ListingStatus.Active);

        listing.Archive();
        Assert.Equal(ListingStatus.Archived, listing.Status);

        listing.Restore();
        Assert.Equal(ListingStatus.Draft, listing.Status);
    }

    [Theory]
    [InlineData(ListingStatus.Draft, true)]
    [InlineData(ListingStatus.Rejected, true)]
    [InlineData(ListingStatus.PendingModeration, false)]
    [InlineData(ListingStatus.Active, false)]
    [InlineData(ListingStatus.Sold, false)]
    public void Only_a_draft_or_a_rejected_listing_is_editable(ListingStatus status, bool expected)
    {
        Assert.Equal(expected, Listing(status).IsEditable);
    }

    [Theory]
    [InlineData(ListingStatus.Active, true)]
    [InlineData(ListingStatus.PendingModeration, true)]
    [InlineData(ListingStatus.Draft, false)]
    [InlineData(ListingStatus.Sold, false)]
    [InlineData(ListingStatus.Archived, false)]
    public void Only_visible_listings_count_towards_the_limit(ListingStatus status, bool expected)
    {
        // Чернеток може бути скільки завгодно: ліміт стосується того,
        // що займає місце у видачі.
        Assert.Equal(expected, Listing(status).CountsTowardsLimit);
    }

    private static Listing Listing(ListingStatus status) => new()
    {
        Id = 1,
        SellerId = 42,
        Title = "Volkswagen Passat B7",
        Status = status,
    };
}
