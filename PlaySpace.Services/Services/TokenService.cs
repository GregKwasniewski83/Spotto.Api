using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PlaySpace.Services.Interfaces;
using PlaySpace.Domain.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace PlaySpace.Services.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user)
        {
            try
            {
                var claims = new[]
                {
                            new Claim("id", user.Id.ToString()),
                            new Claim(JwtRegisteredClaimNames.Name, user.LastName),
                            new Claim(JwtRegisteredClaimNames.Email, user.Email),                           
                           
                        };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: "PlaySpace_issuer",
                    audience: "PlaySpace_audience",
                    claims: claims,
                    expires: DateTime.Now.AddDays(100),
                    signingCredentials: creds);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

}
