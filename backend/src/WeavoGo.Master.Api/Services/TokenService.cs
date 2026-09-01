using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Contracts;
using WeavoGo.Master.Api.Infrastructure;

namespace WeavoGo.Master.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "weavogo-item-master";
    public string Audience { get; set; } = "weavogo-item-master";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}

public interface ITokenService
{
    Task<LoginResponse> AuthenticateAsync(LoginRequest request, CancellationToken ct);
}

public sealed class TokenService : ITokenService
{
    private readonly ItemMasterDbContext _db;
    private readonly JwtOptions _options;

    public TokenService(ItemMasterDbContext db, Microsoft.Extensions.Options.IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<LoginResponse> AuthenticateAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserBusinessUnits)
            .FirstOrDefaultAsync(u => u.UserName == request.UserName && u.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ApiException(ErrorCodes.Unauthorized, System.Net.HttpStatusCode.Unauthorized,
                "Invalid user name or password.");

        var roles = user.UserRoles.Where(ur => ur.IsActive).Select(ur => ur.Role.RoleName).ToList();
        if (user.IsSystemAdmin && !roles.Contains(RoleNames.SystemAdministrator))
            roles.Add(RoleNames.SystemAdministrator);

        var units = user.UserBusinessUnits.Where(ub => ub.IsActive).Select(ub => ub.BusinessUnitId).ToList();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypesEx.UserId, user.UserId.ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(units.Select(u => new Claim(ClaimTypesEx.BusinessUnitIds, u.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresInSeconds = _options.ExpiryMinutes * 60,
            UserId = user.UserId,
            FullName = user.FullName,
            Roles = roles,
            BusinessUnitIds = units
        };
    }
}
