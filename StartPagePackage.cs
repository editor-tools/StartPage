using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using tasks = System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using GitHub.Services;
using GitHub.UI;
using GitHub.ViewModels;
using System.IO;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Git.Controls.Extensibility;

namespace GitHub.StartPage
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Guid(guidPackage)]
    [ProvideCodeContainerProvider("GitHub Container", guidPackage, VisualStudio.Guids.ImagesId, 1, "#110", "#111", typeof(GitHubContainerProvider))]
    public sealed class StartPagePackage : ExtensionPointPackage
    {
        static IServiceProvider serviceProvider;
        internal static IServiceProvider ServiceProvider { get { return serviceProvider; } }

        public const string guidPackage = "3b764d23-faf7-486f-94c7-b3accc44a70d";

        public StartPagePackage()
        {
            serviceProvider = this;
        }

        //protected override tasks.Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        //{
        //    //return tasks.Task.Run(() => GetGlobalService(typeof(IUIProvider)) as IUIProvider);
        //    return base.InitializeAsync(cancellationToken, progress);
        //}
    }

    [Guid(ContainerGuid)]
    public class GitHubContainerProvider : ICodeContainerProvider
    {
        public const string ContainerGuid = "6CE146CB-EF57-4F2C-A93F-5BA685317660";

        public async Task<CodeContainer> AcquireCodeContainerAsync(IProgress<ServiceProgressData> downloadProgress, CancellationToken cancellationToken)
        {
            string path = null;
            try
            {
                var uiProvider = await tasks.Task.Run(() => Package.GetGlobalService(typeof(IUIProvider)) as IUIProvider);
                var te = StartPagePackage.ServiceProvider.GetService(typeof(ITeamExplorer)) as ITeamExplorer;
                var page = te?.NavigateToPage(new Guid(TeamExplorerPageIds.Connect), null);
                var service = page?.GetService<IGitRepositoriesExt>();
                if (service == null)
                    return null;

                uiProvider.AddService(this, service);

                var load = uiProvider.SetupUI(UIControllerFlow.Clone, null);
                load.Subscribe(x =>
                {
                    if (x.Data.ViewType == Exports.UIViewType.Clone)
                    {
                        var vm = x.View.ViewModel as IRepositoryCloneViewModel;
                        x.View.Done.Subscribe(_ => path = Path.Combine(vm.BaseRepositoryPath, vm.SelectedRepository.Name));
                    }
                });
                uiProvider.RunUI();

                uiProvider.RemoveService(typeof(IGitRepositoriesExt), this);
            }
            catch
            {
                // TODO: log
            }

            if (path == null)
                return null;

            return new CodeContainer { LocalPath = path, Provider = new Guid("11B8E6D7-C08B-4385-B321-321078CDD1F8") };
        }

        public Task<CodeContainer> AcquireCodeContainerAsync(CodeContainer onlineCodeContainer, IProgress<ServiceProgressData> downloadProgress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
