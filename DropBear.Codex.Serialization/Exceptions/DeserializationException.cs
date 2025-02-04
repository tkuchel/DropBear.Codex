﻿namespace DropBear.Codex.Serialization.Exceptions;

public sealed class DeserializationException : Exception
{
    public DeserializationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public DeserializationException()
    {
    }

    public DeserializationException(string message) : base(message)
    {
    }
}
