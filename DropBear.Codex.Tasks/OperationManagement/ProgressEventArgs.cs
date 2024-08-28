namespace DropBear.Codex.Tasks.OperationManagement;

public class ProgressEventArgs(int progressPercentage, string message = "") : EventArgs
{
    public int ProgressPercentage { get; } = progressPercentage;
    public string Message { get; } = message;
}
