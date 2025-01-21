#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using Microsoft.AspNetCore.Components;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A component that conditionally renders its content based on synchronous or asynchronous predicates.
///     Implements enhanced error handling, performance optimizations, and lifecycle management.
/// </summary>
public sealed partial class DropBearSectionComponent : DropBearComponentBase
{
    private const string PREDICATE_CONFLICT_MESSAGE = "Cannot specify both AsyncPredicate and Predicate.";
    private const string MISSING_CONTENT_MESSAGE = "ChildContent must be provided and cannot be null.";
    private volatile bool _isInitialized;

    private bool _isPredicateValid;
    private volatile bool _shouldRenderContent;
    private bool _wasAsyncPredicate;

    /// <summary>
    ///     Determines if the content should be rendered.
    /// </summary>
    private bool ShouldRenderContent => _isInitialized && _shouldRenderContent && !IsDisposed;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            ValidateParameters();
            await EvaluatePredicatesAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Initialization failed");
            _shouldRenderContent = false;
        }

        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            var wasValid = _isPredicateValid;
            ValidateParameters();

            if (ShouldRevaluatePredicates(wasValid))
            {
                await EvaluatePredicatesAsync();
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "Parameter update failed");
            _shouldRenderContent = false;
        }

        await base.OnParametersSetAsync();
    }

    private bool ShouldRevaluatePredicates(bool wasValid)
    {
        return (!wasValid && _isPredicateValid) ||
               (AsyncPredicate is null && Predicate is not null);
    }

    /// <summary>
    ///     Evaluates predicates using ValueTask for better performance.
    /// </summary>
    private async ValueTask<bool> EvaluatePredicateAsync()
    {
        if (AsyncPredicate is not null)
        {
            return await AsyncPredicate();
        }

        return Predicate?.Invoke() ?? true;
    }

    /// <summary>
    ///     Evaluates predicates and updates component state.
    /// </summary>
    private async Task EvaluatePredicatesAsync([CallerMemberName] string caller = "")
    {
        try
        {
            var previousState = _shouldRenderContent;
            _shouldRenderContent = await EvaluatePredicateAsync();

            if (previousState != _shouldRenderContent)
            {
                Logger.Debug(
                    "Render state changed from {PreviousState} to {CurrentState} in {Caller}",
                    previousState,
                    _shouldRenderContent,
                    caller
                );

                await NotifyStateChangeAsync(_shouldRenderContent);
            }
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, $"Predicate evaluation failed in {caller}");
            _shouldRenderContent = false;
        }
    }

    /// <summary>
    ///     Validates component parameters.
    /// </summary>
    private void ValidateParameters()
    {
        try
        {
            if (ChildContent is null)
            {
                throw new InvalidOperationException(MISSING_CONTENT_MESSAGE);
            }

            if (AsyncPredicate is not null && Predicate is not null)
            {
                throw new InvalidOperationException(PREDICATE_CONFLICT_MESSAGE);
            }

            var isCurrentlyAsyncPredicate = AsyncPredicate is not null;
            if (_isPredicateValid && _wasAsyncPredicate != isCurrentlyAsyncPredicate)
            {
                _isPredicateValid = false;
                Logger.Debug("Predicate type changed, forcing re-evaluation");
            }

            _wasAsyncPredicate = isCurrentlyAsyncPredicate;
            _isPredicateValid = true;
        }
        catch
        {
            _isPredicateValid = false;
            throw;
        }
    }

    /// <summary>
    ///     Handles exceptions uniformly.
    /// </summary>
    private async Task HandleExceptionAsync(Exception ex, string context)
    {
        Logger.Error(ex, "{Context}: {Message}", context, ex.Message);

        if (!OnError.HasDelegate)
        {
            return;
        }

        try
        {
            await OnError.InvokeAsync(ex);
        }
        catch (Exception callbackEx)
        {
            Logger.Error(callbackEx, "Error callback failed");
        }
    }

    /// <summary>
    ///     Notifies listeners of state changes.
    /// </summary>
    private async Task NotifyStateChangeAsync(bool newState)
    {
        if (!OnRenderStateChanged.HasDelegate)
        {
            return;
        }

        try
        {
            await InvokeStateHasChangedAsync(async () =>
                await OnRenderStateChanged.InvokeAsync(newState));
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(ex, "State change notification failed");
        }
    }

    #region Parameters

    /// <summary>
    ///     A synchronous predicate function determining if the section should be rendered.
    /// </summary>
    [Parameter]
    public Func<bool>? Predicate { get; set; }

    /// <summary>
    ///     An asynchronous predicate function determining if the section should be rendered.
    /// </summary>
    [Parameter]
    public Func<Task<bool>>? AsyncPredicate { get; set; }

    /// <summary>
    ///     The content to be displayed if the predicates succeed.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Event callback triggered when the render state changes.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnRenderStateChanged { get; set; }

    /// <summary>
    ///     Event callback triggered when an error occurs during predicate evaluation.
    /// </summary>
    [Parameter]
    public EventCallback<Exception> OnError { get; set; }

    #endregion
}
