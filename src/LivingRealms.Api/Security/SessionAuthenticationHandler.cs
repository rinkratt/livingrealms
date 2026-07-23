using System.Security.Claims;
using System.Text.Encodings.Web;
using LivingRealms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LivingRealms.Api.Security;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    LivingRealmsDbContext database)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LivingRealmsSession";
    public const string SessionIdClaim = "living_realms_session";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length is < 32 or > 128)
        {
            return AuthenticateResult.Fail("The bearer token is invalid.");
        }

        var tokenHash = SessionToken.Hash(token);
        var now = DateTimeOffset.UtcNow;
        var session = await database.PlayerSessions
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, Context.RequestAborted);

        if (session is null || session.DisconnectedAt is not null || session.ExpiresAt <= now)
        {
            return AuthenticateResult.Fail("The session is invalid or expired.");
        }

        if (session.LastSeenAt is null || now - session.LastSeenAt >= TimeSpan.FromMinutes(1))
        {
            session.LastSeenAt = now;
            session.UpdatedAt = now;
            await database.SaveChangesAsync(Context.RequestAborted);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.AccountId.ToString("D")),
            new(SessionIdClaim, session.Id.ToString("D")),
            new(ClaimTypes.Name, session.Account.Email)
        };
        if (session.Account.IsAdministrator)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
