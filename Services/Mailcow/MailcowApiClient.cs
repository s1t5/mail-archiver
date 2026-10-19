using System.Net.Http.Json;
using System.Text.Json;
using MailArchiver.Models;

namespace MailArchiver.Services.Mailcow;

public record MailcowRemoteMailbox(string Address, string Name, bool Active);
public record MailcowAppPassword(string Id, string Name);

// Never expose Mailcow response bodies: app-passwd responses echo plaintext passwords.
public class MailcowException(string message) : Exception(message);

public class MailcowApiClient(HttpClient http)
{
    public static Uri ValidateUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new MailcowException("Use the HTTPS origin of your Mailcow instance, for example https://mail.example.com.");
        return uri;
    }

    private async Task<JsonElement> SendAsync(MailcowInstance instance, HttpMethod method, string path,
        object? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(instance.ApiKey))
            throw new MailcowException("The API key is unavailable. Check the configured credential encryption key.");
        using var request = new HttpRequestMessage(method, new Uri(ValidateUrl(instance.ApiUrl), "api/v1/" + path));
        request.Headers.Add("X-API-Key", instance.ApiKey);
        if (body != null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new MailcowException($"Mailcow API request failed (HTTP {(int)response.StatusCode}). Check API permissions and the allowed source IPs.");
            // Deliberately deserialize only the fields we need, without logging raw responses.
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = json.RootElement;
            // Mailcow serializes an empty collection as {} on some list endpoints.
            // Accept this only for reads; mutations still require explicit success rows.
            if (method == HttpMethod.Get && root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().Any())
                return JsonSerializer.SerializeToElement(Array.Empty<object>());
            if (root.ValueKind != JsonValueKind.Array || root.EnumerateArray().Any(x => Text(x, "type") is "error" or "danger" or "warning"))
                throw new MailcowException("Mailcow rejected the API request. Check the instance configuration and API permissions.");
            if (method == HttpMethod.Post &&
                (root.GetArrayLength() == 0 || root.EnumerateArray().Any(x => Text(x, "type") != "success")))
                throw new MailcowException("Mailcow did not confirm the change.");
            return root.Clone();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException)
        {
            throw new MailcowException("Mailcow could not be reached or returned an invalid response. Retry to reconcile the remote state.");
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
