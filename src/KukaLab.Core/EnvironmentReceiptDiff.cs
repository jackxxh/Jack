namespace KukaLab.Core;

public sealed record EnvironmentReceiptDiffResult
{
    public string BaselineReceiptId { get; init; } = string.Empty;

    public string CurrentReceiptId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool HasDifferences { get; init; }

    public List<EnvironmentReceiptDifference> Differences { get; init; } = [];

    public List<string> Errors { get; init; } = [];
}

public sealed record EnvironmentReceiptDifference
{
    public string Scope { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string? Baseline { get; init; }

    public string? Current { get; init; }
}

public static class EnvironmentReceiptDiffer
{
    public static EnvironmentReceiptDiffResult Compare(
        EnvironmentInventoryReceipt baseline,
        EnvironmentInventoryReceipt current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);

        var baselineVerification = EnvironmentReceiptVerifier.Verify(baseline);
        var currentVerification = EnvironmentReceiptVerifier.Verify(current);
        var errors = baselineVerification.Errors
            .Select(error => $"baseline: {error}")
            .Concat(currentVerification.Errors.Select(error => $"current: {error}"))
            .ToList();
        if (errors.Count > 0)
        {
            return new EnvironmentReceiptDiffResult
            {
                BaselineReceiptId = baseline.Payload.ReceiptId,
                CurrentReceiptId = current.Payload.ReceiptId,
                Succeeded = false,
                Errors = errors
            };
        }

        var differences = new List<EnvironmentReceiptDifference>();
        AddDifference(
            differences,
            "environment",
            "terminalClassification",
            baseline.Payload.TerminalClassification.ToString(),
            current.Payload.TerminalClassification.ToString());
        AddDifference(
            differences,
            "environment",
            "assetRoot",
            baseline.Payload.AssetRoot,
            current.Payload.AssetRoot);
        AddDifference(
            differences,
            "implementation",
            "coreAssemblySha256",
            baseline.Payload.CoreAssemblySha256,
            current.Payload.CoreAssemblySha256);

        CompareKeyed(
            differences,
            "check",
            baseline.Payload.Checks.ToDictionary(check => check.Id, FormatCheck, StringComparer.Ordinal),
            current.Payload.Checks.ToDictionary(check => check.Id, FormatCheck, StringComparer.Ordinal));
        CompareKeyed(
            differences,
            "file",
            baseline.Payload.Files.ToDictionary(FileKey, FormatFile, StringComparer.OrdinalIgnoreCase),
            current.Payload.Files.ToDictionary(FileKey, FormatFile, StringComparer.OrdinalIgnoreCase));
        CompareSet(differences, "blocking-gap", baseline.Payload.BlockingGaps, current.Payload.BlockingGaps);
        CompareSet(differences, "warning", baseline.Payload.Warnings, current.Payload.Warnings);

        var ordered = differences
            .OrderBy(difference => difference.Scope, StringComparer.Ordinal)
            .ThenBy(difference => difference.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new EnvironmentReceiptDiffResult
        {
            BaselineReceiptId = baseline.Payload.ReceiptId,
            CurrentReceiptId = current.Payload.ReceiptId,
            Succeeded = true,
            HasDifferences = ordered.Count > 0,
            Differences = ordered
        };
    }

    private static void CompareKeyed(
        List<EnvironmentReceiptDifference> differences,
        string scope,
        IReadOnlyDictionary<string, string> baseline,
        IReadOnlyDictionary<string, string> current)
    {
        foreach (var key in baseline.Keys.Concat(current.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            baseline.TryGetValue(key, out var baselineValue);
            current.TryGetValue(key, out var currentValue);
            AddDifference(differences, scope, key, baselineValue, currentValue);
        }
    }

    private static void CompareSet(
        List<EnvironmentReceiptDifference> differences,
        string scope,
        IEnumerable<string> baseline,
        IEnumerable<string> current)
    {
        var baselineSet = baseline.ToHashSet(StringComparer.Ordinal);
        var currentSet = current.ToHashSet(StringComparer.Ordinal);
        foreach (var value in baselineSet.Concat(currentSet).Distinct(StringComparer.Ordinal))
        {
            AddDifference(
                differences,
                scope,
                value,
                baselineSet.Contains(value) ? "Present" : null,
                currentSet.Contains(value) ? "Present" : null);
        }
    }

    private static void AddDifference(
        List<EnvironmentReceiptDifference> differences,
        string scope,
        string key,
        string? baseline,
        string? current)
    {
        if (string.Equals(baseline, current, StringComparison.Ordinal))
        {
            return;
        }

        differences.Add(new EnvironmentReceiptDifference
        {
            Scope = scope,
            Key = key,
            Baseline = baseline,
            Current = current
        });
    }

    private static string FormatCheck(EnvironmentCheck check) =>
        $"{check.Status}|{check.Detail}";

    private static string FileKey(EnvironmentFileObservation file) =>
        $"{file.Id}|{file.Path}";

    private static string FormatFile(EnvironmentFileObservation file) => string.Join(
        '|',
        file.Exists,
        file.Bytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        file.Sha256 ?? string.Empty,
        file.FileVersion ?? string.Empty,
        file.ProductVersion ?? string.Empty);
}
