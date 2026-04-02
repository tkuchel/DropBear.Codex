using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Providers;
using DropBear.Codex.Serialization.Serializers;

namespace DropBear.Codex.Files.Converters;

internal static class ProviderTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> IdentifierToType =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["serializer:json"] = typeof(JsonSerializer),
            ["serializer:messagepack"] = typeof(MessagePackSerializer),
            ["serializer:compressed"] = typeof(CompressedSerializer),
            ["serializer:encoded"] = typeof(EncodedSerializer),
            ["serializer:encrypted"] = typeof(EncryptedSerializer),
            ["serializer:combined"] = typeof(CombinedSerializer),
            ["compression:gzip"] = typeof(GZipCompressionProvider),
            ["compression:deflate"] = typeof(DeflateCompressionProvider),
            ["encryption:aescng"] = typeof(AESCNGEncryptionProvider),
            ["encryption:aesgcm"] = typeof(AESGCMEncryptionProvider),
            ["encoding:base64"] = typeof(Base64EncodingProvider),
            ["encoding:hex"] = typeof(HexEncodingProvider)
        };

    private static readonly IReadOnlyDictionary<Type, string> TypeToIdentifier =
        IdentifierToType.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public static bool TryResolve(string identifier, out Type? type)
    {
        type = null;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return false;
        }

        if (IdentifierToType.TryGetValue(identifier, out var resolved))
        {
            type = resolved;
            return true;
        }

        type = IdentifierToType
            .FirstOrDefault(kvp => string.Equals(kvp.Value.FullName, identifier, StringComparison.Ordinal) ||
                                   string.Equals(kvp.Value.AssemblyQualifiedName, identifier, StringComparison.Ordinal))
            .Value;

        return type != null;
    }

    public static string GetIdentifier(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (TypeToIdentifier.TryGetValue(type, out var identifier))
        {
            return identifier;
        }

        throw new InvalidOperationException(
            $"The provider type '{type.FullName}' is not registered for DropBear file serialization.");
    }

    public static bool IsAllowed(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return TypeToIdentifier.ContainsKey(type);
    }

    public static bool IsAllowedSerializer(Type type) => typeof(ISerializer).IsAssignableFrom(type) && IsAllowed(type);
    public static bool IsAllowedCompression(Type type) => typeof(ICompressionProvider).IsAssignableFrom(type) && IsAllowed(type);
    public static bool IsAllowedEncryption(Type type) => typeof(IEncryptionProvider).IsAssignableFrom(type) && IsAllowed(type);
}
