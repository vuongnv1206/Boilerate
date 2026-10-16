using System.Net;
using System.Text;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class IdentityConfirmationRegressionTests
{
    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql")]
    [InlineData("postgresql")]
    public async Task InvalidConfirmationsReturn400AndSuccessfulMessagesDoNotExposeContactDetails(string provider)
    {
        await using var fixture = new FoundationDatabase(provider);
        await fixture.CreateAsync();
        await using var application = await TestHostApplication.StartAsync(fixture, catalog: false);
        using var scope = application.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "confirm-contact",
            Email = "private-confirmation@example.test",
            PhoneNumber = "+84912345678",
            IsActive = true
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        var withoutPhone = new ApplicationUser { UserName = "without-phone", Email = "no-phone@example.test", IsActive = true };
        Assert.True((await users.CreateAsync(withoutPhone)).Succeeded);
        string emailToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        string phoneToken = await users.GenerateChangePhoneNumberTokenAsync(user, user.PhoneNumber);
        using var client = application.GetTestClient();

        async Task<string> Confirm(string route, string userId, string code, HttpStatusCode expected)
        {
            string path = QueryHelpers.AddQueryString("/api/users/" + route, new Dictionary<string, string?>
            {
                ["userId"] = userId,
                ["code"] = code
            });
            using var response = await client.GetAsync(path);
            string body = await response.Content.ReadAsStringAsync();
            Assert.Equal(expected, response.StatusCode);
            Assert.DoesNotContain(user.Email, body);
            Assert.DoesNotContain(user.PhoneNumber, body);
            if (expected == HttpStatusCode.BadRequest)
            {
                Assert.Contains("validation_failed", body);
            }

            return body;
        }

        await Confirm("confirm-email", Guid.NewGuid().ToString(), emailToken, HttpStatusCode.BadRequest);
        await Confirm("confirm-email", user.Id, "%", HttpStatusCode.BadRequest);
        string invalidEmailToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("invalid-identity-token"));
        await Confirm("confirm-email", user.Id, invalidEmailToken, HttpStatusCode.BadRequest);
        string emailSuccess = await Confirm("confirm-email", user.Id, emailToken, HttpStatusCode.OK);
        Assert.Contains("Email confirmed", emailSuccess);
        Assert.Contains("/api/tokens/get", emailSuccess);
        await Confirm("confirm-email", user.Id, emailToken, HttpStatusCode.BadRequest);

        await Confirm("confirm-phone-number", Guid.NewGuid().ToString(), phoneToken, HttpStatusCode.BadRequest);
        await Confirm("confirm-phone-number", withoutPhone.Id, phoneToken, HttpStatusCode.BadRequest);
        await Confirm("confirm-phone-number", user.Id, "invalid-phone-token", HttpStatusCode.BadRequest);
        string phoneSuccess = await Confirm("confirm-phone-number", user.Id, phoneToken, HttpStatusCode.OK);
        Assert.Contains("Phone number confirmed", phoneSuccess);

        var saved = await users.FindByIdAsync(user.Id);
        Assert.NotNull(saved);
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Entry(saved).ReloadAsync();
        Assert.True(saved.EmailConfirmed);
        Assert.True(saved.PhoneNumberConfirmed);
        Assert.True((await users.UpdateSecurityStampAsync(saved)).Succeeded);
        await Confirm("confirm-phone-number", user.Id, phoneToken, HttpStatusCode.BadRequest);
    }
}
