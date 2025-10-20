#region

using DropBear.Codex.Workflow.Interfaces;
using DropBear.Codex.Workflow.Results;

#endregion

namespace DropBear.Codex.Workflow.Nodes;

/// <summary>
///     Workflow node that provides conditional branching based on context state.
///     Supports sequential linking to continue workflow execution after the conditional branches.
/// </summary>
public sealed class ConditionalNode<TContext> : WorkflowNodeBase<TContext>, ILinkableNode<TContext>
    where TContext : class
{
    private readonly Func<TContext, bool> _condition;
    private IWorkflowNode<TContext>? _nextNode;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConditionalNode{TContext}" /> class.
    /// </summary>
    /// <param name="condition">The predicate function to evaluate the condition.</param>
    /// <param name="trueNode">The node to execute if the condition evaluates to true.</param>
    /// <param name="falseNode">The node to execute if the condition evaluates to false.</param>
    /// <param name="nodeId">Optional unique identifier for this node.</param>
    /// <exception cref="ArgumentNullException">Thrown when condition is null.</exception>
    public ConditionalNode(
        Func<TContext, bool> condition,
        IWorkflowNode<TContext>? trueNode = null,
        IWorkflowNode<TContext>? falseNode = null,
        string? nodeId = null)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        TrueNode = trueNode;
        FalseNode = falseNode;
        NodeId = nodeId ?? CreateNodeId();
    }

    /// <inheritdoc />
    public override string NodeId { get; }

    /// <summary>
    ///     Gets the node to execute when the condition evaluates to true.
    /// </summary>
    public IWorkflowNode<TContext>? TrueNode { get; }

    /// <summary>
    ///     Gets the node to execute when the condition evaluates to false.
    /// </summary>
    public IWorkflowNode<TContext>? FalseNode { get; }

    /// <inheritdoc />
    public void SetNextNode(IWorkflowNode<TContext>? nextNode)
    {
        _nextNode = nextNode;

        // Link the next node to the end of each branch to ensure
        // workflow execution continues after the conditional branches complete
        LinkNextNodeToBranch(TrueNode, nextNode);
        LinkNextNodeToBranch(FalseNode, nextNode);
    }

    /// <inheritdoc />
    public IWorkflowNode<TContext>? GetNextNode() => _nextNode;

    /// <inheritdoc />
    public override ValueTask<NodeExecutionResult<TContext>> ExecuteAsync(
        TContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            bool conditionResult = _condition(context);

            IWorkflowNode<TContext>? selectedBranch = conditionResult ? TrueNode : FalseNode;
            IWorkflowNode<TContext>[] nextNodes = selectedBranch is not null
                ? [selectedBranch]
                : [];

            var metadata = new Dictionary<string, object>
(StringComparer.OrdinalIgnoreCase)
            {
                ["ConditionResult"] = conditionResult, ["BranchTaken"] = conditionResult ? "True" : "False"
            };

            return ValueTask.FromResult(
                NodeExecutionResult<TContext>.Success(nextNodes, metadata));
        }
        catch (Exception ex)
        {
            var stepResult = StepResult.Failure($"Conditional evaluation failed: {ex.Message}");
            return ValueTask.FromResult(NodeExecutionResult<TContext>.Failure(stepResult));
        }
    }

    /// <summary>
    ///     Links the next node to the terminal nodes within a branch.
    ///     This ensures workflow execution continues properly after conditional branches complete.
    /// </summary>
    /// <param name="branchNode">The root node of the branch to link.</param>
    /// <param name="nextNode">The node to link at the end of the branch.</param>
    private static void LinkNextNodeToBranch(
        IWorkflowNode<TContext>? branchNode,
        IWorkflowNode<TContext>? nextNode)
    {
        if (branchNode is null || nextNode is null)
        {
            return;
        }

        // Find all terminal nodes in the branch and link the next node to them
        IEnumerable<IWorkflowNode<TContext>> terminalNodes = FindTerminalNodes(branchNode);
        foreach (IWorkflowNode<TContext> terminalNode in terminalNodes)
        {
            if (terminalNode is ILinkableNode<TContext> linkableNode)
            {
                linkableNode.SetNextNode(nextNode);
            }
        }
    }

    /// <summary>
    ///     Finds all terminal nodes (nodes without next nodes) in a node graph.
    ///     Uses breadth-first traversal to explore the node graph.
    /// </summary>
    /// <param name="rootNode">The root node to start traversal from.</param>
    /// <returns>A collection of terminal nodes.</returns>
    private static IEnumerable<IWorkflowNode<TContext>> FindTerminalNodes(IWorkflowNode<TContext> rootNode)
    {
        var visited = new HashSet<IWorkflowNode<TContext>>();
        var terminalNodes = new List<IWorkflowNode<TContext>>();
        var queue = new Queue<IWorkflowNode<TContext>>();

        queue.Enqueue(rootNode);

        while (queue.Count > 0)
        {
            IWorkflowNode<TContext> currentNode = queue.Dequeue();

            if (!visited.Add(currentNode))
            {
                continue; // Skip if already visited (prevents cycles)
            }

            // Check if this node is a terminal node
            bool isTerminal = true;

            if (currentNode is ILinkableNode<TContext> linkableNode)
            {
                IWorkflowNode<TContext>? nextNode = linkableNode.GetNextNode();
                if (nextNode is not null)
                {
                    queue.Enqueue(nextNode);
                    isTerminal = false;
                }
            }

            // For parallel nodes, check their branches
            if (currentNode is ParallelNode<TContext> parallelNode)
            {
                foreach (IWorkflowNode<TContext> branch in parallelNode.ParallelNodes)
                {
                    queue.Enqueue(branch);
                }

                isTerminal = false; // Parallel nodes are not terminal; their branches might be
            }

            // For conditional nodes, check their branches
            if (currentNode is ConditionalNode<TContext> conditionalNode)
            {
                if (conditionalNode.TrueNode is not null)
                {
                    queue.Enqueue(conditionalNode.TrueNode);
                }

                if (conditionalNode.FalseNode is not null)
                {
                    queue.Enqueue(conditionalNode.FalseNode);
                }

                isTerminal = false; // Conditional nodes are not terminal; their branches might be
            }

            if (isTerminal)
            {
                terminalNodes.Add(currentNode);
            }
        }

        return terminalNodes;
    }
}
