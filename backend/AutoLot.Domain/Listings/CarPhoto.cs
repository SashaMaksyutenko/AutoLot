using AutoLot.Domain.Common;

namespace AutoLot.Domain.Listings;

/// <summary>
/// Фото авто. У базі лежить лише шлях до файла — самі зображення живуть
/// у сховищі, бо бінарні дані в таблиці роздувають її та сповільнюють запити.
/// </summary>
public sealed class CarPhoto : Entity
{
    public long CarId { get; set; }

    public Car Car { get; set; } = null!;

    /// <summary>Шлях до повнорозмірного зображення відносно кореня сховища.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Зменшена копія для списків — щоб видача не тягнула мегабайти.</summary>
    public string ThumbnailPath { get; set; } = string.Empty;

    /// <summary>Порядок у галереї; перше фото не обов'язково головне.</summary>
    public int SortOrder { get; set; }

    /// <summary>Головне фото — те, що показується в списку оголошень.</summary>
    public bool IsPrimary { get; set; }
}
