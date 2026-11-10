using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BuildSentinel.Analysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Design;
using Newtonsoft.Json;

namespace BuildSentinel.Commands
{
    internal sealed class AnalyzeBuildLogCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("4D1C0D3A-3D1C-4F52-9C7A-9B0F8A2C6D11");
        private readonly AsyncPackage package;

        private AnalyzeBuildLogCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package;
            var menuCommandId = new CommandID(CommandSet, CommandId);
            commandService.AddCommand(new MenuCommand(Execute, menuCommandId));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null) new AnalyzeBuildLogCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                using (var open = new OpenFileDialog())
                {
                    open.Title = "Select a saved Visual Studio build log";
                    open.Filter = "Build log files (*.txt;*.log)|*.txt;*.log|All files (*.*)|*.*";
                    open.Multiselect = false;
                    if (open.ShowDialog() != DialogResult.OK) return;

                    var content = File.ReadAllText(open.FileName);
                    var report = BuildLogAnalyzer.Analyze(open.FileName, content);
                    ShowReport(report);
                    OfferExport(report);
                }
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(package, "BuildSentinel could not analyze the selected file.\n\n" + ex.Message,
                    "BuildSentinel", OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private void ShowReport(BuildAnalysisReport report)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var text = new StringBuilder();
            text.AppendLine("BuildSentinel analysis complete");
            text.AppendLine("--------------------------------");
            text.AppendLine("File: " + report.InputFile);
            text.AppendLine("Lines scanned: " + report.TotalLines);
            text.AppendLine("Errors: " + report.Errors + " | Warnings: " + report.Warnings);
            text.AppendLine("Parsed diagnostics: " + report.ParsedDiagnostics + " | Unique: " + report.UniqueDiagnostics);
            text.AppendLine();
            text.AppendLine("Likely root-cause candidates (heuristic ranking):");
            if (!report.LikelyRootCauses.Any()) text.AppendLine("  No error diagnostics were parsed from this log.");
            foreach (var d in report.LikelyRootCauses)
            {
                text.AppendLine("  [" + d.Code + "] " + d.Message);
                if (!string.IsNullOrWhiteSpace(d.File)) text.AppendLine("    " + d.File + (d.Line.HasValue ? "(" + d.Line + ")" : ""));
                text.AppendLine("    Hint: " + d.Hint);
            }
            VsShellUtilities.ShowMessageBox(package, text.ToString(), "BuildSentinel — Build Analysis",
                OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void OfferExport(BuildAnalysisReport report)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var choice = VsShellUtilities.ShowMessageBox(package, "Would you like to export this analysis?\n\nYes: JSON report\nNo: HTML report\nCancel: Do not export",
                "BuildSentinel — Export Report", OLEMSGICON.OLEMSGICON_QUERY, OLEMSGBUTTON.OLEMSGBUTTON_YESNOCANCEL, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            if (choice == (int)DialogResult.Cancel) return;

            var json = choice == (int)DialogResult.Yes;
            using (var save = new SaveFileDialog())
            {
                save.Title = "Save BuildSentinel report";
                save.Filter = json ? "JSON report (*.json)|*.json" : "HTML report (*.html)|*.html";
                save.FileName = "buildsentinel-report" + (json ? ".json" : ".html");
                if (save.ShowDialog() != DialogResult.OK) return;
                var output = json ? JsonConvert.SerializeObject(report, Formatting.Indented) : BuildHtml(report);
                File.WriteAllText(save.FileName, output, new UTF8Encoding(false));
                VsShellUtilities.ShowMessageBox(package, "Report saved successfully:\n" + save.FileName, "BuildSentinel",
                    OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private static string BuildHtml(BuildAnalysisReport report)
        {
            var rows = new StringBuilder();
            foreach (var d in report.Diagnostics)
            {
                rows.Append("<tr><td>").Append(E(d.Severity)).Append("</td><td>").Append(E(d.Code)).Append("</td><td>")
                    .Append(E(d.Message)).Append("</td><td>").Append(E(d.File)).Append("</td><td>")
                    .Append(d.Line.HasValue ? d.Line.Value.ToString() : "").Append("</td><td>").Append(d.Occurrences)
                    .Append("</td><td>").Append(E(d.Hint)).Append("</td></tr>");
            }
            return "<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>" +
                "<title>BuildSentinel Report</title><style>body{font:14px Segoe UI,Arial;background:#101722;color:#e7edf5;margin:32px}" +
                "h1{color:#54d6e8} .stats{display:flex;gap:12px;flex-wrap:wrap}.card{background:#1b2736;padding:14px 20px;border-radius:8px}" +
                "table{border-collapse:collapse;width:100%;margin-top:24px}th,td{padding:10px;border-bottom:1px solid #344255;text-align:left;vertical-align:top}" +
                "th{color:#54d6e8}td{overflow-wrap:anywhere}.muted{color:#a9b6c6}</style></head><body><h1>BuildSentinel</h1>" +
                "<p class='muted'>Local build log analysis · " + E(report.AnalyzedAtUtc.ToString("u")) + "</p><div class='stats'>" +
                "<div class='card'>Errors: <b>" + report.Errors + "</b></div><div class='card'>Warnings: <b>" + report.Warnings + "</b></div>" +
                "<div class='card'>Unique diagnostics: <b>" + report.UniqueDiagnostics + "</b></div></div><p>Input: " + E(report.InputFile) + "</p>" +
                "<table><thead><tr><th>Severity</th><th>Code</th><th>Message</th><th>File</th><th>Line</th><th>Count</th><th>Hint</th></tr></thead><tbody>" +
                rows + "</tbody></table><p class='muted'>Root-cause ranking is heuristic and should be verified by the developer.</p></body></html>";
        }

        private static string E(string value)
        {
            return System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }
    }
}
