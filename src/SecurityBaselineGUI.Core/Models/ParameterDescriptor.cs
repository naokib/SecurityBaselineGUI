namespace SecurityBaselineGUI.Core.Models;

/// <summary>
/// ps1のparam()ブロックをAST解析して得られる、1パラメータぶんの記述子。
/// UiSchema.jsonによる表示上書き前の「生の」情報を表す。
/// </summary>
public sealed class ParameterDescriptor
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public bool IsMandatory { get; init; }
    public bool IsSwitch { get; init; }
    public string[]? ValidateSetValues { get; init; }
    public int? ValidateRangeMin { get; init; }
    public int? ValidateRangeMax { get; init; }
    public string? DefaultValueExpression { get; init; }
    public string? HelpMessage { get; init; }
}
