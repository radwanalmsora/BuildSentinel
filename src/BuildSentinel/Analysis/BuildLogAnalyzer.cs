using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BuildSentinel.Analysis
{
    public sealed class BuildDiagnostic
    {
        public string Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string File { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }
        public int Occurrences { get; set; }
        public string Hint { get; set; }
        public int RootCauseScore { get; set; }
    }

    public sealed class BuildAnalysisReport
    {
        public string InputFile { get; set; }
        public DateTime AnalyzedAtUtc { get; set; }
        public int TotalLines { get; set; }
        public int ParsedDiagnostics { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public int UniqueDiagnostics { get; set; }
        public List<BuildDiagnostic> Diagnostics { get; set; }
        public List<BuildDiagnostic> LikelyRootCauses { get; set; }
    }

    public static class BuildLogAnalyzer
    {
        private static readonly Regex DiagnosticRegex = new Regex(
            @"^(?:(?<file>.+?)\((?<line>\d+)(?:,(?<column>\d+))?\):\s*)?(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static BuildAnalysisReport Analyze(string inputFile, string content)
        {
            var parsed = new List<BuildDiagnostic>();
            var lines = (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                var match = DiagnosticRegex.Match(line);
                if (!match.Success) continue;

                int number;
                int? lineNumber = int.TryParse(match.Groups["line"].Value, out number) ? number : (int?)null;
                int? column = int.TryParse(match.Groups["column"].Value, out number) ? number : (int?)null;
                var code = match.Groups["code"].Value.ToUpperInvariant();
                parsed.Add(new BuildDiagnostic
                {
                    Severity = match.Groups["severity"].Value.ToLowerInvariant(),
                    Code = code,
                    Message = match.Groups["message"].Value.Trim(),
                    File = match.Groups["file"].Value.Trim(),
                    Line = lineNumber,
                    Column = column,
                    Occurrences = 1,
                    Hint = GetHint(code)
                });
            }

            var grouped = parsed
                .GroupBy(d => string.Join("|", d.Severity, d.Code, d.Message, d.File, d.Line.HasValue ? d.Line.Value.ToString() : ""))
                .Select(g =>
                {
                    var item = g.First();
                    item.Occurrences = g.Count();
                    item.RootCauseScore = Score(item);
                    return item;
                })
                .OrderByDescending(d => d.RootCauseScore)
                .ThenByDescending(d => d.Occurrences)
                .ThenBy(d => d.Code)
                .ToList();

            return new BuildAnalysisReport
            {
                InputFile = inputFile ?? string.Empty,
                AnalyzedAtUtc = DateTime.UtcNow,
                TotalLines = lines.Length,
                ParsedDiagnostics = parsed.Count,
                Errors = parsed.Count(d => d.Severity == "error"),
                Warnings = parsed.Count(d => d.Severity == "warning"),
                UniqueDiagnostics = grouped.Count,
                Diagnostics = grouped,
                LikelyRootCauses = grouped.Where(d => d.Severity == "error").Take(5).ToList()
            };
        }

        private static int Score(BuildDiagnostic diagnostic)
        {
            var score = diagnostic.Severity == "error" ? 50 : 10;
            if (diagnostic.Code.StartsWith("CS100") || diagnostic.Code == "CS1513" || diagnostic.Code == "CS1514") score += 35;
            if (diagnostic.Code == "CS0246" || diagnostic.Code == "CS0103" || diagnostic.Code == "NU1101" || diagnostic.Code == "NU1102") score += 25;
            if (diagnostic.Code.StartsWith("MSB")) score += 15;
            if (diagnostic.Occurrences > 1) score += Math.Min(20, diagnostic.Occurrences * 2);
            return score;
        }

        private static string GetHint(string code)
        {
            switch (code)
            {
                case "CS0246": return "Type or namespace not found. Check spelling, using directives, project references, and installed packages.";
                case "CS0103": return "Name does not exist in the current context. Check spelling, scope, and whether the declaration is available.";
                case "CS1061": return "The type has no matching member. Check the receiver type, API version, and intended extension namespace.";
                case "CS1002": return "A semicolon may be missing near this location. Inspect the preceding statement first.";
                case "CS1513": return "A closing brace may be missing. Inspect the nearest preceding block or declaration.";
                case "CS1514": return "An opening brace may be missing after a namespace or declaration.";
                case "NU1101": return "NuGet package was not found. Check package ID, configured feeds, network access, and source mapping.";
                case "NU1102": return "A compatible package version was not found. Check version constraints and available feeds.";
                case "MSB4019": return "An imported MSBuild project was not found. Check the SDK/workload installation and imported file path.";
                case "MSB4236": return "The requested .NET SDK could not be resolved. Check global.json and installed SDK versions.";
                default: return diagnosticFallback(code);
            }
        }

        private static string diagnosticFallback(string code)
        {
            if (code.StartsWith("MSB")) return "MSBuild diagnostic. Inspect the full message, imported targets, SDK installation, and project configuration.";
            if (code.StartsWith("NU")) return "NuGet diagnostic. Check package versions, feeds, restore configuration, and network access.";
            if (code.StartsWith("CS")) return "C# compiler diagnostic. Inspect this location and the first related compiler error.";
            return "Review the diagnostic message and nearby build output for the underlying cause.";
        }
    }
}
