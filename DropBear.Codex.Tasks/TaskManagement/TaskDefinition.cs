using DropBear.Codex.Core;

namespace DropBear.Codex.Tasks.TaskManagement;

public delegate Task<Result> TaskDefinition(TaskContext context);
