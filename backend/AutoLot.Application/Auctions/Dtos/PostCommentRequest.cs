namespace AutoLot.Application.Auctions.Dtos;

/// <summary>Новий коментар під лотом — лише текст; хто й коли, вирішує сервер.</summary>
public sealed record PostCommentRequest(string Text);
