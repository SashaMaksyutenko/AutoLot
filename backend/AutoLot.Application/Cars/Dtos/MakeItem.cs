namespace AutoLot.Application.Cars.Dtos;

public sealed record MakeItem(long Id, string Name, string Slug, bool IsPopular, int ModelCount);
