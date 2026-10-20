using System.Net.Http.Json;
using System.Text.Json;
using MailArchiver.Models;

namespace MailArchiver.Services.Mailcow;

public record MailcowRemoteMailbox(string Address, string Name, bool Active);
public record MailcowAppPassword(string Id, string Name);

// Never expose Mailcow response bodies: app-passwd responses echo plaintext passwords.
// The resource key is localized by the controller; service and API layers stay UI-agnostic.
public class MailcowException : Exception
{
    public string ResourceKey { get; }
    public object[] ResourceArgs { get; }

    public MailcowException(string resourceKey, params object[] resourceArgs) : base(resourceKey)
    {
        ResourceKey = resourceKey;
        ResourceArgs = resourceArgs;
    }
}

public class MailcowApiClient(HttpClient http)
{
    public static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new MailcowException("MailcowHttpsOriginRequired");
        return uri;
    }

    private async Task<JsonElement> SendAsync(MailcowInstance instance, HttpMethod method, string path,
        object? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instance.ApiKey))
            throw new MailcowException("MailcowApiKeyUnavailable");
        using var request = new HttpRequestMessage(method, new Uri(ValidateUrl(instance.ApiUrl), "api/v1/" + path));
        request.Headers.Add("X-API-Key", instance.ApiKey);
        if (body != null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new MailcowException("MailcowApiRequestFailed", (int)response.StatusCode);
            // Deliberately deserialize only the fields we need, without logging raw responses.
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = json.RootElement;
            // Mailcow serializes an empty collection as {} on some list endpoints.
            // Accept this only for reads; mutations still require explicit success rows.
            if (method == HttpMethod.Get && root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().Any())
                return JsonSerializer.SerializeToElement(Array.Empty<object>());
            if (root.ValueKind != JsonValueKind.Array || root.EnumerateArray().Any(x => Text(x, "type") is "error" or "danger" or "warning"))
                throw new MailcowException("MailcowApiRejected");
            if (method == HttpMethod.Post &&
                (root.GetArrayLength() == 0 || root.EnumerateArray().Any(x => Text(x, "type") != "success")))
                throw new MailcowException("MailcowApiNotConfirmed");
            return root.Clone();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            throw new MailcowException("MailcowApiUnreachable");
        }
    }

    private static string Text(JsonElement row, string key) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) ? value.ToString() : string.Empty;

    public virtual async Task<List<MailcowRemoteMailbox>> GetMailboxesAsync(MailcowInstance instance, CancellationToken ct)
    {
        var root = await SendAsync(instance, HttpMethod.Get, "get/mailbox/all", null, ct);
        return root.EnumerateArray().Select(x => new MailcowRemoteMailbox(
                Text(x, "username").Trim().ToLowerInvariant(), Text(x, "name"), Text(x, "active") is "1" or "true" or "True"))
            .Where(x => !string.IsNullOrWhiteSpace(x.Address)).DistinctBy(x => x.Address).OrderBy(x => x.Address).ToList();
    }

    public virtual async Task<List<MailcowAppPassword>> GetAppPasswordsAsync(MailcowInstance instance, string address, CancellationToken ct)
    {
        var root = await SendAsync(instance, HttpMethod.Get, "get/app-passwd/all/" + Uri.EscapeDataString(address), null, ct);
        return root.EnumerateArray().Select(x => new MailcowAppPassword(Text(x, "id"),
            string.IsNullOrEmpty(Text(x, "name")) ? Text(x, "app_name") : Text(x, "name"))).ToList();
    }

    public virtual async Task CreateAppPasswordAsync(MailcowInstance instance, string address, string name, string password, CancellationToken ct)
    {
        await SendAsync(instance, HttpMethod.Post, "add/app-passwd", new
        {
            active = "1", username = address, app_name = name, app_passwd = password,
            app_passwd2 = password, protocols = new[] { "imap_access" }
        }, ct);
    }

    public virtual async Task DeleteAppPasswordAsync(MailcowInstance instance, string id, CancellationToken ct)
    {
        await SendAsync(instance, HttpMethod.Post, "delete/app-passwd", new[] { id }, ct);
    }
}
