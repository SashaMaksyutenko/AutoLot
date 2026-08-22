namespace AutoLot.Application.Cars.Dtos;

public sealed record GenerationItem(long Id, string Name, string Slug, int YearFrom, int? YearTo);
