namespace DropBear.Codex.Workflow.Interfaces;

/// <summary>
///     Interface for workflow nodes that support sequential linking.
///     Replaces reflection-based node linking with explicit, type-safe linking.
/// </summary>
/// <typeparam name="TContext">The type of workflow context</typeparam>
public interface ILinkableNode<TContext> where TContext : class
{
    /// <summary>
    ///     Sets the next node to execute after this node completes successfully.
    /// </summary>
    /// <param name="nextNode">The node to execute next. Can be null to indicate workflow completion.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the node does not support setting a next node (e.g., already configured).
    /// </exception>
    void SetNextNode(IWorkflowNode<TContext>? nextNode);

    /// <summary>
    ///     Gets the next node that will execute after this node, if any.
    /// </summary>
    /// <returns>The next node, or null if this is a terminal node</returns>
    IWorkflowNode<TContext>? GetNextNode();
}
