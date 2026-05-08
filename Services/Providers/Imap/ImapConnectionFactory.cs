using MailArchiver.Models;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// Factory for creating, connecting, and authenticating IMAP clients.
    /// Handles SSL/TLS→STARTTLS fallback, SASL PLAIN→auto authentication,
    /// certificate validation, and reconnection logic.
    /// </summary>
    public class ImapConnectionFactory
    {
        private readonly ILogger<ImapConnectionFactory> _logger;
        private readonly MailSyncOptions _mailSyncOptions;
        private readonly BatchOperationOptions _batchOptions;

        public ImapConnectionFactory(
            ILogger<ImapConnectionFactory> logger,
            IOptions<MailSyncOptions> mailSyncOptions,
            IOptions<BatchOperationOptions> batchOptions)
        {
            _logger = logger;
            _mailSyncOptions = mailSyncOptions.Value;
            _batchOptions = batchOptions.Value;
        }

        /// <summary>
        /// Creates a new ImapClient instance without protocol logging.
        /// </summary>
        public ImapClient CreateImapClient(string accountName)
        {
            return new ImapClient();
        }

        /// <summary>
        /// Extracts the authentication username from an account.
        /// </summary>
        public static string GetAuthenticationUsername(MailAccount account)
        {
            return account.Username ?? account.EmailAddress;
        }

        /// <summary>
        /// Connects to an IMAP server with SSL/TLS, falling back to STARTTLS if the initial
        /// SSL handshake fails.
        /// </summary>
        public async Task ConnectWithFallbackAsync(ImapClient client, string server, int port, bool useSSL, string accountName)
        {
            if (!useSSL)
            {
                _logger.LogDebug("Connecting to {Server}:{Port} with no security for account {AccountName}",
                    server, port, accountName);
                await client.ConnectAsync(server, port, SecureSocketOptions.None);
                return;
            }

            // First try: SSL/TLS directly
            try
            {
                _logger.LogDebug("Connecting to {Server}:{Port} with SSL/TLS for account {AccountName}",
                    server, port, accountName);
                await client.ConnectAsync(server, port, SecureSocketOptions.SslOnConnect);
                _logger.LogDebug("Successfully connected using SSL/TLS for account {AccountName}", accountName);
            }
            catch (SslHandshakeException sslEx)
            {
                _logger.LogDebug("SSL/TLS connection failed for account {AccountName}, trying STARTTLS: {Message}",
                    accountName, sslEx.Message);

                // Fallback: STARTTLS
                try
                {
                    await client.ConnectAsync(server, port, SecureSocketOptions.StartTls);
                    _logger.LogInformation("Successfully connected using STARTTLS for account {AccountName} on {Server}:{Port}",
                        accountName, server, port);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "STARTTLS fallback also failed for account {AccountName}", accountName);
                    throw new AggregateException("Both SSL/TLS and STARTTLS connection attempts failed", sslEx, fallbackEx);
                }
            }
        }

        /// <summary>
        /// Authenticates the IMAP client using a fallback authentication strategy.
        /// Tries SASL PLAIN first (for Exchange compatibility), then falls back to
        /// auto-negotiation if PLAIN fails (for T-Online and others).
        /// </summary>
        public async Task AuthenticateClientAsync(ImapClient client, MailAccount account)
        {
            // Remove GSSAPI and NEGOTIATE mechanisms to prevent Kerberos authentication attempts
            // which can fail in containerized environments due to missing libraries
            client.AuthenticationMechanisms.Remove("GSSAPI");
            client.AuthenticationMechanisms.Remove("NEGOTIATE");

            var username = GetAuthenticationUsername(account);
            var password = account.Password;

            // Try SASL PLAIN first (preferred for Exchange 2019 and similar servers)
            if (client.AuthenticationMechanisms.Contains("PLAIN"))
            {
                try
                {
                    _logger.LogDebug("Attempting SASL PLAIN authentication for account {AccountName}", account.Name);
                    var credentials = new NetworkCredential(username, password);
                    var saslPlain = new SaslMechanismPlain(credentials);
                    await client.AuthenticateAsync(saslPlain);
                    _logger.LogDebug("SASL PLAIN authentication successful for account {AccountName}", account.Name);
                    return;
                }
                catch (MailKit.Security.AuthenticationException ex)
                {
                    _logger.LogInformation("SASL PLAIN authentication failed for account {AccountName}, trying fallback: {Message}",
                        account.Name, ex.Message);
                    // Continue to fallback authentication
                }
            }
            else
            {
                _logger.LogInformation("SASL PLAIN not available for account {AccountName}, using fallback authentication", account.Name);
            }

            // Fallback: Let MailKit auto-negotiate the best available mechanism
            _logger.LogDebug("Using auto-negotiated authentication for account {AccountName}", account.Name);
            await client.AuthenticateAsync(username, password);
        }

        /// <summary>
        /// Reconnects the IMAP client by disconnecting, delaying, and re-establishing
        /// the connection with authentication.
        /// </summary>
        public async Task ReconnectClientAsync(ImapClient client, MailAccount account)
        {
            try
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true);
                }

                // Use the configurable pause between batches as reconnection delay
                if (_batchOptions.PauseBetweenBatchesMs > 0)
                {
                    await Task.Delay(_batchOptions.PauseBetweenBatchesMs);
                }

                _logger.LogInformation("Reconnecting to IMAP server for account {AccountName}", account.Name);
                await ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                client.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
                await AuthenticateClientAsync(client, account);
                _logger.LogInformation("Successfully reconnected to IMAP server for account {AccountName}", account.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to IMAP server for account {AccountName}", account.Name);
                throw new InvalidOperationException("Failed to reconnect to IMAP server", ex);
            }
        }

        /// <summary>
        /// Validates the server certificate based on the IgnoreSelfSignedCert setting.
        /// Accepts self-signed certificates and name mismatches when configured to do so.
        /// </summary>
        public bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // If there are no SSL policy errors, the certificate is valid
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // If we're configured to ignore self-signed certificates and the only error is
            // that the certificate is untrusted (which is typical for self-signed certs),
            // then accept the certificate
            if (_mailSyncOptions.IgnoreSelfSignedCert &&
                (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors ||
                 sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                // Additional check: if it's a chain error, verify it's specifically a self-signed certificate
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain.ChainStatus.Length > 0)
                {
                    // Check if the chain status indicates a self-signed certificate
                    bool isSelfSigned = chain.ChainStatus.All(status =>
                        status.Status == X509ChainStatusFlags.UntrustedRoot ||
                        status.Status == X509ChainStatusFlags.PartialChain ||
                        status.Status == X509ChainStatusFlags.RevocationStatusUnknown);

                    if (isSelfSigned)
                    {
                        _logger.LogDebug("Accepting self-signed certificate for IMAP server");
                        return true;
                    }
                }
                else if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    _logger.LogDebug("Accepting certificate with name mismatch for IMAP server (IgnoreSelfSignedCert=true)");
                    return true;
                }
            }

            // Log the certificate validation error
            _logger.LogWarning("Certificate validation failed for IMAP server: {SslPolicyErrors}", sslPolicyErrors);
            return false;
        }
    }
}