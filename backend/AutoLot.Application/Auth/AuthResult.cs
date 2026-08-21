using AutoLot.Application.Auth.Dtos;

namespace AutoLot.Application.Auth;

/// <summary>
/// Очікувані відмови входу — не виняткові ситуації, тому повертаються значенням.
/// Винятки лишаються для справді несподіваного.
/// </summary>
public sealed class AuthResult
{
    private AuthResult(AuthTokens? tokens, AuthError error, IReadOnlyList<string> messages)
    {
        Tokens = tokens;
        Error = error;
        Messages = messages;
    }

    public AuthTokens? Tokens { get; }

    public AuthError Error { get; }

    public IReadOnlyList<string> Messages { get; }

    public bool Succeeded => Error == AuthError.None;

    public static AuthResult Success(AuthTokens tokens) => new(tokens, AuthError.None, []);

    public static AuthResult Failure(AuthError error, params string[] messages) =>
        new(null, error, messages);
}
