using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using GitHub.Services;
using GitHub.UI;
using GitHub.ViewModels;
using System.IO;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.Git.Controls.Extensibility;
using Microsoft.VisualStudio.Shell.CodeContainerManagement;
using ICodeContainerProvider = Microsoft.VisualStudio.Shell.CodeContainerManagement.ICodeContainerProvider;
using CodeContainer = Microsoft.VisualStudio.Shell.CodeContainerManagement.CodeContainer;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel;

namespace GitHub.StartPage
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Guid(guidPackage)]
    [ProvideCodeContainerProvider("GitHub Container", guidPackage, VisualStudio.Guids.ImagesId, 1, "#110", "#111", typeof(GitHubContainerProvider))]
    public sealed class StartPagePackage : ExtensionPointPackage
    {
        static IServiceProvider serviceProvider;
        internal static IServiceProvider ServiceProvider { get { return serviceProvider; } }

        public const string guidPackage = "3b764d23-faf7-486f-94c7-b3accc44a70e";

        public StartPagePackage()
        {
            serviceProvider = this;
        }
    }

    [Guid(ContainerGuid)]
    public class GitHubContainerProvider : ICodeContainerProvider
    {
        public const string ContainerGuid = "6CE146CB-EF57-4F2C-A93F-5BA685317660";
        public static Guid GitSccProvider = new Guid("11B8E6D7-C08B-4385-B321-321078CDD1F8");

        public async Task<CodeContainer> AcquireCodeContainerAsync(IProgress<ServiceProgressData> downloadProgress, CancellationToken cancellationToken)
        {
            CloneRequest request = null;

            try
            {
                var uiProvider = await Task.Run(() => Package.GetGlobalService(typeof(IUIProvider)) as IUIProvider);
                var gitRepositories = await GetGitRepositoriesExt(uiProvider);
                request = ShowCloneDialog(uiProvider, gitRepositories);
            }
            catch
            {
                // TODO: log
            }

            if (request == null)
                return null;

            var path = Path.Combine(request.BasePath, request.RepositoryName);
            return new CodeContainer(
                localProperties: new CodeContainerLocalProperties(path, CodeContainerType.Folder,
                                new CodeContainerSourceControlProperties(request.RepositoryName, path, GitSccProvider)),
                remote: null,
                isFavorite: false,
                lastAccessed: DateTimeOffset.UtcNow);
        }

        public Task<CodeContainer> AcquireCodeContainerAsync(RemoteCodeContainer onlineCodeContainer, IProgress<ServiceProgressData> downloadProgress, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        async Task<IGitRepositoriesExt> GetGitRepositoriesExt(IUIProvider uiProvider)
        {
            var page = await GetTeamExplorerPage(uiProvider);
            return page?.GetService<IGitRepositoriesExt>();
        }

        async Task<ITeamExplorerPage> GetTeamExplorerPage(IUIProvider uiProvider)
        {
            var te = uiProvider?.GetService(typeof(ITeamExplorer)) as ITeamExplorer;

            if (te != null)
            {
                var page = te.NavigateToPage(new Guid(TeamExplorerPageIds.Connect), null);

                if (page == null)
                {
                    var tcs = new TaskCompletionSource<ITeamExplorerPage>();
                    PropertyChangedEventHandler handler = null;

                    handler = new PropertyChangedEventHandler((s, e) =>
                    {
                        if (e.PropertyName == "CurrentPage")
                        {
                            tcs.SetResult(te.CurrentPage);
                            te.PropertyChanged -= handler;
                        }
                    });

                    te.PropertyChanged += handler;

                    page = await tcs.Task;
                }

                return page;
            }
            else
            {
                // TODO: Log
                return null;
            }
        }

        CloneRequest ShowCloneDialog(IUIProvider uiProvider, IGitRepositoriesExt gitRepositories)
        {
            string basePath = null;
            string repositoryName = null;

            uiProvider.AddService(this, gitRepositories);

            var load = uiProvider.SetupUI(UIControllerFlow.Clone, null);
            load.Subscribe(x =>
            {
                if (x.Data.ViewType == Exports.UIViewType.Clone)
                {
                    var vm = x.View.ViewModel as IRepositoryCloneViewModel;
                    x.View.Done.Subscribe(_ =>
                    {
                        basePath = vm.BaseRepositoryPath;
                        repositoryName = vm.SelectedRepository.Name;
                    });
                }
            });

            uiProvider.RunUI();
            uiProvider.RemoveService(typeof(IGitRepositoriesExt), this);

            return new CloneRequest(basePath, repositoryName);
        }

        class CloneRequest
        {
            public CloneRequest(string basePath, string repositoryName)
            {
                BasePath = basePath;
                RepositoryName = repositoryName;
            }

            public string BasePath { get; }
            public string RepositoryName { get; }
        }
    }
}
