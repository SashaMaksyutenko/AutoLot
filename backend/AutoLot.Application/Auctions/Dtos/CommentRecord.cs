namespace AutoLot.Application.Auctions.Dtos;

/// <summary>
/// Коментар під лотом так, як його бачать усі.
/// </summary>
/// <param name="ListingId">
/// Потрібен у живій розсилці: браузер сам перевіряє, що новина про той лот,
/// який відкритий, — як і з оновленнями ставок.
/// </param>
/// <param name="IsSeller">
/// Написав продавець або хтось із його салону. Такі коментарі позначаються
/// окремо: відповідь продавця під лотом важить більше за чужу думку, і
/// загубитися серед інших вона не має.
/// </param>
public sealed record CommentRecord(
    long Id,
    long ListingId,
    string AuthorName,
    bool IsSeller,
    string Text,
    DateTimeOffset CreatedAt);
