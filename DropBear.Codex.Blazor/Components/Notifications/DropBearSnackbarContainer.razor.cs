#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Models;

#endregion

namespace DropBear.Codex.Blazor.Components.Notifications;

public partial class DropBearSnackbarContainer : DropBearComponentBase
{
    private const int MaxSnackbars = 5;
    private readonly List<SnackbarInstance> _activeSnackbars = new();
    private readonly SemaphoreSlim? _semaphore = new(1, 1);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SafeJsVoidInteropAsync("DropBearSnackbar.initialize", ComponentId);
            SnackbarService.OnShow += ShowSnackbar;
        }
    }

    private async Task ShowSnackbar(SnackbarInstance snackbar)
    {
        try
        {
            await _semaphore?.WaitAsync()!;

            // Remove oldest if we're at max capacity
            while (_activeSnackbars.Count >= MaxSnackbars)
            {
                var oldestId = _activeSnackbars[0].Id;
                await RemoveSnackbar(oldestId);
            }

            _activeSnackbars.Add(snackbar);
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            _semaphore?.Release();
        }
    }

    private async Task RemoveSnackbar(string id)
    {
        try
        {
            await _semaphore?.WaitAsync()!;

            var snackbar = _activeSnackbars.FirstOrDefault(s => s.Id == id);
            if (snackbar != null)
            {
                _activeSnackbars.Remove(snackbar);
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            _semaphore?.Release();
        }
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            SnackbarService.OnShow -= ShowSnackbar;
            _semaphore?.Dispose();
            await SafeJsVoidInteropAsync("DropBearSnackbar.dispose", ComponentId);
        }

        await base.DisposeAsync(disposing);
    }
}
