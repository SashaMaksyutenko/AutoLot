namespace AutoLot.Application.Cars.Dtos;

/// <summary>
/// Опції одного розділу — «Салон», «Безпека» тощо. Форма показує їх групами,
/// тож і віддаємо одразу згрупованими, щоб клієнт не робив цього сам.
/// </summary>
public sealed record FeatureGroup(string Category, IReadOnlyList<FeatureItem> Features);

public sealed record FeatureItem(long Id, string Code, string Name);
