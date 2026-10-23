using System.Net;
using System.Text;
using System.Text.Json;
using MailArchiver.Models;
using MailArchiver.Services.Mailcow;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailArchiver.Tests.Services;

public class MailcowApiClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request);
    }

    [Fact]
    public async Task Create_UsesOnlyImap_AndSendsPasswordOnlyInBody()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("https://mail.example.com/api/v1/add/app-passwd", request.RequestUri!.AbsoluteUri);
            Assert.Equal("api-secret", Assert.Single(request.Headers.GetValues("X-API-Key")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("mailbox@example.com", body.RootElement.GetProperty("username").GetString());
            Assert.Equal("generated-secret", body.RootElement.GetProperty("app_passwd").GetString());
            Assert.Equal("generated-secret", body.RootElement.GetProperty("app_passwd2").GetString());
            Assert.Equal("imap_access", Assert.Single(body.RootElement.GetProperty("protocols").EnumerateArray()).GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"type\":\"success\",\"log\":{\"app_passwd\":\"generated-secret\"}}]") };
        });
        await new MailcowApiClient(new HttpClient(handler), NullLogger<MailcowApiClient>.Instance)
            .CreateAppPasswordAsync(Instance(), "mailbox@example.com", "archive", "generated-secret", default);
    }

    [Theory]
    [InlineData("[{\"type\":\"error\",\"msg\":\"secret-value\"}]")]
    [InlineData("[{\"type\":\"danger\",\"msg\":\"secret-value\"}]")]
    [InlineData("not-json secret-value")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task RejectedOrMalformedMutation_DoesNotExposeResponse(string response)
    {
        var api = new MailcowApiClient(new HttpClient(new Handler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") }))),
            NullLogger<MailcowApiClient>.Instance);
        var error = await Assert.ThrowsAsync<MailcowException>(() => api.DeleteAppPasswordAsync(Instance(), "123", default));
        Assert.DoesNotContain("secret-value", error.ToString());
    }

    [Theory]
    [InlineData("http://mail.example.com")]
    [InlineData("https://key@mail.example.com")]
    [InlineData("https://mail.example.com/api/")]
    [InlineData("https://mail.example.com/?key=secret")]
    public void RejectsUnsafeApiOrigins(string value) => Assert.Throws<MailcowException>(() => MailcowApiClient.ValidateUrl(value));

    [Fact]
    public async Task EmptyObjectFromMailcow_IsAnEmptyCollectionForReads()
    {
        var api = new MailcowApiClient(new HttpClient(new Handler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }))),
            NullLogger<MailcowApiClient>.Instance);
        Assert.Empty(await api.GetAppPasswordsAsync(Instance(), "test@example.com", default));
        Assert.Empty(await api.GetMailboxesAsync(Instance(), default));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("app_name")]
    public async Task AppPasswordListing_ReadsTheReturnedNameField(string field)
    {
        var response = "[{\"id\":42,\"" + field + "\":\"MailArchiver-test\",\"password\":\"not-returned\"}]";
        var api = new MailcowApiClient(new HttpClient(new Handler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) }))),
            NullLogger<MailcowApiClient>.Instance);
        var app = Assert.Single(await api.GetAppPasswordsAsync(Instance(), "test@example.com", default));
        Assert.Equal("42", app.Id);
        Assert.Equal("MailArchiver-test", app.Name);
        Assert.DoesNotContain("not-returned", app.ToString());
    }

    [Fact]
    public async Task MailboxListing_HandlesNumericAndBooleanActiveFlags_AndNormalizesAddresses()
    {
        var api = new MailcowApiClient(new HttpClient(new Handler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"username\":\"A@Example.com\",\"active\":1},{\"username\":\"b@example.com\",\"active\":true},{\"username\":\"c@example.com\",\"active\":0}]") }))),
            NullLogger<MailcowApiClient>.Instance);
        var mailboxes = await api.GetMailboxesAsync(Instance(), default);
        Assert.Equal("a@example.com", mailboxes[0].Address);
        Assert.True(mailboxes[0].Active);
        Assert.True(mailboxes[1].Active);
        Assert.False(mailboxes[2].Active);
    }

    internal static MailcowInstance Instance() => new()
    { Name = "Mailcow", ApiUrl = "https://mail.example.com/", ApiKey = "api-secret", ImapHost = "mail.example.com" };
}
