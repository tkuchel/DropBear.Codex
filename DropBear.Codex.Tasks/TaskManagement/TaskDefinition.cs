#region

using DropBear.Codex.Core.Results.Compatibility;

#endregion

namespace DropBear.Codex.Tasks.TaskManagement;

public delegate Task<Result> TaskDefinition(TaskContext context);
