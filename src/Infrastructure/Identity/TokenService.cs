using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Auth.Jwt;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Boilerate.Infrastructure.Identity;
internal sealed class TokenService : ITokenService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly JwtSettings _jwt;
    private readonly SecuritySettings _security;
    private readonly IServiceProvider _services;
    public TokenService(ApplicationDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IOptions<JwtSettings> jwt, IOptions<SecuritySettings> security, IServiceProvider services)
        => (_db, _users, _signIn, _jwt, _security, _services) = (db, users, signIn, jwt.Value, security.Value, services);
    private void Eligible(ApplicationUser user)
    {
        if (!user.IsActive || (_security.RequireConfirmedAccount && !user.EmailConfirmed))
            throw new UnauthorizedException("Account is unavailable or unconfirmed.");
    }
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<TokenResponse> GetTokenAsync(TokenRequest request, string ipAddress, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_services, request, cancellationToken);
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null) throw new UnauthorizedException("Authentication failed.");
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(user.Id, cancellationToken);
        await _db.Entry(user).ReloadAsync(cancellationToken);
        Eligible(user);
        if (!(await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
        {
            // Identity persists failed-attempt/lockout counters even when sign-in is rejected.
            await tx.CommitAsync(cancellationToken);
            throw new UnauthorizedException("Authentication failed.");
        }
        var (session, refresh) = NewSession(user.Id, Guid.NewGuid(), ipAddress);
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return await ResponseAsync(user, session, refresh);
    }
    public async Task<TokenResponse> GenerateTokensAndUpdateUser(ApplicationUser user, string ipAddress)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        await LockUserAsync(user.Id, default);
        await _db.Entry(user).ReloadAsync();
        Eligible(user);
        if (await _users.IsLockedOutAsync(user)) throw new UnauthorizedException("Account is unavailable.");
        var (session, refresh) = NewSession(user.Id, Guid.NewGuid(), ipAddress);
        _db.UserSessions.Add(session); await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ResponseAsync(user, session, refresh);
    }
    private (UserSession, string) NewSession(string userId, Guid family, string ip)
    {
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (new UserSession { UserId = userId, FamilyId = family, RefreshTokenHash = Hash(refresh),
            ExpiresOn = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationInDays), IpAddress = ip.Length > 64 ? ip[..64] : ip }, refresh);
    }
    private async Task<TokenResponse> ResponseAsync(ApplicationUser user, UserSession session, string refresh)
    {
        var expires = DateTime.UtcNow.AddMinutes(_jwt.TokenExpirationInMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id), new(JwtRegisteredClaimNames.Sub, user.Id),
            new("sid", session.Id.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Email, user.Email ?? ""), new(ClaimTypes.Name, user.UserName ?? ""),
            new(AppClaims.Fullname, $"{user.FirstName} {user.LastName}")
        };
        foreach (var role in await _users.GetRolesAsync(user)) claims.Add(new(ClaimTypes.Role, role));
        var jwt = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, DateTime.UtcNow, expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, session.ExpiresOn, session.Id, expires)
        {
            UserId = user.Id
        };
    }
    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
    {
        await RequestValidation.ValidateAsync(_services, request);
        var hash = Hash(request.RefreshToken);
        await using var tx = await _db.Database.BeginTransactionAsync();
        var old = await _db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.RefreshTokenHash == hash)
            ?? throw new UnauthorizedException("Invalid refresh token.");
        await LockUserAsync(old.UserId, default);
        var user = await _users.FindByIdAsync(old.UserId) ?? throw new UnauthorizedException("Invalid refresh token.");
        await _db.Entry(user).ReloadAsync();
        Eligible(user);
        if (await _users.IsLockedOutAsync(user)) throw new UnauthorizedException("Account is unavailable.");
        var now = DateTime.UtcNow;
        var consumed = await _db.UserSessions.Where(x => x.Id == old.Id && x.ConsumedOn == null && x.RevokedOn == null && x.ExpiresOn > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedOn, now));
        if (consumed != 1)
        {
            // Commit revocation before rejecting: throwing inside an uncommitted transaction would undo it.
            await _db.UserSessions.Where(x => x.FamilyId == old.FamilyId && x.RevokedOn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, now));
            await tx.CommitAsync(); throw new UnauthorizedException("Refresh token expired, revoked or reused.");
        }
        var (next, refresh) = NewSession(user.Id, old.FamilyId, ipAddress);
        _db.UserSessions.Add(next); await _db.SaveChangesAsync();
        await _db.UserSessions.Where(x => x.Id == old.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplacedById, next.Id));
        await tx.CommitAsync(); return await ResponseAsync(user, next, refresh);
    }
    public Task<List<SessionDto>> GetSessionsAsync(string userId, CancellationToken ct) => _db.UserSessions.AsNoTracking()
        .Where(x => x.UserId == userId && x.RevokedOn == null && x.ReplacedById == null && x.ExpiresOn > DateTime.UtcNow)
        .OrderByDescending(x => x.CreatedOn).Select(x => new SessionDto(x.Id, x.CreatedOn, x.ExpiresOn, x.IpAddress)).ToListAsync(ct);
    public async Task RevokeSessionAsync(string userId, Guid sessionId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(userId, ct);
        var family = await _db.UserSessions.Where(x => x.Id == sessionId && x.UserId == userId).Select(x => (Guid?)x.FamilyId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Session not found.");
        await _db.UserSessions.Where(x => x.UserId == userId && x.FamilyId == family && x.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);
    }
    public async Task RevokeAllAsync(string userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(userId, ct);
        await _db.UserSessions.Where(x => x.UserId == userId && x.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);
    }
    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 256) return;
        var hash = Hash(refreshToken);
        var session = await _db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.RefreshTokenHash == hash, ct);
        if (session is null) return;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(session.UserId, ct);
        await _db.UserSessions.Where(x => x.UserId == session.UserId && x.FamilyId == session.FamilyId && x.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);
    }
    private async Task LockUserAsync(string id, CancellationToken ct)
    {
        if (await _db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ConcurrencyStamp, x => x.ConcurrencyStamp), ct) != 1)
            throw new UnauthorizedException("Account is unavailable.");
    }
}
