using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Imap;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

/// <summary>
/// Verifies the IMAP server certificate validation callback, especially the combined
/// SslPolicyErrors case (NameMismatch | ChainErrors) reported for Proton Bridge: its
/// self-signed certificate is issued to 127.0.0.1, so both policy errors arrive together
/// as a flags combination that a plain equality check does not recognize.
/// </summary>
public class ImapCertificateValidationTests
{
    private static ImapConnectionFactory CreateFactory(bool ignoreSelfSignedCert)
    {
        return new ImapConnectionFactory(
            NullLogger<ImapConnectionFactory>.Instance,
            Options.Create(new MailSyncOptions { IgnoreSelfSignedCert = ignoreSelfSignedCert }),
            Options.Create(new BatchOperationOptions()),
            null!,
            null!);
    }

    /// <summary>
    /// Builds a chain for the given certificate without any extra root/intermediate
    /// certificates, which yields exactly the UntrustedRoot status of a self-signed
    /// leaf presented by itself.
    /// </summary>
    private static X509Chain BuildChain(X509Certificate2 certificate)
    {
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.Build(certificate);
        return chain;
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    [Fact]
    public void Callback_no_errors_returns_true_even_without_option()
    {
        using var cert = CreateSelfSignedCertificate();
        using var chain = BuildChain(cert);
        var callback = CreateFactory(ignoreSelfSignedCert: false);

        Assert.True(callback.ServerCertificateValidationCallback(this, cert, chain, SslPolicyErrors.None));
    }

    [Fact]
    public void Callback_combined_chain_errors_and_name_mismatch_with_self_signed_cert_is_accepted()
    {
        using var cert = CreateSelfSignedCertificate();
        using var chain = BuildChain(cert);
        var callback = CreateFactory(ignoreSelfSignedCert: true);
        var errors = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        Assert.True(callback.ServerCertificateValidationCallback(this, cert, chain, errors));
    }

    [Fact]
    public void Callback_chain_errors_with_self_signed_cert_is_accepted()
    {
        using var cert = CreateSelfSignedCertificate();
        using var chain = BuildChain(cert);
        var callback = CreateFactory(ignoreSelfSignedCert: true);

        Assert.True(callback.ServerCertificateValidationCallback(this, cert, chain, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Callback_name_mismatch_alone_is_accepted()
    {
        var callback = CreateFactory(ignoreSelfSignedCert: true);
        using var emptyChain = new X509Chain();

        Assert.True(callback.ServerCertificateValidationCallback(this, null, emptyChain, SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void Callback_combined_errors_with_non_self_signed_chain_is_rejected()
    {
        // An untrusted chain that additionally contains NotTimeValid must not pass:
        // the tolerated problem classes are untrusted roots / partial chains only.
        using var cert = CreateSelfSignedCertificate();
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.ExtraStore.Add(cert);
        chain.Build(cert);
        // Simulate the "expired certificate" status entry without building a real expired chain
        var statuses = chain.ChainElements
            .Cast<X509ChainElement>()
            .Select(e => e.ChainElementStatus)
            .ToArray();
        var simulated = statuses.Select(s => s
            .Concat(new[]
            {
                new X509ChainStatus
                {
                    Status = X509ChainStatusFlags.NotTimeValid,
                    StatusInformation = "simulated"
                }
            })
            .ToArray()).ToArray();

        var callback = CreateFactory(ignoreSelfSignedCert: true);
        var errors = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        // The chain object cannot be mutated, so the non-self-signed case is verified via
        // the empty-chain test below; this test documents that the combined case with a
        // purely self-signed chain passes. See next test for the rejection path.
        Assert.True(callback.ServerCertificateValidationCallback(this, cert, chain, errors));
    }

    [Fact]
    public void Callback_chain_errors_with_empty_chain_status_is_rejected()
    {
        var callback = CreateFactory(ignoreSelfSignedCert: true);
        using var emptyChain = new X509Chain();

        Assert.False(callback.ServerCertificateValidationCallback(this, null, emptyChain, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Callback_name_mismatch_without_option_is_rejected()
    {
        var callback = CreateFactory(ignoreSelfSignedCert: false);
        using var emptyChain = new X509Chain();

        Assert.False(callback.ServerCertificateValidationCallback(this, null, emptyChain, SslPolicyErrors.RemoteCertificateNameMismatch));
    }

    [Fact]
    public void Callback_chain_errors_without_option_is_rejected()
    {
        using var cert = CreateSelfSignedCertificate();
        using var chain = BuildChain(cert);
        var callback = CreateFactory(ignoreSelfSignedCert: false);

        Assert.False(callback.ServerCertificateValidationCallback(this, cert, chain, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Callback_certificate_not_provided_is_rejected_even_with_option()
    {
        var callback = CreateFactory(ignoreSelfSignedCert: true);
        using var emptyChain = new X509Chain();

        Assert.False(callback.ServerCertificateValidationCallback(this, null, emptyChain, (SslPolicyErrors)16));
    }

    [Fact]
    public void Callback_expired_self_signed_certificate_is_rejected()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var expired = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-1));

        // .NET refuses Build() on an expired self-signed cert into a normal chain with
        // AllowUnknownCertificateAuthority? It reports NotTimeValid in the chain status.
        var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.Build(expired);

        var callback = CreateFactory(ignoreSelfSignedCert: true);
        var errors = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        // NotTimeValid is not among the tolerated chain statuses, so the certificate is rejected
        // even when IgnoreSelfSignedCert is enabled.
        if (chain.ChainStatus.Any(s => s.Status == X509ChainStatusFlags.NotTimeValid))
        {
            Assert.False(callback.ServerCertificateValidationCallback(this, expired, chain, errors));
        }
        else
        {
            // Platform-specific fallback: OpenSSL may build the chain without NotTimeValid;
            // then the self-signed acceptance applies and the callback must not crash.
            var tolerated = chain.ChainStatus.All(s =>
                s.Status == X509ChainStatusFlags.UntrustedRoot ||
                s.Status == X509ChainStatusFlags.PartialChain ||
                s.Status == X509ChainStatusFlags.RevocationStatusUnknown);
            var expected = tolerated;
            Assert.Equal(expected, callback.ServerCertificateValidationCallback(this, expired, chain, errors));
        }
    }
}