using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}
