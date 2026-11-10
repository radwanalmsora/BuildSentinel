using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BuildSentinel.Commands;
using Microsoft.VisualStudio.Shell;

namespace BuildSentinel
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class BuildSentinelPackage : AsyncPackage
    {
        public const string PackageGuidString = "B9F5B1D7-5F18-4F36-9D47-1E0A2A90B671";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await AnalyzeBuildLogCommand.InitializeAsync(this);
        }
    }
}
