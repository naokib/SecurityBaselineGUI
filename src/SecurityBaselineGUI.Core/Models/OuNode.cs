namespace SecurityBaselineGUI.Core.Models;

/// <summary>ADのOU1件(ADツリーピッカーが表示する単位)。</summary>
public sealed record OuNode(string Name, string DistinguishedName);
