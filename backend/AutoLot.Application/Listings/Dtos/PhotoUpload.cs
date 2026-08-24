namespace AutoLot.Application.Listings.Dtos;

/// <summary>
/// Завантажуваний файл у вигляді, незалежному від ASP.NET. Прикладний шар не
/// має знати про IFormFile — інакше його не викличеш ні з тесту, ні з фонової
/// задачі.
/// </summary>
public sealed record PhotoUpload(string FileName, long Length, Stream Content);
