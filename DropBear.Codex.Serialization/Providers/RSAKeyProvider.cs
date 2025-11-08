#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using Serilog;
using static System.Security.Cryptography.ProtectedData;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides methods to generate or load RSA keys from files.
///     SECURITY NOTE (M3 - Medium Severity): Uses Windows DPAPI for private key encryption.
///     This is Windows-only. For cross-platform support, consider implementing AES-GCM encryption
///     with secure key storage (Azure Key Vault, AWS Secrets Manager, or user keychain).
///     See SECURITY.md for details.
/// </summary>
public sealed class RSAKeyProvider
{
    private readonly bool _createIfNotExists;
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<RSAKeyProvider>();
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RSAKeyProvider" /> class with the paths to the public and private key
    ///     files.
    /// </summary>
    /// <param name="publicKeyPath">The path to the public key file.</param>
    /// <param name="privateKeyPath">The path to the private key file.</param>
    /// <param name="createIfNotExists">Whether to create the keys if they don't exist.</param>
    public RSAKeyProvider(string publicKeyPath, string privateKeyPath, bool createIfNotExists = true)
    {
        _publicKeyPath = publicKeyPath ?? throw new ArgumentNullException(nameof(publicKeyPath));
        _privateKeyPath = privateKeyPath ?? throw new ArgumentNullException(nameof(privateKeyPath));
        _createIfNotExists = createIfNotExists;

        _logger.Information("RSAKeyProvider initialized with PublicKeyPath: {PublicKeyPath}, " +
                            "PrivateKeyPath: {PrivateKeyPath}, CreateIfNotExists: {CreateIfNotExists}",
            publicKeyPath, privateKeyPath, createIfNotExists);
    }

