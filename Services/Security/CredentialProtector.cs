using System.Security.Cryptography;
using System.Text;
using MailArchiver.Models;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Security
{
    /// <summary>
    /// AES-256-GCM based credential protector. The key encryption key (KEK) is read from
    /// configuration (recommended: the environment variable
    /// <c>Security__CredentialEncryptionKey</c>) and never stored in the database. A
    /// per-version data key is derived from the KEK with HKDF-SHA256.
    ///
    /// Envelope format: <c>enc:v1:&lt;base64(nonce || ciphertext || tag)&gt;</c>.
    /// Values without the <c>enc:</c> prefix are treated as legacy plain text.
    /// </summary>
    public sealed class CredentialProtector : ICredentialProtector
    {
        private const string EnvelopePrefix = "enc:";
        private const string V1Prefix = "enc:v1:";
        private const string V1 = "v1";

        private const int KeySizeBytes = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly ILogger<CredentialProtector> _logger;
        private readonly SecurityOptions _options;
        private readonly byte[]? _primaryKey;
        private readonly byte[]? _previousKey;

        public CredentialProtector(IOptions<SecurityOptions> options, ILogger<CredentialProtector> logger)
        {
            _logger = logger;
            _options = options.Value;

            _primaryKey = TryParseKey(_options.CredentialEncryptionKey, "Security:CredentialEncryptionKey");
            _previousKey = TryParseKey(_options.CredentialEncryptionKeyPrevious, "Security:CredentialEncryptionKeyPrevious");

            if (_options.EncryptAccountCredentials && _primaryKey == null)
            {
                _logger.LogWarning(
                    "Mail account credential encryption is enabled but no valid 'Security:CredentialEncryptionKey' is configured. " +
                    "Credentials will be stored as plain text. Generate a key with 'openssl rand -base64 32' and provide it via " +
                    "the environment variable 'Security__CredentialEncryptionKey' (or appsettings).");
            }
        }

        /// <inheritdoc />
        public bool IsEnabled => _options.EncryptAccountCredentials && _primaryKey != null;

        /// <inheritdoc />
        public bool IsProtected(string? value)
            => !string.IsNullOrEmpty(value) && value.StartsWith(EnvelopePrefix, StringComparison.Ordinal);

        /// <inheritdoc />
        public string? Protect(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (!IsEnabled || IsProtected(value))
                return value;

            try
            {
                return EncryptV1(value);
            }
            catch (Exception ex)
            {
                // Never block a save because of an encryption problem; fall back to plain text.
                _logger.LogError(ex, "Failed to encrypt a mail account credential; storing it as plain text");
                return value;
            }
        }

        /// <inheritdoc />
        public string? Unprotect(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Legacy plain text (or an unknown future format) is returned unchanged. This also
            // protects plain text that coincidentally starts with the envelope prefix but is
            // not a structurally valid payload.
            if (!value.StartsWith(V1Prefix, StringComparison.Ordinal))
                return value;

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(value[V1Prefix.Length..]);
            }
            catch (FormatException)
            {
                return value;
            }

            if (payload.Length < NonceSize + TagSize)
                return value;

            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
            var ciphertext = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
            var buffer = new byte[ciphertext.Length];

            foreach (var key in EnumerateDecryptionKeys())
            {
                try
                {
                    var dataKey = DeriveKey(key, V1);
                    using var aes = new AesGcm(dataKey, TagSize);
                    aes.Decrypt(nonce, ciphertext, tag, buffer);
                    return Encoding.UTF8.GetString(buffer);
                }
                catch (CryptographicException)
                {
                    // Wrong key (e.g. the previous key after a rotation) - try the next one.
                }
            }

            _logger.LogWarning(
                "Failed to decrypt a mail account credential. The configured encryption key may have changed or been lost; " +
                "the credential has to be entered again.");
            return null;
        }

        private IEnumerable<byte[]> EnumerateDecryptionKeys()
        {
            if (_primaryKey != null)
                yield return _primaryKey;
            if (_previousKey != null)
                yield return _previousKey;
        }

        private string EncryptV1(string value)
        {
            var key = DeriveKey(_primaryKey!, V1);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plaintext = Encoding.UTF8.GetBytes(value);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];

            using (var aes = new AesGcm(key, TagSize))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            var payload = new byte[NonceSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, payload, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, payload, NonceSize + ciphertext.Length, TagSize);

            return V1Prefix + Convert.ToBase64String(payload);
        }

        private static byte[] DeriveKey(byte[] kek, string version)
            => HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                kek,
                outputLength: KeySizeBytes,
                salt: null,
                info: Encoding.UTF8.GetBytes($"MailArchiver.Credentials.{version}"));

        private byte[]? TryParseKey(string? raw, string name)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                var key = Convert.FromBase64String(raw.Trim());
                if (key.Length != KeySizeBytes)
                {
                    _logger.LogError(
                        "{Name} must be a base64 encoded 32 byte (256 bit) key but was {Length} byte(s); ignoring it",
                        name, key.Length);
                    return null;
                }

                return key;
            }
            catch (FormatException)
            {
                _logger.LogError("{Name} is not valid base64; ignoring it", name);
                return null;
            }
        }
    }
}
