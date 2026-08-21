using System.Security.Claims;
using System.Text;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AutoLot.Infrastructure.Identity;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, IDateTimeProvider clock)
{
    private readonly JwtOptions options = options.Value;
    private readonly JsonWebTokenHandler handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Create(User user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AutoLotClaims.Subject, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(AutoLotClaims.Email, user.Email ?? string.Empty),
            new(AutoLotClaims.Name, user.DisplayName),
            new(AutoLotClaims.AccountType, user.AccountType.ToString()),
            new(AutoLotClaims.TokenId, Guid.NewGuid().ToString("N")),
        };

        claims.AddRange(roles.Select(role => new Claim(AutoLotClaims.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (handler.CreateToken(descriptor), expiresAt);
    }
}
