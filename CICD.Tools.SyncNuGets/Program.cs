namespace Skyline.DataMiner.CICD.Tools.SyncNuGets
{
    using System;
    using System.CommandLine;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Will push every missing nuget version from a nuget source to a nuget destination.
    /// </summary>
    public static class Program
    {
        /*
         * Design guidelines for command line tools: https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax#design-guidance
         */

        /// <summary>
        /// Code that will be called when running the tool.
        /// </summary>
        /// <param name="args">Extra arguments.</param>
        /// <returns>0 if successful.</returns>
        public static async Task<int> Main(string[] args)
        {
            var packageName = new Option<string>(
                name: "--package-name",
                description: "Name of the package to sync.")
            {
                IsRequired = true
            };

            var packageSource = new Option<string>(
            name: "--package-source",
            description: "Source url of the package store.")
            {
                IsRequired = true
            };

            var packageTarget = new Option<string>(
            name: "--package-target",
            description: "Target url of the package store to push into.")
            {
                IsRequired = true
            };

            var sourceApiKey = new Option<string>(
            name: "--package-source-token",
            description: "Token to access the source package store.")
            {
                IsRequired = true
            };

            var targetApiKey = new Option<string>(
            name: "--package-target-token",
            description: "Token to allow pushing into the target package store.")
            {
                IsRequired = true
            };

            var includePrereleases = new Option<bool>(
            name: "--include-prerelease",
            description: "When true, pre-releases will also be synced.")
            {
                IsRequired = false
            };

            includePrereleases.SetDefaultValue(false);

            var rootCommand = new RootCommand("Will push every missing nuget version from a nuget source to a nuget destination")
            {
                packageName,
                packageSource,
                packageTarget,
                sourceApiKey,
                targetApiKey,
                includePrereleases
            };

            rootCommand.SetHandler(Process, packageName, packageSource, packageTarget, sourceApiKey, targetApiKey, includePrereleases);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task<int> Process(string packageName, string sourceStore, string targetStore, string sourceApiKey, string targetApiKey, bool includePrereleases)
        {
            try
            {
                NuGetSyncer syncer = new NuGetSyncer(sourceStore, targetStore, sourceApiKey, targetApiKey);
                var missingVersions = await syncer.FindMissingNuGetsAsync(packageName, includePrereleases);

                if (missingVersions != null && missingVersions.Any())
                {
                    await syncer.PushMissingNuGetsAsync(packageName, missingVersions.ToArray());
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}