using AutoLot.Application.Catalog;
using AutoLot.Application.Catalog.Validation;
using AutoLot.Domain.Enums;

namespace AutoLot.Tests.Catalog;

public class CatalogQueryValidatorTests
{
    private readonly CatalogQueryValidator validator = new();

    [Fact]
    public void Accepts_an_empty_query()
    {
        // Порожній запит — це «показати все», і він має бути допустимим.
        Assert.True(validator.Validate(new CatalogQuery()).IsValid);
    }

    [Fact]
    public void Accepts_a_full_query()
    {
        var query = new CatalogQuery
        {
            Text = "Passat",
            MakeId = 1,
            PriceFrom = 2000,
            PriceTo = 15000,
            YearFrom = 2010,
            YearTo = 2020,
            MileageTo = 200_000,
            BodyTypes = [BodyType.Sedan, BodyType.Universal],
            FuelTypes = [FuelType.Diesel],
            FeatureIds = [3, 26],
            Sort = CatalogSort.PriceAscending,
            Page = 2,
            PageSize = 40,
        };

        Assert.True(validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_page_below_one(int page)
    {
        var result = validator.Validate(new CatalogQuery { Page = page });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CatalogQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(5000)]
    public void Rejects_a_page_size_outside_the_limit(int pageSize)
    {
        // Без верхньої межі один запит витягнув би всю базу.
        var result = validator.Validate(new CatalogQuery { PageSize = pageSize });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CatalogQuery.PageSize));
    }

    [Fact]
    public void Rejects_an_inverted_price_range()
    {
        // Переплутані місцями межі — найчастіша помилка у формі фільтрів.
        // Мовчки віддавати за неї порожній список означало б залишити людину
        // гадати, чому нічого не знайшлося.
        var result = validator.Validate(new CatalogQuery { PriceFrom = 20000, PriceTo = 5000 });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_an_inverted_year_range()
    {
        Assert.False(validator.Validate(new CatalogQuery { YearFrom = 2020, YearTo = 2010 }).IsValid);
    }

    [Fact]
    public void Rejects_an_inverted_mileage_range()
    {
        Assert.False(validator.Validate(new CatalogQuery { MileageFrom = 300_000, MileageTo = 10_000 }).IsValid);
    }

    [Fact]
    public void Accepts_a_half_open_range()
    {
        // «До 5000» без нижньої межі — цілком звичайний фільтр.
        Assert.True(validator.Validate(new CatalogQuery { PriceTo = 5000 }).IsValid);
        Assert.True(validator.Validate(new CatalogQuery { YearFrom = 2015 }).IsValid);
    }

    [Fact]
    public void Rejects_a_search_text_that_is_too_long()
    {
        var result = validator.Validate(new CatalogQuery { Text = new string('а', 200) });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CatalogQuery.Text));
    }

    [Fact]
    public void Rejects_a_non_positive_feature_id()
    {
        var result = validator.Validate(new CatalogQuery { FeatureIds = [3, 0] });

        Assert.False(result.IsValid);
    }
}
