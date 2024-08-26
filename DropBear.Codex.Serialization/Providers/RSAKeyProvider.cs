﻿#region

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using Serilog;
using static System.Security.Cryptography.ProtectedData;

#endregion

namespace DropBear.Codex.Serialization.Providers;

/// <summary>
///     Provides methods to generate or load RSA keys from files.
/// </summary>
public class RSAKeyProvider
{
    private readonly ILogger _logger = LoggerFactory.Logger.ForContext<RSAKeyProvider>();
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RSAKeyProvider" /> class with the paths to the public and private key
    ///     files.
    /// </summary>
    /// <param name="publicKeyPath">The path to the public key file.</param>
    /// <param name="privateKeyPath">The path to the private key file.</param>
    public RSAKeyProvider(string publicKeyPath, string privateKeyPath)
    {
        _publicKeyPath = publicKeyPath;
        _privateKeyPath = privateKeyPath;
    }

    /// <summary>
    ///     Gets an RSA provider with the loaded or generated keys.
    /// </summary>
    /// <returns>An RSA provider with the loaded or generated keys.</returns>
    [SupportedOSPlatform("windows")]
    public RSA GetRsaProvider()
    {
        var rsa = RSA.Create();
        try
        {
            if (File.Exists(_privateKeyPath))
            {
                // Load existing keys
                _logger.Information("Loading RSA keys from {PrivateKeyPath} and {PublicKeyPath}", _privateKeyPath,
                    _publicKeyPath);
                rsa.ImportParameters(LoadKeyFromFile(_privateKeyPath, true));
            }
            else
            {
                // Generate and save new keys
                _logger.Information("RSA keys not found. Generating new RSA keys.");
                rsa.KeySize = 2048;
                var publicKey = rsa.ExportParameters(false);
                var privateKey = rsa.ExportParameters(true);
                SaveKeyToFile(_publicKeyPath, publicKey, false);
                SaveKeyToFile(_privateKeyPath, privateKey, true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while getting or generating RSA keys.");
            throw;
        }

        return rsa;
    }

    [SupportedOSPlatform("windows")]
    private static void SaveKeyToFile(string filePath, RSAParameters parameters, bool isPrivate)
    {
        try
        {
            var keyString = ConvertToXmlString(parameters, isPrivate);
            if (isPrivate)
            {
                // Encrypt the private key using DPAPI before saving it
                var encryptedKey = Protect(Encoding.UTF8.GetBytes(keyString), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(filePath, encryptedKey);
            }
            else
            {
                File.WriteAllText(filePath, keyString);
            }
        }
        catch (Exception ex)
        {
            LoggerFactory.Logger.ForContext<RSAKeyProvider>()
                .Error(ex, "Failed to save RSA key to file {FilePath}.", filePath);
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static RSAParameters LoadKeyFromFile(string filePath, bool isPrivate)
    {
        try
        {
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
        catch (Exception ex)
        {
            LoggerFactory.Logger.ForContext<RSAKeyProvider>()
                .Error(ex, "Failed to load RSA key from file {FilePath}.", filePath);
            throw;
        }
    }

    private static string ConvertToXmlString(RSAParameters parameters, bool includePrivateParameters)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(parameters);
        return
            rsa.ToXmlString(
                includePrivateParameters); // Replace with actual serialization method if not using .NET Framework
    }

    private static RSAParameters ConvertFromXmlString(string xml, bool includePrivateParameters)
    {
        using var rsa = RSA.Create();
        rsa.FromXmlString(xml); // Replace with actual deserialization method if not using .NET Framework
        return rsa.ExportParameters(includePrivateParameters);
    }
}
