using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using SecurityBaselineGUI.Core.Models;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 仕様書5.4: Runspaceを都度生成してps1を実行し、各ストリームをイベントで中継する。
/// </summary>
public sealed class PowerShellExecutionService
{
    public event EventHandler<ExecutionLogLine>? LogReceived;

    public async Task<ExecutionResult> RunAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.ScriptPath))
        {
            throw new FileNotFoundException("指定されたスクリプトが見つかりません。", request.ScriptPath);
        }

        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddCommand(request.ScriptPath);

        foreach (var (name, value) in request.Parameters)
        {
            if (value is null)
            {
                continue;
            }
            // bool値は switch/[bool] パラメータのどちらに対しても
            // -Name:$true / -Name:$false 形式でそのままバインドできるため、
            // 「trueの時だけフラグとして追加」という特別扱いはしない
            // (以前はswitchのみを想定していたが、既定値$trueの[bool]パラメータでは
            // falseを明示的に渡す必要があるため、この一般化が必須)。
            ps.AddParameter(name, value);
        }

        if (request.WhatIf)
        {
            ps.AddParameter("WhatIf");
        }

        var logBuilder = new StringBuilder();

        ps.Streams.Verbose.DataAdded += (_, e) => Emit("Verbose", ps.Streams.Verbose[e.Index].Message, logBuilder);
        ps.Streams.Warning.DataAdded += (_, e) => Emit("Warning", ps.Streams.Warning[e.Index].Message, logBuilder);
        ps.Streams.Error.DataAdded += (_, e) => Emit("Error", ps.Streams.Error[e.Index].ToString(), logBuilder);
        ps.Streams.Information.DataAdded += (_, e) => Emit("Information", ps.Streams.Information[e.Index].ToString(), logBuilder);

        var stopwatch = Stopwatch.StartNew();
        bool succeeded;
        try
        {
            var results = await Task.Run(() => ps.Invoke(), cancellationToken).ConfigureAwait(false);
            foreach (var item in results)
            {
                Emit("Output", item?.ToString() ?? string.Empty, logBuilder);
            }
            succeeded = !ps.HadErrors;
        }
        catch (Exception ex)
        {
            Emit("Error", ex.Message, logBuilder);
            succeeded = false;
        }
        finally
        {
            stopwatch.Stop();
        }

        return new ExecutionResult(succeeded, stopwatch.Elapsed, logBuilder.ToString());
    }

    private void Emit(string level, string message, StringBuilder logBuilder)
    {
        var line = new ExecutionLogLine(level, message, DateTimeOffset.Now);
        logBuilder.AppendLine($"[{line.Timestamp:HH:mm:ss}] [{level}] {message}");
        LogReceived?.Invoke(this, line);
    }
}
