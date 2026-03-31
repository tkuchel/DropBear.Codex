using System.Text.Json;
using DropBear.Codex.Core.Envelopes;
using FluentAssertions;

namespace DropBear.Codex.Core.Tests.Envelopes;

public sealed class CompositeEnvelopeSecurityTests
{
    [Fact]
    public void Where_ShouldClearSealState_WhenPayloadSetChanges()
    {
        var envelope = new CompositeEnvelope<string>(
                ["alpha", "beta"],
                new Dictionary<string, object> { ["tenant"] = "alpha" })
            .Seal(SignEnvelope);

        var filtered = envelope.Where(value => value == "alpha");

        filtered.IsSealed.Should().BeFalse();
        filtered.SealedAt.Should().BeNull();
        filtered.Signature.Should().BeNull();
    }

    [Fact]
    public void VerifySignature_ShouldFail_WhenHeadersChangeAfterSealing()
    {
        var envelope = new CompositeEnvelope<string>(
                ["alpha", "beta"],
                new Dictionary<string, object> { ["tenant"] = "alpha" })
            .Seal(SignEnvelope);

        var tamperedEnvelope = new CompositeEnvelope<string>(
            envelope.Payloads,
            new Dictionary<string, object> { ["tenant"] = "beta" })
        {
        };

        var tamperedSealed = tamperedEnvelope.Seal(_ => envelope.Signature!);
        var result = tamperedSealed.VerifySignature(VerifyEnvelopeSignature);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_ShouldPass_ForUntamperedCompositeEnvelope()
    {
        var envelope = new CompositeEnvelope<string>(
                ["alpha", "beta"],
                new Dictionary<string, object> { ["tenant"] = "alpha" })
            .Seal(SignEnvelope);

        var result = envelope.VerifySignature(VerifyEnvelopeSignature);

        result.IsSuccess.Should().BeTrue();
    }

    private static string SignEnvelope(CompositeEnvelope<string> envelope)
    {
        return JsonSerializer.Serialize(new
        {
            Payloads = envelope.Payloads.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Headers = envelope.Headers.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            envelope.IsSealed,
            envelope.CreatedAt,
            envelope.SealedAt
        });
    }

    private static bool VerifyEnvelopeSignature(CompositeEnvelope<string> envelope, string signature)
    {
        return SignEnvelope(envelope) == signature;
    }
}
