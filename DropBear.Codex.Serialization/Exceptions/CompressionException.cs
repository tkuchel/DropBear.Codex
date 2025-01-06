namespace DropBear.Codex.Serialization.Exceptions;

public sealed class CompressionException : Exception
{
    public CompressionException(string message) : base(message)
    {
    }

    public CompressionException()
    {
    }

    public CompressionException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
