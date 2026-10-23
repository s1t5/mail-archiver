using System.Security.Cryptography;
using MailArchiver.Models;
using MailArchiver.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

public class CredentialProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static CredentialProtector Create(string? key, string? previous = null, bool enabled = true)
        => new(Options.Create(new SecurityOptions
        {
            CredentialEncryptionKey = key,
            CredentialEncryptionKeyPrevious = previous,
            EncryptAccountCredentials = enabled
        }), NullLogger<CredentialProtector>.Instance);

    [Fact]
    public void Protect_Unprotect_Roundtrip()
    {
        var protector = Create(NewKey());

        var cipher = protector.Protect("s3cr3t");

        Assert.NotNull(cipher);
        Assert.NotEqual("s3cr3t", cipher);
        Assert.StartsWith("enc:v1:", cipher);
        Assert.True(protector.IsProtected(cipher));
        Assert.Equal("s3cr3t", protector.Unprotect(cipher));
    }

    [Fact]
    public void Protect_ProducesDifferentCiphertextEachTime()
    {
        var protector = Create(NewKey());

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void Unprotect_LegacyPlaintext_ReturnsUnchanged()
    {
        var protector = Create(NewKey());

        Assert.Equal("plain", protector.Unprotect("plain"));
        Assert.False(protector.IsProtected("plain"));
    }

    [Fact]
    public void Protect_WithoutKey_ReturnsPlaintext()
    {
        var protector = Create(null);

        Assert.False(protector.IsEnabled);
        Assert.Equal("plain", protector.Protect("plain"));
    }

    [Fact]
    public void Protect_WhenDisabled_ReturnsPlaintext()
    {
        var protector = Create(NewKey(), enabled: false);

        Assert.False(protector.IsEnabled);
        Assert.Equal("plain", protector.Protect("plain"));
    }

    [Fact]
    public void Unprotect_WithoutKey_ReturnsNullForEncryptedValue()
    {
        var encrypted = Create(NewKey()).Protect("secret");
        var protector = Create(null);

        Assert.Null(protector.Unprotect(encrypted));
    }

    [Fact]
    public void Unprotect_WithWrongKey_ReturnsNull()
    {
        var encrypted = Create(NewKey()).Protect("secret");
        var protector = Create(NewKey());

        Assert.Null(protector.Unprotect(encrypted));
    }

    [Fact]
    public void Unprotect_WithPreviousKey_Decrypts()
    {
        var oldKey = NewKey();
        var encrypted = Create(oldKey).Protect("secret");
        var protector = Create(NewKey(), previous: oldKey);

        Assert.Equal("secret", protector.Unprotect(encrypted));
    }

    [Fact]
    public void Unprotect_TamperedPayload_ReturnsNull()
    {
        var protector = Create(NewKey());
        var encrypted = protector.Protect("secret")!;
        var tampered = encrypted[..^4] + "AAAA";

        Assert.Null(protector.Unprotect(tampered));
    }

    [Fact]
    public void Unprotect_UnknownVersion_ReturnsUnchanged()
    {
        var protector = Create(NewKey());

        // Unknown formats are treated as legacy values instead of being discarded.
        Assert.Equal("enc:v9:AAAA", protector.Unprotect("enc:v9:AAAA"));
    }

    [Fact]
    public void Protect_NullOrEmpty_ReturnsAsIs()
    {
        var protector = Create(NewKey());

        Assert.Null(protector.Protect(null));
        Assert.Equal(string.Empty, protector.Protect(string.Empty));
    }

    [Fact]
    public void InvalidKey_IsIgnored_AndEncryptionDisabled()
    {
        var protector = Create("not-base64!!");

        Assert.False(protector.IsEnabled);
        Assert.Equal("plain", protector.Protect("plain"));
    }
}
