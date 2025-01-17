#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A component that conditionally renders its content based on synchronous or asynchronous predicates.
///     Implements enhanced error handling, performance optimizations, and lifecycle management.
/// </summary>
public sealed partial class DropBearSectionComponent : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSectionComponent>();
    private volatile bool _isDisposing;

    // Performance optimization: Use volatile for thread-safe state management
    private volatile bool _isInitialized;

    // Cache validation state to avoid unnecessary re-evaluations
    private bool _isPredicateValid;
    private volatile bool _shouldRenderContent;
    private bool _wasAsyncPredicate;

    /// <summary>
    ///     Determines if the content should be rendered based on initialization and predicate state.
    /// </summary>
    private bool ShouldRenderContent => _isInitialized && _shouldRenderContent && !_isDisposing;

    /// <summary>
    ///     A synchronous predicate function determining if the section should be rendered.
    ///     If null or returns true, the section is rendered.
    /// </summary>
    [Parameter]
    public Func<bool>? Predicate { get; set; }

    /// <summary>
    ///     An asynchronous predicate function determining if the section should be rendered.
    ///     If null or returns true, the section is rendered after evaluation.
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
        if (_isDisposing)
        {
            return;
        }

        try
        {
            var wasValid = _isPredicateValid;
            ValidateParameters();

            // Re-evaluate if:
            // 1. Parameters were previously invalid but are now valid
            // 2. Using sync predicate (async predicates are handled in OnInitializedAsync)
            if ((!wasValid && _isPredicateValid) || (AsyncPredicate is null && Predicate is not null))
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

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposing)
        {
            return;
        }

        _isDisposing = true;
        try
        {
            // Cleanup any resources if needed
            _shouldRenderContent = false;
            _isInitialized = false;

            await base.DisposeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during component disposal");
        }
    }

    /// <summary>
    ///     Evaluates both synchronous and asynchronous predicates.
    ///     Uses ValueTask for better performance with sync predicates.
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
    ///     Validates component parameters and throws if invalid.
    /// </summary>
    private void ValidateParameters()
    {
        try
        {
            if (ChildContent is null)
            {
                throw new InvalidOperationException($"{nameof(ChildContent)} must be provided and cannot be null.");
            }

            if (AsyncPredicate is not null && Predicate is not null)
            {
                throw new InvalidOperationException("Cannot specify both AsyncPredicate and Predicate.");
            }

            // Track if predicate type has changed
            var isCurrentlyAsyncPredicate = AsyncPredicate is not null;
            if (_isPredicateValid && _wasAsyncPredicate != isCurrentlyAsyncPredicate)
            {
                // Force re-evaluation if switching between sync and async predicates
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
    ///     Handles exceptions uniformly across the component.
    /// </summary>
    private async Task HandleExceptionAsync(Exception ex, string context)
    {
        Logger.Error(ex, "{Context}: {Message}", context, ex.Message);

        if (OnError.HasDelegate)
        {
            try
            {
                await OnError.InvokeAsync(ex);
            }
            catch (Exception callbackEx)
            {
                Logger.Error(callbackEx, "Error callback failed");
            }
        }
    }

    /// <summary>
    ///     Notifies listeners of state changes.
    /// </summary>
    private async Task NotifyStateChangeAsync(bool newState)
    {
        if (OnRenderStateChanged.HasDelegate)
        {
            try
            {
                await OnRenderStateChanged.InvokeAsync(newState);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ex, "State change notification failed");
            }
        }
    }
}
