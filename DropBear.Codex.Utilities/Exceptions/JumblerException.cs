namespace DropBear.Codex.Utilities.Exceptions;

public sealed class JumblerException : Exception
{
    public JumblerException(string message, Exception innerException) : base(message, innerException) { }
}
