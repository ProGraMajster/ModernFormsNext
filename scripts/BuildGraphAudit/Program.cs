using System.Collections;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

// Replay the SDK's own binary-log format. This utility has no NuGet dependencies
// and never loads/evaluates project code from the log being inspected.
if (args.Length < 2 || args.Length > 3 || (args.Length == 3 && args[2] != "--allow-incremental"))
{
    Console.Error.WriteLine("Usage: BuildGraphAudit <input.binlog> <report.json> [--allow-incremental]");
    return 2;
}

string[] propertyNames = ["BaseIntermediateOutputPath", "IntermediateOutputPath", "BaseOutputPath",
    "OutputPath", "TargetPath", "PublishDir", "MSBuildProjectExtensionsPath", "ProjectAssetsFile",
    "Configuration", "TargetFramework", "RuntimeIdentifier", "SelfContained", "DocumentationFile",
    "EnableWindowsTargeting", "DesignTimeBuild", "BuildingInsideVisualStudio", "ModernFormsNextBuildVariant",
    "DesignerHostPublishDir", "MicroComGeneratedFile", "SkipCompilerExecution"];
string[] writeTasks = ["Csc", "Copy", "CopyRefAssembly", "WriteLinesToFile", "WriteCodeFragment", "Touch",
    "Delete", "RemoveDir", "GenerateDepsFile", "GenerateRuntimeConfigurationFiles", "GenerateGlobalUsings",
    "GenerateMSBuildEditorConfig", "ResolveAssemblyReference", "ResolvePackageAssets", "CreateAppHost", "Exec"];
var evaluations = new Dictionary<string, Dictionary<string, string>>();
var projects = new Dictionary<string, Invocation>();
var runningTasks = new Dictionary<string, WriteOperation>();
var operations = new List<WriteOperation>();
string Key(BuildEventContext c) => $"{c.NodeId}:{c.ProjectContextId}";
string EvaluationKey(BuildEventContext c) => $"{c.NodeId}:{c.EvaluationId}";
Dictionary<string, string> ReadProperties(IEnumerable? values)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (values != null) foreach (var value in values)
    {
        if (value is DictionaryEntry d) result[d.Key.ToString()!] = d.Value?.ToString() ?? "";
        else if (value is KeyValuePair<string, string> p) result[p.Key] = p.Value;
    }
    return result.Where(p => propertyNames.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
}

var source = new BinaryLogReplayEventSource();
source.AnyEventRaised += (_, e) =>
{
    if (e is ProjectEvaluationFinishedEventArgs f && f.BuildEventContext is { } context)
        evaluations[EvaluationKey(context)] = ReadProperties(f.Properties);
};
source.ProjectStarted += (_, e) =>
{
    if (e.BuildEventContext is not { } context) return;
    projects[Key(context)] = new Invocation
    {
        Id = Key(context), Project = e.ProjectFile!, Evaluation = EvaluationKey(context), Targets = e.TargetNames!,
        Properties = ReadProperties(e.Properties),
        Globals = e.GlobalProperties?.ToDictionary(p => p.Key, p => p.Value) ?? new()
    };
};
source.TargetStarted += (_, e) =>
{
    if (e.BuildEventContext is { } context && projects.TryGetValue(Key(context), out var p))
        p.TargetNames.Add(e.TargetName!);
};
source.TaskStarted += (_, e) =>
{
    if (e.BuildEventContext is not { } context || !projects.TryGetValue(Key(context), out var p)) return;
    p.Tasks[e.TaskName!] = p.Tasks.GetValueOrDefault(e.TaskName!) + 1;
    if (writeTasks.Contains(e.TaskName) && !(e.TaskName == "Csc" && p.Globals.GetValueOrDefault("SkipCompilerExecution") == "true"))
        runningTasks[$"{Key(context)}:{context.TaskId}"] = new WriteOperation(p.Id, e.TaskName!, e.Timestamp);
};
source.TaskFinished += (_, e) =>
{
    if (e.BuildEventContext is { } context && runningTasks.Remove($"{Key(context)}:{context.TaskId}", out var operation))
    {
        operation.End = e.Timestamp;
        operations.Add(operation);
    }
};
try
{
    source.Replay(args[0]);
}
catch (Exception e) when (e is IOException or NotSupportedException)
{
    Console.Error.WriteLine($"Cannot audit the binary log: {e.Message}");
    Console.Error.WriteLine("Use a completed log and a compatible MSBuild reader; incomplete evidence cannot pass.");
    return 2;
}
foreach (var p in projects.Values)
{
    if (p.Properties.Count == 0 && evaluations.TryGetValue(p.Evaluation, out var properties)) p.Properties = properties;
    p.Owner = p.Project.ToUpperInvariant() + "|" + string.Join(";", p.Globals.OrderBy(g => g.Key, StringComparer.Ordinal)
        .Select(g => $"{g.Key}={g.Value}"));
}

