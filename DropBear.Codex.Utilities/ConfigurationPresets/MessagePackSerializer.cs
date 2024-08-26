namespace DropBear.Codex.Utilities.ConfigurationPresets;

/// <summary>
///     Provides preset configurations for <see cref="MessagePackSerializerOptions" /> used in MessagePack serialization.
/// </summary>
public static class MessagePackSerializerPresets
{
    /// <summary>
    /// Creates a new instance of <see cref="MessagePackSerializerOptions"/> with default settings.
    /// </summary>
    /// <returns>A <see cref="MessagePackSerializerOptions"/> instance with predefined settings.</returns>
    // public static MessagePackSerializerOptions CreateDefaultOptions()
    // {
    //     var options = MessagePackSerializerOptions.Standard
    //         .WithResolver(CompositeResolver.Create(
    //             ImmutableCollectionResolver.Instance,
    //             StandardResolverAllowPrivate.Instance,
    //             StandardResolver.Instance
    //         ))
    //         .WithSecurity(MessagePackSecurity.UntrustedData);
    //
    //     return options;
    // }
}
