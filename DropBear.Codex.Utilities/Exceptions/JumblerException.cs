namespace DropBear.Codex.Utilities.Exceptions;

public class JumblerException : Exception
{
    public JumblerException(string message, Exception innerException) : base(message, innerException) { }
}
