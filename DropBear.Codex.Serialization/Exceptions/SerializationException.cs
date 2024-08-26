namespace DropBear.Codex.Serialization.Exceptions;

public class SerializationException : Exception
{
    public SerializationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public SerializationException()
    {
    }

    public SerializationException(string message) : base(message)
    {
    }
}
