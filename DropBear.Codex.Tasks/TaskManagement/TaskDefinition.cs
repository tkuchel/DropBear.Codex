using DropBear.Codex.Core;
using DropBear.Codex.Core.Results.Compatibility;

namespace DropBear.Codex.Tasks.TaskManagement;

public delegate Task<Result> TaskDefinition(TaskContext context);
