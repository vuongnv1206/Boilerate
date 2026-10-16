using Boilerate.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Boilerate.Shared.Authorization;

namespace Boilerate.Infrastructure.Auth.Jwt;

/// <summary>
/// Configure JWT Bearer authentication options
/// </summary>
public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _jwtSettings;
    private readonly SecuritySettings _security;

    public ConfigureJwtBearerOptions(IOptions<JwtSettings> jwtSettings, IOptions<SecuritySettings> security)
    {
        _jwtSettings = jwtSettings.Value;
        _security = security.Value;
    }

    public void Configure(JwtBearerOptions options)
    {
        Configure(string.Empty, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        byte[] key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

        options.RequireHttpsMetadata = true;
        options.SaveToken = true; // Save token trong AuthenticationProperties
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer, // Không validate issuer
            ValidateLifetime = true, // Validate token expiration
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience, // Không validate audience
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero // Không có clock skew tolerance (strict expiration)
        };

        // Custom events
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.GetUserId();
                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessionId))
                { context.Fail("Session is missing; sign in again."); return; }
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                if (!await db.UserSessions.AsNoTracking().AnyAsync(x => x.Id == sessionId && x.UserId == userId &&
                        x.RevokedOn == null && x.ExpiresOn > now && x.User.IsActive && (!_security.RequireConfirmedAccount || x.User.EmailConfirmed) &&
                        (!x.User.LockoutEnabled || x.User.LockoutEnd == null || x.User.LockoutEnd <= DateTimeOffset.UtcNow), context.HttpContext.RequestAborted))
                    context.Fail("Session is revoked, expired or unavailable.");
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    throw new UnauthorizedException("Authentication Failed.");
                }
                return Task.CompletedTask;
            },

            OnForbidden = _ => throw new ForbiddenException(
                "You are not authorized to access this resource."),

            OnMessageReceived = context =>
            {
                // Support SignalR authentication từ query string
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    }
}
