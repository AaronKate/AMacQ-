using System.Collections.Generic;

namespace AMacQConfigEditor.Services;

internal sealed record PackageDeploymentProgress(int CompletedFiles, int TotalFiles, string CurrentTarget)
{
    public double Percentage => TotalFiles == 0 ? 100 : (double)CompletedFiles / TotalFiles * 100;
}

internal sealed record PackageDeploymentResult(IReadOnlyList<string> ExtractedTargets, IReadOnlyList<string> SkippedTargets);