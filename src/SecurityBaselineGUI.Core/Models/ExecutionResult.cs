namespace SecurityBaselineGUI.Core.Models;

public sealed record ExecutionResult(bool Succeeded, TimeSpan Duration, string LogText);

public sealed record ExecutionLogLine(string Level, string Message, DateTimeOffset Timestamp);
