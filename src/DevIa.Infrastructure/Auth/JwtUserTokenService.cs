using System.Security.Claims;
using System.Text;
using DevIa.Application.Identity;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevIa.Infrastructure.Auth;

/// <summary>Issues an HS256 JWT carrying the user id (sub), login, and role claims.</summary>
public sealed class JwtUserTokenService(IOptions<AuthOptions> options) : IUserTokenService
{
    private readonly AuthOptions _options = options.Value;

    public string CreateToken(User user)
    {
        var role = _options.ReviewerLogins.Contains(user.Login, StringComparer.OrdinalIgnoreCase)
            ? MembershipRole.Reviewer
            : MembershipRole.Developer;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("login", user.Login),
                new Claim("role", role.ToString())
            ])
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
