namespace AutoLot.Application.Listings.Dtos;

/// <summary>Причина обов'язкова: автор має розуміти, що саме виправляти.</summary>
public sealed record RejectListingRequest(string Reason);
