using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;

namespace AutoLot.Tests.Listings;

/// <summary>
/// Стежить за вмістом demo-sellers.json.
///
/// Помилка в цьому файлі виглядає жахливо для того, хто її ловить: застосунок
/// піднімається, а на сіді падає з порушенням унікального індексу — і причина
/// («два салони з однаковим slug») з тексту помилки бази геть не очевидна.
/// Дешевше перевірити файл тут.
/// </summary>
public class DemoSellerSeedDataTests
{
    private const string ResourceName = "AutoLot.Infrastructure.Persistence.SeedData.demo-sellers.json";

    private static readonly DemoSellersDocument Document =
        SeedResource.ReadAsync<DemoSellersDocument>(ResourceName).GetAwaiter().GetResult();

    [Fact]
    public void The_file_is_not_empty()
    {
        Assert.NotEmpty(Document.Sellers);
    }

    [Fact]
    public void Every_seller_has_a_mailbox_and_a_name()
    {
        Assert.All(Document.Sellers, seller =>
        {
            Assert.False(string.IsNullOrWhiteSpace(seller.Email));
            Assert.False(string.IsNullOrWhiteSpace(seller.Name));
        });
    }

    /// <summary>
    /// Пошта — ключ, за яким сідер шукає акаунт. Два однакові префікси означали б,
    /// що другий продавець мовчки не створиться, а його оголошення дістануться
    /// першому.
    /// </summary>
    [Fact]
    public void Mailboxes_do_not_repeat()
    {
        var prefixes = Document.Sellers.Select(seller => seller.Email);

        Assert.Equal(Document.Sellers.Count, prefixes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Slugs_do_not_repeat()
    {
        var slugs = Dealerships().Select(dealership => dealership.Slug).ToList();

        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Slug потрапляє просто в адресу /dealers/&lt;slug&gt;, тож кирилиця, пробіл
    /// чи велика літера там перетворилися б на покручене посилання.
    /// </summary>
    [Fact]
    public void Slugs_are_lowercase_latin()
    {
        Assert.All(Dealerships(), dealership =>
            Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", dealership.Slug));
    }

    [Fact]
    public void Every_dealership_has_a_name()
    {
        Assert.All(Dealerships(), dealership =>
            Assert.False(string.IsNullOrWhiteSpace(dealership.Name)));
    }

    /// <summary>
    /// Сенс демо-даних — показати обидва стани бейджа. Файл, у якому всі салони
    /// перевірені або жоден, цього не показує.
    /// </summary>
    [Fact]
    public void Verified_and_unverified_dealerships_are_both_present()
    {
        var dealerships = Dealerships().ToList();

        Assert.Contains(dealerships, dealership => dealership.IsVerified);
        Assert.Contains(dealerships, dealership => !dealership.IsVerified);
    }

    /// <summary>
    /// Приватні продавці теж мають бути: інакше в каталозі не лишиться жодного
    /// оголошення без бейджа салону, і фільтр «тип продавця» нічого не розділить.
    /// </summary>
    [Fact]
    public void Private_sellers_are_present_too()
    {
        Assert.Contains(Document.Sellers, seller => seller.Dealership is null);
    }

    private static IEnumerable<DemoDealershipRow> Dealerships() =>
        Document.Sellers.Select(seller => seller.Dealership).OfType<DemoDealershipRow>();
}
