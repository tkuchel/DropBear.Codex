﻿#region

using System.Text;
using DropBear.Codex.Core;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Hashing.Interfaces;
using HashDepot;
using Serilog;

#endregion

namespace DropBear.Codex.Hashing.Hashers;

public class SipHasher : IHasher
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SipHasher>();
    private byte[] _key;

    public SipHasher(byte[] key)
    {
        if (key == Array.Empty<byte>() || key.Length is not 16)
        {
            Logger.Error("Key must be 16 bytes in length.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        _key = key;
    }

    // SipHash does not use salt or iterations, so these methods are no-ops but are implemented for interface compliance
    public IHasher WithSalt(byte[]? salt)
    {
        return this;
    }

    public IHasher WithIterations(int iterations)
    {
        return this;
    }

    public Result<string> Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger.Error("Input cannot be null or empty.");
            return Result<string>.Failure("Input cannot be null or empty.");
        }

        try
        {
            Logger.Information("Hashing input with SipHash-2-4.");
            var buffer = Encoding.UTF8.GetBytes(input);
            var hash = SipHash24.Hash64(buffer, _key);
            return Result<string>.Success(hash.ToString("x8")); // Hex string format
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during hashing.");
            return Result<string>.Failure($"Error during hashing: {ex.Message}");
        }
    }

    public Result Verify(string input, string expectedHash)
    {
        var hashResult = Hash(input);
        if (!hashResult.IsSuccess)
        {
            Logger.Error("Failed to compute hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(hashResult.Value, expectedHash, StringComparison.OrdinalIgnoreCase);
        Logger.Information(isValid ? "Verification succeeded." : "Verification failed.");
        return isValid ? Result.Success() : Result.Failure("Verification failed.");
    }

    public Result<string> EncodeToBase64Hash(byte[] data)
    {
        if (data == Array.Empty<byte>() || data.Length is 0)
        {
            Logger.Error("Data cannot be null or empty.");
            return Result<string>.Failure("Data cannot be null or empty.");
        }

        try
        {
            Logger.Information("Encoding data to Base64 hash using SipHash-2-4.");
            var hash = SipHash24.Hash64(data, _key);
            var hashBytes = BitConverter.GetBytes(hash);
            var base64Hash = Convert.ToBase64String(hashBytes);
            return Result<string>.Success(base64Hash);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during Base64 encoding hash.");
            return Result<string>.Failure($"Error during base64 encoding hash: {ex.Message}");
        }
    }

    public Result VerifyBase64Hash(byte[] data, string expectedBase64Hash)
    {
        var encodeResult = EncodeToBase64Hash(data);
        if (!encodeResult.IsSuccess)
        {
            Logger.Error("Failed to compute Base64 hash for verification.");
            return Result.Failure("Failed to compute hash.");
        }

        var isValid = string.Equals(encodeResult.Value, expectedBase64Hash, StringComparison.Ordinal);
        Logger.Information(isValid ? "Base64 hash verification succeeded." : "Base64 hash verification failed.");
        return isValid ? Result.Success() : Result.Failure("Base64 hash verification failed.");
    }

    public IHasher WithKey(byte[] key)
    {
        if (key == Array.Empty<byte>() || key.Length is not 16)
        {
            Logger.Error("Key must be 16 bytes in length.");
            throw new ArgumentException("Key must be 16 bytes in length.", nameof(key));
        }

        Logger.Information("Setting a new key for SipHasher.");
        _key = key;
        return this;
    }

#pragma warning disable IDE0060 // Remove unused parameter
    // SipHash output size is fixed by the algorithm, so this method is effectively a noop.
    public IHasher WithHashSize(int size)
    {
        return this;
    }
#pragma warning restore IDE0060 // Remove unused parameter
}
