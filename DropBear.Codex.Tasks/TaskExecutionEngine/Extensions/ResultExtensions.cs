#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;

#endregion

namespace DropBear.Codex.Tasks.TaskExecutionEngine.Extensions;

public static class ResultExtensions
{
    #region ExecutionEngine Error Handling

    public static Result<Unit, TaskExecutionError> ToTaskError(
        this Result<Unit, TaskExecutionError> result,
        string taskName,
        string message)
    {
        if (result.IsSuccess)
        {
            return result;
        }

        return Result<Unit, TaskExecutionError>.Failure(
            new TaskExecutionError(
                $"{message}: {result.Error?.Message}",
                taskName,
                result.Error?.Exception));
    }

    #endregion
}
