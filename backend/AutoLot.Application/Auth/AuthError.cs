namespace AutoLot.Application.Auth;

/// <summary>
/// Причина відмови. Контролер перекладає її у код відповіді, а текст для
/// користувача навмисно не деталізує, чи існує такий email.
/// </summary>
public enum AuthError
{
    None = 0,
    EmailAlreadyUsed,
    InvalidCredentials,
    AccountLockedOut,
    AccountBanned,
    InvalidRefreshToken,
    PasswordRejected,
    ExternalLoginFailed,
}
