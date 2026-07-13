namespace SecurityBaselineGUI.Core.Models;

/// <summary>ps1実行1回ぶんの入力。パラメータはps1のparam名と一致させる。</summary>
public sealed class ExecutionRequest
{
    public required string ScriptPath { get; init; }
    public required IReadOnlyDictionary<string, object?> Parameters { get; init; }
    public bool WhatIf { get; init; }
}
