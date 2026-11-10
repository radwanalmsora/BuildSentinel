# BuildSentinel

**Intelligent Build Failure Analyzer for Visual Studio 2022**  
Find the root cause. Fix it before it ships.

BuildSentinel is a Visual Studio 2022 VSIX extension starter with a working, local build-log analyzer. It parses saved MSBuild / C# / .NET build logs, groups duplicate diagnostics, highlights likely root-cause patterns, and exports JSON or HTML reports. All analysis is performed locally; no API key, account, server, or telemetry is required.

> **Current scope:** this release analyzes a build log file that you save/export from Visual Studio. It does not yet hook into every automatic Build/Rebuild operation. The root-cause ranking is heuristic, not an AI proof of causality.

## Features

- Parse common MSBuild and C# compiler diagnostic lines (`file(line,column): error CODE: message`, `error CODE: message`, and warning equivalents).
- Count errors and warnings, group repeated diagnostics, and rank likely root-cause candidates.
- Add practical hints for common errors such as `CS0246`, `CS0103`, `CS1061`, `CS1002`, `CS1513`, `NU1101`, and `MSB...`.
- Export a machine-readable JSON report or a standalone HTML report.
- Run fully offline and keep build logs on your machine.

## Requirements

- Windows
- Visual Studio 2022 (17.x)
- Visual Studio extension development workload
- .NET Framework 4.7.2 Developer Pack
- .NET SDK compatible with the installed Visual Studio 2022 version

## Build the VSIX

1. Extract this ZIP.
2. Open `src/BuildSentinel/BuildSentinel.csproj` in Visual Studio 2022.
3. If prompted, install/enable **Visual Studio extension development** from Visual Studio Installer.
4. Restore NuGet packages.
5. Build the project in **Release** configuration.
6. The generated `.vsix` will be under `src/BuildSentinel/bin/Release/`.
7. Double-click the `.vsix` to install it, then restart Visual Studio.

This environment did not have the .NET SDK or Visual Studio build tools, so the ZIP contains the complete VSIX source project rather than a precompiled `.vsix` binary. A GitHub Actions workflow is included at `.github/workflows/build-vsix.yml`; after pushing the project to a GitHub repository, run **Actions → Build Visual Studio Extension → Run workflow** and download the `BuildSentinel-VSIX` artifact.

## Use it

1. In Visual Studio, choose **Tools → Analyze Build Log with BuildSentinel**.
2. Select a text log file (`.txt` or `.log`) containing MSBuild / compiler output. You can copy the contents of Visual Studio's Build output window into a text file.
3. Review the summary and choose whether to export a JSON or HTML report.

## Project layout

```text
BuildSentinel/
├── README.md
├── LICENSE
└── src/BuildSentinel/
    ├── Analysis/BuildLogAnalyzer.cs
    ├── Commands/AnalyzeBuildLogCommand.cs
    ├── Properties/AssemblyInfo.cs
    ├── BuildSentinelPackage.cs
    ├── BuildSentinel.csproj
    ├── BuildSentinel.vsct
    └── source.extension.vsixmanifest
```

## Roadmap

- Capture build events automatically through Visual Studio/MSBuild integration.
- Compare consecutive builds and flag newly introduced diagnostics.
- Add solution/project dependency diagnostics.
- Add Roslyn-powered source analysis and previewable safe code fixes.
- Add test/build regression history.

## Privacy

BuildSentinel processes the selected log locally. It does not transmit source code, logs, diagnostics, or project metadata. It has no telemetry implementation.

## License

MIT. See [LICENSE](LICENSE).