    /// <summary>
    ///     Gets an RSA provider with the loaded or generated keys.
    /// </summary>
    /// <returns>An RSA provider with the loaded or generated keys.</returns>
    /// <exception cref="InvalidOperationException">Thrown if key files cannot be loaded or created.</exception>
    /// <exception cref="CryptographicException">Thrown if there is an error with the cryptographic operations.</exception>
    [SupportedOSPlatform("windows")]
    public RSA GetRsaProvider()
    {
        var rsa = RSA.Create(2048); // Create with default key size

        try
        {
            var keysExist = File.Exists(_privateKeyPath) && File.Exists(_publicKeyPath);

            if (keysExist)
            {
                // Load existing keys
                _logger.Information("Loading RSA keys from {PrivateKeyPath} and {PublicKeyPath}",
                    _privateKeyPath, _publicKeyPath);

                try
                {
                    var parameters = LoadKeyFromFile(_privateKeyPath, true);
                    rsa.ImportParameters(parameters);

                    _logger.Information("RSA keys loaded successfully with KeySize: {KeySize}", rsa.KeySize);
                }
                catch (CryptographicException ex)
                {
                    _logger.Error(ex, "Failed to load RSA keys due to cryptographic error: {Message}", ex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to load RSA keys: {Message}", ex.Message);
                    throw new InvalidOperationException($"Failed to load RSA keys: {ex.Message}", ex);
                }
            }
            else if (_createIfNotExists)
            {
                // Generate and save new keys
                _logger.Information("RSA keys not found. Generating new RSA keys with KeySize: {KeySize}", rsa.KeySize);

                try
                {
                    var publicKey = rsa.ExportParameters(false);
                    var privateKey = rsa.ExportParameters(true);

                    // Ensure directories exist
                    Directory.CreateDirectory(Path.GetDirectoryName(_publicKeyPath) ?? ".");
                    Directory.CreateDirectory(Path.GetDirectoryName(_privateKeyPath) ?? ".");

                    SaveKeyToFile(_publicKeyPath, publicKey, false);
                    SaveKeyToFile(_privateKeyPath, privateKey, true);

                    _logger.Information("RSA keys generated and saved successfully.");
                }
                catch (CryptographicException ex)
                {
                    _logger.Error(ex, "Failed to generate or save RSA keys due to cryptographic error: {Message}",
                        ex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to generate or save RSA keys: {Message}", ex.Message);
                    throw new InvalidOperationException($"Failed to generate or save RSA keys: {ex.Message}", ex);
                }
            }
            else
            {
                _logger.Error("RSA key files not found and CreateIfNotExists is false.");
                throw new FileNotFoundException("RSA key files not found and CreateIfNotExists is false.",
                    _privateKeyPath);
            }

            return rsa;
        }
        catch (Exception)
        {
            // Clean up resources if an exception occurs
            rsa.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Gets RSA parameters from files with Result error handling.
    /// </summary>
    /// <returns>A Result containing the RSA provider or an error.</returns>
    [SupportedOSPlatform("windows")]
    public Result<RSA, SerializationError> GetRsaProviderWithResult()
    {
        try
        {
            var rsa = GetRsaProvider();
            return Result<RSA, SerializationError>.Success(rsa);
        }
        catch (CryptographicException ex)
        {
            _logger.Error(ex, "Cryptographic error getting RSA provider: {Message}", ex.Message);
            return Result<RSA, SerializationError>.Failure(
                new SerializationError($"Cryptographic error with RSA keys: {ex.Message}")
                {
                    Operation = "LoadRSAKeys"
                }, ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting RSA provider: {Message}", ex.Message);
            return Result<RSA, SerializationError>.Failure(
                new SerializationError($"Failed to get RSA provider: {ex.Message}") { Operation = "LoadRSAKeys" }, ex);
        }
    }

    /// <summary>
    ///     Saves RSA parameters to a file.
    /// </summary>
    /// <param name="filePath">The path to save the key.</param>
    /// <param name="parameters">The RSA parameters to save.</param>
    /// <param name="isPrivate">Whether this is a private key (needs extra protection).</param>
    /// <exception cref="ArgumentException">Thrown if the file path is invalid.</exception>
    [SupportedOSPlatform("windows")]
    private void SaveKeyToFile(string filePath, RSAParameters parameters, bool isPrivate)
    {
        try
        {
            // Check if PEM format is required (e.g., by checking file extension or user configuration)
            if (Path.GetExtension(filePath).Equals(".pem", StringComparison.OrdinalIgnoreCase))
            {
                var pemKey = ConvertToPemString(parameters, isPrivate);
                File.WriteAllText(filePath, pemKey);
                _logger.Information("Saved RSA key in PEM format to {FilePath}", filePath);
            }
            else
            {
                var keyString = ConvertToXmlString(parameters, isPrivate);

                if (isPrivate)
                {
                    // Encrypt the private key using DPAPI before saving it
                    var encryptedKey = Protect(Encoding.UTF8.GetBytes(keyString), null,
                        DataProtectionScope.CurrentUser);
                    File.WriteAllBytes(filePath, encryptedKey);
                    _logger.Information("Saved encrypted RSA private key to {FilePath}", filePath);
                }
                else
                {
                    File.WriteAllText(filePath, keyString);
                    _logger.Information("Saved RSA public key to {FilePath}", filePath);
                }
            }
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "I/O error saving RSA key to file {FilePath}: {Message}", filePath, ex.Message);
            throw new InvalidOperationException($"I/O error saving RSA key: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Access denied saving RSA key to file {FilePath}: {Message}", filePath, ex.Message);
            throw new InvalidOperationException($"Access denied saving RSA key: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save RSA key to file {FilePath}: {Message}", filePath, ex.Message);
            throw new InvalidOperationException($"Failed to save RSA key: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Loads RSA parameters from a file.
    /// </summary>
    /// <param name="filePath">The path to load the key from.</param>
    /// <param name="isPrivate">Whether this is a private key (needs extra protection).</param>
    /// <returns>The loaded RSA parameters.</returns>
    /// <exception cref="ArgumentException">Thrown if the file path is invalid.</exception>
    /// <exception cref="CryptographicException">Thrown if there is an error with the cryptographic operations.</exception>
    [SupportedOSPlatform("windows")]
    private RSAParameters LoadKeyFromFile(string filePath, bool isPrivate)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"RSA key file not found: {filePath}");
            }

            if (Path.GetExtension(filePath).Equals(".pem", StringComparison.OrdinalIgnoreCase))
            {
                var pemKey = File.ReadAllText(filePath);
                return ConvertFromPemString(pemKey, isPrivate);
            }

            if (isPrivate)
            {
                var encryptedKey = File.ReadAllBytes(filePath);
                var decryptedKey = Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
                var keyXml = Encoding.UTF8.GetString(decryptedKey);
                return ConvertFromXmlString(keyXml, isPrivate);
            }
            else
            {
                var keyXml = File.ReadAllText(filePath);
                return ConvertFromXmlString(keyXml, isPrivate);
            }
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "I/O error loading RSA key from file {FilePath}: {Message}", filePath, ex.Message);
            throw new InvalidOperationException($"I/O error loading RSA key: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Access denied loading RSA key from file {FilePath}: {Message}", filePath, ex.Message);
            throw new InvalidOperationException($"Access denied loading RSA key: {ex.Message}", ex);
        }
        catch (CryptographicException ex)
        {
            _logger.Error(ex, "Cryptographic error loading RSA key from file {FilePath}: {Message}", filePath,
                ex.Message);
            throw; // Rethrow cryptographic exceptions directly
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load RSA key from file {FilePath}: {Message}", filePath, ex.Message);
            throw new InvalidOperationException($"Failed to load RSA key: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Converts RSA parameters to a PEM format string.
    /// </summary>
    /// <param name="parameters">The RSA parameters to convert.</param>
    /// <param name="isPrivate">Whether this is a private key.</param>
    /// <returns>A PEM format string containing the key.</returns>
    /// <exception cref="CryptographicException">Thrown if there is an error exporting the key.</exception>
    private string ConvertToPemString(RSAParameters parameters, bool isPrivate)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(parameters);

        if (isPrivate)
        {
            var privateKeyBytes = rsa.ExportRSAPrivateKey();
            return PemEncode(privateKeyBytes, "PRIVATE KEY");
        }

        var publicKeyBytes = rsa.ExportRSAPublicKey();
        return PemEncode(publicKeyBytes, "PUBLIC KEY");
    }

    /// <summary>
    ///     Converts a PEM format string to RSA parameters.
    /// </summary>
    /// <param name="pem">The PEM string to convert.</param>
    /// <param name="isPrivate">Whether this is a private key.</param>
    /// <returns>The RSA parameters.</returns>
    /// <exception cref="CryptographicException">Thrown if the PEM format is invalid or cannot be parsed.</exception>
    private RSAParameters ConvertFromPemString(string pem, bool isPrivate)
    {
        var base64Key = pem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("-----BEGIN PUBLIC KEY-----", "")
            .Replace("-----END PUBLIC KEY-----", "")
            .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
            .Replace("-----END RSA PRIVATE KEY-----", "")
            .Replace("-----BEGIN RSA PUBLIC KEY-----", "")
            .Replace("-----END RSA PUBLIC KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "");

        var keyBytes = Convert.FromBase64String(base64Key);
        using var rsa = RSA.Create();

        if (isPrivate)
        {
            try
            {
                // Try PKCS#8 format first
                rsa.ImportPkcs8PrivateKey(keyBytes, out _);
            }
            catch (CryptographicException)
            {
                // Fall back to RSA Private Key format
                rsa.ImportRSAPrivateKey(keyBytes, out _);
            }
        }
        else
        {
            try
            {
                // Try SPKI format first
                rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            }
            catch (CryptographicException)
            {
                // Fall back to RSA Public Key format
                rsa.ImportRSAPublicKey(keyBytes, out _);
            }
        }

        return rsa.ExportParameters(isPrivate);
    }

    /// <summary>
    ///     Encodes binary data as a PEM format string.
    /// </summary>
    /// <param name="key">The key data to encode.</param>
    /// <param name="keyType">The type of key ("PRIVATE KEY" or "PUBLIC KEY").</param>
    /// <returns>A PEM format string containing the key.</returns>
    private string PemEncode(byte[] key, string keyType)
    {
        var base64Key = Convert.ToBase64String(key);
        var pemBuilder = new StringBuilder();
        pemBuilder.AppendLine($"-----BEGIN {keyType}-----");

        // Write the key in 64-character lines
        for (var i = 0; i < base64Key.Length; i += 64)
        {
            pemBuilder.AppendLine(base64Key.Substring(i, Math.Min(64, base64Key.Length - i)));
        }

        pemBuilder.AppendLine($"-----END {keyType}-----");
        return pemBuilder.ToString();
    }

    /// <summary>
    ///     Converts RSA parameters to an XML string.
    /// </summary>
    /// <param name="parameters">The RSA parameters to convert.</param>
    /// <param name="includePrivateParameters">Whether to include private parameters.</param>
    /// <returns>An XML string containing the key.</returns>
    /// <exception cref="CryptographicException">Thrown if there is an error exporting the key.</exception>
    private static string ConvertToXmlString(RSAParameters parameters, bool includePrivateParameters)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(parameters);
        return rsa.ToXmlString(includePrivateParameters);
    }

    /// <summary>
    ///     Converts an XML string to RSA parameters.
    /// </summary>
    /// <param name="xml">The XML string to convert.</param>
    /// <param name="includePrivateParameters">Whether to include private parameters.</param>
    /// <returns>The RSA parameters.</returns>
    /// <exception cref="CryptographicException">Thrown if the XML format is invalid or cannot be parsed.</exception>
    private static RSAParameters ConvertFromXmlString(string xml, bool includePrivateParameters)
    {
        using var rsa = RSA.Create();
        rsa.FromXmlString(xml);
        return rsa.ExportParameters(includePrivateParameters);
    }
}
