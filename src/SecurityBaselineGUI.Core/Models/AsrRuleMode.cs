namespace SecurityBaselineGUI.Core.Models;

/// <summary>ASRルールのモード。値はDefenderのASRレジストリ値と対応する。</summary>
public enum AsrRuleMode
{
    Disabled = 0,
    Block = 1,
    Audit = 2,
    Warn = 6,
}