string FullPath(Invocation p, string property)
{
    var path = p.Properties.GetValueOrDefault(property);
    return string.IsNullOrEmpty(path) ? "" : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, Path.GetDirectoryName(p.Project)!));
}
var failures = new HashSet<string>(StringComparer.Ordinal);
bool Compiles(Invocation p) => p.Tasks.ContainsKey("Csc") && p.Properties.GetValueOrDefault("SkipCompilerExecution") != "true";
bool OwnsBuildOutputs(Invocation p) => Compiles(p) ||
    (p.TargetNames.Contains("Build") && p.Tasks.Keys.Any(writeTasks.Contains));
// Duplicate build owners are unsafe even when a serial log keeps writes apart,
// or incremental/Android builds skip Csc. Query-only NuGet invocations are handled
// by the timing check below; they do not own another complete Build request.
foreach (var property in new[] { "IntermediateOutputPath", "OutputPath", "TargetPath", "DocumentationFile" })
{
    foreach (var group in projects.Values.Where(p => OwnsBuildOutputs(p) && FullPath(p, property) != "")
        .GroupBy(p => FullPath(p, property), StringComparer.OrdinalIgnoreCase))
    {
        if (group.Select(p => p.Owner).Distinct().Count() > 1)
            failures.Add($"Different build instances share {property}: {group.Key}");
    }
}
// Also inspect the timing of SDK bookkeeping writers, including pack queries
// (ResolveReferences can write caches/global usings). We intentionally report
// potential writes even if a particular task might have skipped unchanged files.
foreach (var group in operations.GroupBy(o => FullPath(projects[o.Invocation], "IntermediateOutputPath"), StringComparer.OrdinalIgnoreCase))
{
    if (group.Key == "") continue;
    var ordered = group.OrderBy(o => o.Start).ToArray();
    for (var i = 0; i < ordered.Length; i++)
    {
        var a = ordered[i];
        for (var j = i + 1; j < ordered.Length && ordered[j].Start < a.End; j++)
        {
            var b = ordered[j];
            if (projects[a.Invocation].Owner != projects[b.Invocation].Owner)
                failures.Add($"Overlapping {a.Task}/{b.Task} in {group.Key} ({a.Invocation}, {b.Invocation})");
        }
    }
}
var compilations = projects.Values.Count(Compiles);
var buildOwners = projects.Values.Count(OwnsBuildOutputs);
var missing = projects.Values.Where(p => OwnsBuildOutputs(p) && FullPath(p, "IntermediateOutputPath") == "").ToArray();
if (missing.Length > 0) failures.Add("A build invocation has no evaluated intermediate path; evidence is incomplete.");
if (buildOwners == 0) failures.Add("No build output owner found; evidence is incomplete.");
if (compilations == 0 && !args.Contains("--allow-incremental"))
    failures.Add("No compiler execution found; use a full rebuild log or explicitly allow an incremental audit.");
var report = new { Input = Path.GetFileName(args[0]), Compilations = compilations, BuildOwners = buildOwners, Failures = failures,
    Projects = projects.Values, Operations = operations };
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{compilations} compiler invocations; {projects.Count} total invocations; {failures.Count} isolation failures.");
foreach (var failure in failures) Console.Error.WriteLine(failure);
return failures.Count == 0 ? 0 : 1;

sealed class Invocation
{
    public string Id { get; set; } = "";
    public string Project { get; set; } = "";
    public string Evaluation { get; set; } = "";
    public string Targets { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Owner { get; set; } = "";
    public Dictionary<string, string> Globals { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new();
    public Dictionary<string, int> Tasks { get; set; } = new();
    public HashSet<string> TargetNames { get; set; } = new();
}
sealed class WriteOperation(string invocation, string task, DateTime start)
{
    public string Invocation { get; } = invocation;
    public string Task { get; } = task;
    public DateTime Start { get; } = start;
    public DateTime End { get; set; }
}
