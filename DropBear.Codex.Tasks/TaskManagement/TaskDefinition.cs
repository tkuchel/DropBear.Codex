#region

using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Tasks.Errors;

#endregion

namespace DropBear.Codex.Tasks.TaskManagement;

public delegate Task<Result<Unit, TaskExecutionError>> TaskDefinition(TaskContext context);
