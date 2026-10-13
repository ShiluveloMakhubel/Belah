using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Belah_Backend.Tests;

public sealed class AuthorizationPipelineTests
{
    [Fact]
    public async Task Admin_Order_Policy_Allows_Admin_And_Denies_Customers_And_Anonymous_Requests()
    {
        Assert.Equal(StatusCodes.Status204NoContent, await InvokePolicy("Admin"));
        Assert.Equal(StatusCodes.Status403Forbidden, await InvokePolicy("Customer"));
        Assert.Equal(StatusCodes.Status401Unauthorized, await InvokePolicy(null));
    }

    private static async Task<int> InvokePolicy(string? role)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddAuthentication("HeaderTest")
            .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>("HeaderTest", _ => { });
        services.AddAuthorization();
        using var provider = services.BuildServiceProvider();

        var builder = new ApplicationBuilder(provider);
        builder.UseAuthentication();
        builder.UseAuthorization();
        builder.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var pipeline = builder.Build();

        var context = new DefaultHttpContext { RequestServices = provider };
        if (role != null) context.Request.Headers["X-Test-Role"] = role;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute { Roles = "Admin" }),
            "Admin-only test endpoint"));

        await pipeline(context);
        return context.Response.StatusCode;
    }

    private sealed class HeaderRoleAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderRoleAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "pipeline-test"),
                new Claim(ClaimTypes.Role, role)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
