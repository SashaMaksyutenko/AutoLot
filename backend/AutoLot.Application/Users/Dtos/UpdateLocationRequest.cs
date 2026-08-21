namespace AutoLot.Application.Users.Dtos;

/// <summary>
/// Порожній CityId означає «прибрати місцезнаходження». Область і район
/// області клієнт не надсилає — вони однозначно випливають із міста.
/// </summary>
public sealed record UpdateLocationRequest(long? CityId, long? CityDistrictId);
