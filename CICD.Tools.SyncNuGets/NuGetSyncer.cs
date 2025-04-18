﻿namespace Skyline.DataMiner.CICD.Tools.SyncNuGets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using NuGet.Common;
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
            if (!String.IsNullOrWhiteSpace(sourceApiKey))
            {
                source.Credentials = new PackageSourceCredential(sourceStore, "az", sourceApiKey, true, null);
            }

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
            MyLogger log = new MyLogger();

            using SourceCacheContext cacheContext = new SourceCacheContext { NoCache = true };
            using CancellationTokenSource tokenSource = new CancellationTokenSource();

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

            return unknownVersions.Where(p => !p.IsPrerelease).ToArray();
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

            MyLogger log = new MyLogger();
            using SourceCacheContext cacheContext = new SourceCacheContext { NoCache = true };
            using CancellationTokenSource tokenSource = new CancellationTokenSource();

            List<string> packageFilePaths = new List<string>();
            List<string> originalVersions = new List<string>();
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
                        originalVersions.Add(version.ToString());
                    }
                    else
                    {
                        Console.WriteLine($"Failed to download: {packageName} with version {version}");
                    }
                }

                bool success = false;
                Stopwatch maxLoop = Stopwatch.StartNew();
                TimeSpan timeout = TimeSpan.FromMinutes(5);
                while (!success && maxLoop.Elapsed < timeout)
                {
                    try
                    {
                        await targetPush.Push(packageFilePaths, null, 5 * 60, false, _ => targetApiKey, _ => null, false, false, null, log);
                    }
                    catch (Exception ex)
                    {
                        string exString = ex.ToString();
                        if (!exString.Contains("409"))
                        {
                            throw;
                        }

                        var regex = new Regex(@"\b\d+\.\d+\.\d+(?:-[\w\d]+)?\b");
                        var match = regex.Match(exString);
                        if (!match.Success)
                        {
                            throw;
                        }

                        int found = originalVersions.IndexOf(match.Value);
                        var pathToRemove = packageFilePaths[found];
                        File.Delete(pathToRemove);
                        packageFilePaths.RemoveAt(found);
                        originalVersions.RemoveAt(found);
                        Console.WriteLine($"Skipping Previous Deleted version: {match.Value}");
                        continue;
                    }

                    success = true;
                }

                maxLoop.Stop();
            }
            finally
            {
                foreach (var file in packageFilePaths)
                {
                    File.Delete(file);
                }
            }
        }

        public async Task<List<string>> FindAllKnownPackageNames(bool includePrereleases)
        {
            // Get the SearchAutocompleteResource from the source repository.
            var packageSearchResource = await sourceRepository.GetResourceAsync<PackageSearchResource>();

            int skip = 0;
            const int take = 100; // page size
            List<string> packageIds = new List<string>();
            Stopwatch maxLoop = Stopwatch.StartNew();
            SearchFilter searchFilter = new SearchFilter(includePrerelease: includePrereleases);

            TimeSpan maxTime = new(0, 2, 0);
            while (maxLoop.Elapsed < maxTime)
            {
                // An empty search query "" to match all packages.
                var packages = (await packageSearchResource.SearchAsync(
                String.Empty,
                searchFilter,
                skip: skip,
                take: take,
                NullLogger.Instance,
                CancellationToken.None))?.ToList();

                // If no more packages are returned, stop paging.
                if (packages == null || packages.Count == 0)
                {
                    break;
                }

                // Print out each package ID
                packageIds.AddRange(packages.Select(package => package.Identity.Id));
                skip += take;
            }

            if (maxLoop.Elapsed >= maxTime)
            {
                throw new InvalidOperationException($"Exceeded Maximum Loop Time {maxTime} for requesting package id's from source. Loop-Safety triggered.");
            }

            maxLoop.Stop();
            // Remove duplicates (if any) and return.
            return packageIds.Distinct().ToList();
        }
    }
}