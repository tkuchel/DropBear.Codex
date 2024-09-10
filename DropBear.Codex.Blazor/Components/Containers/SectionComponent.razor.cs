#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A component that renders its content conditionally based on synchronous or asynchronous predicates.
/// </summary>
public sealed partial class SectionComponent : DropBearComponentBase
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<SectionComponent>();
    private bool _isLoading;

    private bool? _shouldRender;

    /// <summary>
    ///     A synchronous predicate function that determines whether the section should be rendered.
    ///     If the predicate is null or returns true, the section will be rendered.
    /// </summary>
    [Parameter]
    public Func<bool>? Predicate { get; set; }

    /// <summary>
    ///     An asynchronous predicate function that determines whether the section should be rendered.
    ///     If the predicate is null or returns true, the section will be rendered after evaluation.
    /// </summary>
    [Parameter]
    public Func<Task<bool>>? AsyncPredicate { get; set; }

    /// <summary>
    ///     The content to be rendered within the section.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///     Validates the component's parameters and initializes necessary properties.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        if (ChildContent is null)
        {
            throw new InvalidOperationException($"{nameof(ChildContent)} must be provided and cannot be null.");
        }

        // If an asynchronous predicate is provided, evaluate it
        if (AsyncPredicate is not null)
        {
            Logger.Debug("Evaluating asynchronous predicate.");
            _isLoading = true;
            try
            {
                _shouldRender = await AsyncPredicate.Invoke();
                Logger.Debug("Asynchronous predicate evaluated to: {ShouldRender}", _shouldRender);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred during asynchronous predicate evaluation.");
                _shouldRender = false;
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }
        else
        {
            // If no async predicate, fall back to synchronous predicate
            _shouldRender = Predicate?.Invoke() ?? true;
            Logger.Debug("Synchronous predicate evaluated to: {ShouldRender}", _shouldRender);
        }

        await base.OnInitializedAsync();
    }

    /// <summary>
    ///     Determines whether the section should render.
    /// </summary>
    private bool ShouldRenderSection()
    {
        // Section should render only if _shouldRender is true and it's not loading.
        return _shouldRender.GetValueOrDefault(true) && !_isLoading;
    }
}
