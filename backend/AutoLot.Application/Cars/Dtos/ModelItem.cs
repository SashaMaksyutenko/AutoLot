namespace AutoLot.Application.Cars.Dtos;

public sealed record ModelItem(long Id, string Name, string Slug, bool HasGenerations);
