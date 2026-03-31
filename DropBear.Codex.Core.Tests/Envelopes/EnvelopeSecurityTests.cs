using System.Text.Json;
using DropBear.Codex.Core.Envelopes;
using FluentAssertions;

namespace DropBear.Codex.Core.Tests.Envelopes;

public sealed class EnvelopeSecurityTests
{
    [Fact]
    public void Map_ShouldClearSealState_WhenPayloadChanges()
    {
        var envelope = new Envelope<string>("payload", new Dictionary<string, object> { ["tenant"] = "alpha" })
            .Seal(SignEnvelope);

        var mapped = envelope.Map(value => value.Length);

        mapped.IsSealed.Should().BeFalse();
        mapped.SealedAt.Should().BeNull();
        mapped.Signature.Should().BeNull();
    }

    [Fact]
    public void VerifySignature_ShouldFail_WhenHeadersChangeAfterSealing()
    {
        var envelope = new Envelope<string>("payload", new Dictionary<string, object> { ["tenant"] = "alpha" })
            .Seal(SignEnvelope);

        var tamperedDto = envelope.GetDto();
        tamperedDto.Headers!["tenant"] = "beta";
        var tamperedEnvelope = Envelope<string>.FromDto(tamperedDto);

        var result = tamperedEnvelope.VerifySignature(VerifyEnvelopeSignature);

        result.IsSuccess.Should().BeFalse();
    }

    private static string SignEnvelope(Envelope<string> envelope)
    {
        return JsonSerializer.Serialize(envelope.GetDto());
    }

    private static bool VerifyEnvelopeSignature(Envelope<string> envelope, string signature)
    {
        return JsonSerializer.Serialize(envelope.GetDto()) == signature;
    }
}
