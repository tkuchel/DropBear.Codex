#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Core.Logging;
using Microsoft.AspNetCore.Components;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Components.Containers;

/// <summary>
///     A component that conditionally renders its content based on synchronous or asynchronous predicates.
/// </summary>
public sealed partial class DropBearSectionComponent : DropBearComponentBase
{
    private new static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DropBearSectionComponent>();

    private bool? _shouldRender;
    private bool ShouldRenderContent => _shouldRender == true;

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

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (ChildContent is null)
        {
            throw new InvalidOperationException($"{nameof(ChildContent)} must be provided and cannot be null.");
        }

        try
        {
            if (AsyncPredicate is not null)
            {
                Logger.Debug("Evaluating asynchronous predicate for DropBearSectionComponent.");
                _shouldRender = await AsyncPredicate.Invoke();
                Logger.Debug("Asynchronous predicate result: {ShouldRender}", _shouldRender);
            }
            else
            {
                _shouldRender = Predicate?.Invoke() ?? true;
                Logger.Debug("Synchronous predicate result: {ShouldRender}", _shouldRender);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error in DropBearSectionComponent predicate evaluation.");
            _shouldRender = false;
        }

        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (AsyncPredicate is null && Predicate is not null)
        {
            try
            {
                _shouldRender = Predicate.Invoke();
                Logger.Debug("Re-evaluated synchronous predicate: {ShouldRender}", _shouldRender);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error re-evaluating synchronous predicate in DropBearSectionComponent.");
                _shouldRender = false;
            }
        }
    }
}
