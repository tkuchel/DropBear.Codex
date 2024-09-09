﻿#region

using DropBear.Codex.Blazor.Components.Bases;
using DropBear.Codex.Blazor.Events.EventArgs;
using Microsoft.JSInterop;
using Serilog;

#endregion

namespace DropBear.Codex.Blazor.Services;

public sealed class ThemeManager : DropBearComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<ThemeManager>? objRef;

    public ThemeManager(IJSRuntime jsRuntime)
    {
        JSRuntime = jsRuntime;
        Log.Information("ThemeManager constructor");
    }

    private IJSRuntime JSRuntime { get; } = null!;

    public async ValueTask DisposeAsync()
    {
        await JSRuntime.InvokeVoidAsync("DropBearThemeManager.dispose");
        objRef?.Dispose();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            objRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("DropBearThemeManager.initialize", objRef);
        }
    }

    [JSInvokable]
    public void OnThemeChanged(string effectiveTheme, string userPreference)
    {
        Log.Information($"Theme changed - Effective: {effectiveTheme}, User Preference: {userPreference}");
        InvokeAsync(() => ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(effectiveTheme, userPreference)));
    }

    private Action<Action> InvokeAsync { get; set; } = action => action();

    public void SetInvokeAsync(Action<Action> invokeAsync)
    {
        InvokeAsync = invokeAsync;
    }

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public async Task ToggleThemeAsync()
    {
        await JSRuntime.InvokeVoidAsync("DropBearThemeManager.toggleTheme");
    }

    public async Task SetThemeAsync(string theme)
    {
        await JSRuntime.InvokeVoidAsync("DropBearThemeManager.setTheme", theme);
    }

    public async Task<string> GetCurrentThemeAsync()
    {
        return await JSRuntime.InvokeAsync<string>("DropBearThemeManager.getCurrentTheme");
    }
}
