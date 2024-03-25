namespace Skyline.DataMiner.CICD.Tools.SyncNuGets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using NuGet.Configuration;
    using NuGet.Packaging.Core;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;

    /// <summary>
    /// Provides functionality for synchronizing NuGet packages between two NuGet repositories.
    /// </summary>
    internal class NuGetSyncer
    {
        private readonly SourceRepository sourceRepository;
        private readonly string targetApiKey;
        private readonly SourceRepository targetRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetSyncer"/> class.
        /// </summary>
        /// <param name="sourceStore">The URL of the source package store.</param>
        /// <param name="targetStore">The URL of the target package store.</param>
        /// <param name="sourceApiKey">The API key for the source store.</param>
        /// <param name="targetApiKey">The API key for the target store.</param>
        public NuGetSyncer(string sourceStore, string targetStore, string sourceApiKey, string targetApiKey)
        {
            this.targetApiKey = targetApiKey;
            PackageSource source = new PackageSource(sourceStore);
            PackageSource target = new PackageSource(targetStore);
            source.Credentials = new PackageSourceCredential(sourceStore, "az", sourceApiKey, true, null);
            target.Credentials = new PackageSourceCredential(targetStore, "az", targetApiKey, true, null);

            sourceRepository = Repository.Factory.GetCoreV3(source);
            targetRepository = Repository.Factory.GetCoreV3(target);
        }

        /// <summary>
        /// Finds NuGet packages that exist in the source repository but not in the target repository.
        /// </summary>
        /// <param name="packageName">The name of the package to check.</param>
        /// <param name="includePrerelease">Whether to include prerelease versions in the search.</param>
        /// <returns>A task that represents the asynchronous search operation and returns the missing package versions.</returns>
        public async Task<ICollection<NuGetVersion>> FindMissingNuGetsAsync(string packageName, bool includePrerelease)
        {
            FindPackageByIdResource sourceSearch = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();
            FindPackageByIdResource targetSearch = await targetRepository.GetResourceAsync<FindPackageByIdResource>();
            using (SourceCacheContext cacheContext = new SourceCacheContext { NoCache = true })
            {
                MyLogger log = new MyLogger();
                using (CancellationTokenSource tokenSource = new CancellationTokenSource())
                {

                    // get all versions from source
                    var allSourceVersions = await sourceSearch.GetAllVersionsAsync(packageName, cacheContext, log, tokenSource.Token);

                    // get all versions from target
                    var allTargetVersions = await targetSearch.GetAllVersionsAsync(packageName, cacheContext, log, tokenSource.Token);

                    // Filter the unknown versions
                    var unknownVersions = allSourceVersions.Except(allTargetVersions).ToList();

                    if (includePrerelease)
                    {
                        return unknownVersions;
                    }
                    else
                    {
                        return unknownVersions.Where(p => !p.IsPrerelease).ToArray();
                    }
                }
            }
        }

        /// <summary>
        /// Pushes missing NuGet packages to the target repository.
        /// </summary>
        /// <param name="packageName">The name of the package to push.</param>
        /// <param name="versions">The versions of the package to push.</param>
        /// <returns>A task that represents the asynchronous push operation.</returns>
        public async Task PushMissingNuGetsAsync(string packageName, params NuGetVersion[] versions)
        {
            var sourceSearch = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();
            var targetPush = await targetRepository.GetResourceAsync<PackageUpdateResource>();

            using (SourceCacheContext cacheContext = new SourceCacheContext { NoCache = true })
            {
                MyLogger log = new MyLogger();
                using (CancellationTokenSource tokenSource = new CancellationTokenSource())
                {
                    List<string> packageFilePaths = new List<string>();

                    try
                    {
                        foreach (var version in versions)
                        {
                            PackageIdentity id = new PackageIdentity(packageName, version);

                            var downloader = await sourceSearch.GetPackageDownloaderAsync(id, cacheContext, log, tokenSource.Token);

                            if (downloader == null)
                            {
                                throw new InvalidOperationException($"Could not download package {packageName} with version {version}");
                            }

                            string timeFileName = Path.GetRandomFileName();

                            if (await downloader.CopyNupkgFileToAsync(timeFileName, tokenSource.Token))
                            {
                                packageFilePaths.Add(timeFileName);
                            }
                            else
                            {
                                Console.WriteLine($"Failed to download: {packageName} with version {version}");
                            }
                        }

                        await targetPush.Push(packageFilePaths, null, 5 * 60, false, _ => targetApiKey, _ => null, false, false, null, log);
                    }
                    finally
                    {
                        foreach (var file in packageFilePaths)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
        }
    }
}