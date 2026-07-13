namespace SecurityBaselineGUI.Core.Services;

/// <summary>AD接続・OU列挙に失敗した際にAdOuBrowserServiceがスローする例外。</summary>
public sealed class AdOuBrowserException : Exception
{
    public AdOuBrowserException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
