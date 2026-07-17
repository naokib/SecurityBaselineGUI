namespace SecurityBaselineGUI.Core.Models;

public sealed record OperationJournalEntry(
    string OperationId,
    string ProductId,
    string OperationName,
    string Stage,
    string ConsistencyState,
    string ExternalDependency,
    string CorrelationKey,
    string Message,
    DateTimeOffset OccurredAt,
    bool RecoveryRequired,
    string RepairPlan);

