using System.Management.Automation.Language;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// ps1のparam()ブロックをASTで解析し、ParameterDescriptorの一覧を返す。
/// 仕様書5.1: これにより将来ps1へパラメータが追加/変更されても、
/// アプリ側は動的に検出できる(実際のGUIコントロールへの反映は上位層の責務)。
/// </summary>
public sealed class ScriptParameterService
{
    public IReadOnlyList<ParameterDescriptor> GetParameters(string scriptPath)
    {
        var ast = Parser.ParseFile(scriptPath, out _, out var errors);
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                $"'{scriptPath}' の構文解析に失敗しました: {string.Join("; ", errors.Select(e => e.Message))}");
        }

        var paramBlock = ast.ParamBlock
            ?? ast.EndBlock?.Statements.OfType<FunctionDefinitionAst>().FirstOrDefault()?.Body.ParamBlock;

        if (paramBlock is null)
        {
            return [];
        }

        var helpMap = (ast as ScriptBlockAst)?.GetHelpContent()?.Parameters
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return paramBlock.Parameters
            .Select(p => ToDescriptor(p, helpMap))
            .ToList();
    }

    private static ParameterDescriptor ToDescriptor(ParameterAst p, IDictionary<string, string> helpMap)
    {
        var name = p.Name.VariablePath.UserPath;
        var typeName = p.StaticType.Name;
        var isSwitch = typeName == "SwitchParameter";

        var isMandatory = p.Attributes
            .OfType<AttributeAst>()
            .Any(a => a.TypeName.Name == "Parameter" &&
                      a.NamedArguments.Any(n => n.ArgumentName == "Mandatory"));

        string[]? validateSet = p.Attributes
            .OfType<AttributeAst>()
            .FirstOrDefault(a => a.TypeName.Name == "ValidateSet")
            ?.PositionalArguments
            .Select(a => a.SafeGetValue()?.ToString() ?? string.Empty)
            .ToArray();

        int? rangeMin = null, rangeMax = null;
        var rangeAttr = p.Attributes.OfType<AttributeAst>().FirstOrDefault(a => a.TypeName.Name == "ValidateRange");
        if (rangeAttr is { PositionalArguments.Count: 2 })
        {
            rangeMin = rangeAttr.PositionalArguments[0].SafeGetValue() as int?;
            rangeMax = rangeAttr.PositionalArguments[1].SafeGetValue() as int?;
        }

        helpMap.TryGetValue(name, out var help);

        return new ParameterDescriptor
        {
            Name = name,
            TypeName = typeName,
            IsMandatory = isMandatory,
            IsSwitch = isSwitch,
            ValidateSetValues = validateSet is { Length: > 0 } ? validateSet : null,
            ValidateRangeMin = rangeMin,
            ValidateRangeMax = rangeMax,
            DefaultValueExpression = p.DefaultValue?.Extent.Text,
            HelpMessage = help,
        };
    }
}
