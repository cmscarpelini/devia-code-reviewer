using DevIa.Domain.Identity;

namespace DevIa.Application.Identity;

/// <summary>Issues a signed access token (JWT) for an authenticated user, including their role.</summary>
public interface IUserTokenService
{
    string CreateToken(User user);
}
