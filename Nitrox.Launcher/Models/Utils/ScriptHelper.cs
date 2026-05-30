namespace Nitrox.Launcher.Models.Utils;

/// <summary>
///     Helpers for safely embedding filesystem paths into generated shell scripts.
/// </summary>
internal static class ScriptHelper
{
    /// <summary>
    ///     Escapes a path for use inside a PowerShell single-quoted string (<c>'...'</c>).
    ///     Single quotes are escaped by doubling them (<c>'</c> → <c>''</c>).
    /// </summary>
    public static string EscapeForPowerShell(string path) =>
        path.Replace("'", "''");

    /// <summary>
    ///     Escapes a path for use inside a bash double-quoted string (<c>"..."</c>).
    ///     Escapes backslash, double-quote, dollar sign, and backtick.
    /// </summary>
    public static string EscapeForBash(string path) =>
        path.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");
}
