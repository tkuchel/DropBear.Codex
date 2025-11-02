# Script to migrate remaining log statements in SerializerFactory.cs

$filePath = "DropBear.Codex.Serialization\Factories\SerializerFactory.cs"

# Read the file
$content = Get-Content $filePath -Raw

# Array of replacements
$replacements = @(
    # CreateBaseSerializer
    @{
        Old = 'Logger.Information("Creating base serializer of type {SerializerType}", serializerType.Name);'
        New = 'LogCreatingBaseSerializer(Logger, serializerType.Name);'
    },
    # InstantiateSerializer
    @{
        Old = @'
                Logger.Information("Instantiating serializer of type {SerializerType} with MEL logger (migrated pattern)",
                    serializerType.Name);
'@
        New = '                LogInstantiatingSerializerWithLogger(Logger, serializerType.Name);'
    },
    @{
        Old = '            Logger.Information("Instantiating serializer of type {SerializerType} (legacy pattern)", serializerType.Name);'
        New = '            LogInstantiatingSerializerLegacy(Logger, serializerType.Name);'
    },
    @{
        Old = '            Logger.Error(ex, "Failed to instantiate serializer of type {SerializerType}", serializerType.Name);'
        New = '            LogInstantiateSerializerFailed(Logger, ex, serializerType.Name);'
    },
    # ApplyCompression
    @{
        Old = '            Logger.Information("No compression provider configured.");'
        New = '            LogNoCompressionProvider(Logger);'
    },
    @{
        Old = @'
            Logger.Information("Applying compression provider of type {ProviderType}",
                config.CompressionProviderType.Name);
'@
        New = '            LogApplyingCompressionProvider(Logger, config.CompressionProviderType.Name);'
    },
    @{
        Old = @'
            Logger.Error(ex, "Failed to apply compression provider of type {ProviderType}",
                config.CompressionProviderType.Name);
'@
        New = '            LogApplyCompressionFailed(Logger, ex, config.CompressionProviderType.Name);'
    },
    # ApplyEncryption
    @{
        Old = '            Logger.Information("No encryption provider configured.");'
        New = '            LogNoEncryptionProvider(Logger);'
    },
    @{
        Old = @'
            Logger.Information("Applying encryption provider of type {ProviderType}",
                config.EncryptionProviderType.Name);
'@
        New = '            LogApplyingEncryptionProvider(Logger, config.EncryptionProviderType.Name);'
    },
    @{
        Old = @'
            Logger.Error(ex, "Failed to apply encryption provider of type {ProviderType}",
                config.EncryptionProviderType.Name);
'@
        New = '            LogApplyEncryptionFailed(Logger, ex, config.EncryptionProviderType.Name);'
    },
    # ApplyEncoding
    @{
        Old = '            Logger.Information("No encoding provider configured.");'
        New = '            LogNoEncodingProvider(Logger);'
    },
    @{
        Old = '            Logger.Information("Applying encoding provider of type {ProviderType}", config.EncodingProviderType.Name);'
        New = '            LogApplyingEncodingProvider(Logger, config.EncodingProviderType.Name);'
    },
    @{
        Old = @'
            Logger.Error(ex, "Failed to apply encoding provider of type {ProviderType}",
                config.EncodingProviderType.Name);
'@
        New = '            LogApplyEncodingFailed(Logger, ex, config.EncodingProviderType.Name);'
    },
    # CreateProvider
    @{
        Old = '                Logger.Information("Instantiating provider of type {ProviderType} with logger factory", providerType.Name);'
        New = '                LogInstantiatingProviderWithLoggerFactory(Logger, providerType.Name);'
    },
    @{
        Old = '            Logger.Information("Instantiating provider of type {ProviderType}", providerType.Name);'
        New = '            LogInstantiatingProvider(Logger, providerType.Name);'
    },
    @{
        Old = '            Logger.Error(ex, "Failed to create provider of type {ProviderType}", providerType.Name);'
        New = '            LogCreateProviderFailed(Logger, ex, providerType.Name);'
    }
)

# Apply replacements
foreach ($replacement in $replacements) {
    $content = $content.Replace($replacement.Old, $replacement.New)
}

# Write back to file
$content | Set-Content $filePath -NoNewline

Write-Host "Migration completed for SerializerFactory.cs"
