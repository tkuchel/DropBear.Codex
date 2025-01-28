namespace DropBear.Codex.Blazor.Helpers;

public static class SvgIcons
{
    public const string Success = @"
            <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                <path d='M20 6L9 17l-5-5'/>
            </svg>";

    public const string Error = @"
            <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                <circle cx='12' cy='12' r='10'/>
                <path d='M12 8v4m0 4h.01'/>
            </svg>";

    public const string Warning = @"
            <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                <path d='M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0zM12 9v4m0 4h.01'/>
            </svg>";

    public const string Information = @"
            <svg width='20' height='20' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'>
                <circle cx='12' cy='12' r='10'/>
                <path d='M12 16v-4m0-4h.01'/>
            </svg>";
}
