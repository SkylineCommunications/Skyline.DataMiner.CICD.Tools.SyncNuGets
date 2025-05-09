﻿namespace CICD.Tools.SyncNuGetsTests
{
    using System;

    using FluentAssertions;

    using Microsoft.Extensions.Configuration;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using NuGet.Versioning;

    using Skyline.DataMiner.CICD.Tools.SyncNuGets;

    [TestClass, Ignore("Can only run on local machine")]
    public class NuGetAccessIntegrationTests
    {
        private IConfiguration Configuration { get; set; }
        private const string nameOfIntegrationPackage = "Skyline_SyncNuGet_IntegrationTestPackage_DoNotRemove";

        public NuGetAccessIntegrationTests()
        {
            // the type specified here is just so the secrets library can 
            // find the UserSecretId we added in the csproj file
            // See README.md for the setup

            var builder = new ConfigurationBuilder()
                .AddUserSecrets<NuGetAccessIntegrationTests>(false);

            Configuration = builder.Build();
        }

        [TestMethod, Ignore("This test can only run once, use it wisely. After that alpha3 will exist and can't be added again. There is no way to make a valid re-usable integration test without first uploading new versions.")]
        public async Task PushMissingNugetAsyncTest()
        {
            string sourceRepo = "https://devcore3/nuget";
            string targetRepo = "https://pkgs.dev.azure.com/skyline-cloud/_packaging/skyline-private-nugets/nuget/v3/index.json";

            string sourceApi = Configuration["SyncNuGetTestsSouceToken"];
            string targetApi = Configuration["SyncNuGetTestsTargetToken"];

            var syncer = new NuGetSyncer(sourceRepo, targetRepo, sourceApi, targetApi);
            await syncer.PushMissingNuGetsAsync(nameOfIntegrationPackage, new NuGetVersion("1.0.3-alpha3"));

            // Don't even think about deleting it. Once it's deleted, it can never be added again due to hard azure limitations.
            // This ran green on 19/03/2024
        }

        [TestMethod]
        public async Task FindMissingNuGetsTest_StableReleases()
        {
            string sourceRepo = "https://devcore3/nuget";
            string targetRepo = "https://pkgs.dev.azure.com/skyline-cloud/_packaging/skyline-private-nugets/nuget/v3/index.json";

            string sourceApi = Configuration["SyncNuGetTestsSouceToken"];
            string targetApi = Configuration["SyncNuGetTestsTargetToken"];

            var syncer = new NuGetSyncer(sourceRepo, targetRepo, sourceApi, targetApi);
            var results = await syncer.FindMissingNuGetsAsync(nameOfIntegrationPackage, false);

            NuGetVersion[] expectedResults = new[]
            {
                new NuGetVersion("1.0.1"),
                new NuGetVersion("1.0.2"),
            };

            results.Should().BeEquivalentTo(expectedResults);
        }

        [TestMethod]
        public async Task FindMissingNuGetsTest_WithPreReleases()
        {
            string sourceRepo = "https://devcore3/nuget";
            string targetRepo = "https://pkgs.dev.azure.com/skyline-cloud/_packaging/skyline-private-nugets/nuget/v3/index.json";

            string sourceApi = Configuration["SyncNuGetTestsSouceToken"];
            string targetApi = Configuration["SyncNuGetTestsTargetToken"];

            var syncer = new NuGetSyncer(sourceRepo, targetRepo, sourceApi, targetApi);
            var results = await syncer.FindMissingNuGetsAsync(nameOfIntegrationPackage, true);

            NuGetVersion[] expectedResults =
            {
                new NuGetVersion("1.0.1"),
                new NuGetVersion("1.0.2"),
                new NuGetVersion("1.0.3-alpha1"),
                new NuGetVersion("1.0.3-alpha2"),
                new NuGetVersion("1.0.3-alpha3"),
            };

            results.Should().BeEquivalentTo(expectedResults);
        }

        [TestMethod]
        public async Task ListAllKnownNuGets()
        {
            string sourceRepo = "https://devcore3/nuget";
            string targetRepo = "https://pkgs.dev.azure.com/skyline-cloud/_packaging/skyline-private-nugets/nuget/v3/index.json";

            string sourceApi = Configuration["SyncNuGetTestsSouceToken"];
            string targetApi = Configuration["SyncNuGetTestsTargetToken"];

            var syncer = new NuGetSyncer(sourceRepo, targetRepo, sourceApi, targetApi);
            var results = await syncer.FindAllKnownPackageNames(true);

            results.Should().NotBeEmpty();
        }

        [TestMethod, Ignore("This is used to manually perform a complete sync but keep track of the execution.")]
        public async Task PerformActualSync()
        {
            string sourceRepo = "https://devcore3/nuget";
            string targetRepo = "https://pkgs.dev.azure.com/skyline-cloud/_packaging/skyline-private-nugets/nuget/v3/index.json";

            string sourceApi = "";// Configuration["SyncNuGetTestsSouceToken"];
            string targetApi = Configuration["SyncNuGetTestsTargetToken"];
            bool success = false;
            string lastException = "";
            for (int i = 0; i < 500; i++)
            {
                if (success) break;
                try
                {
                    var syncer = new NuGetSyncer(sourceRepo, targetRepo, sourceApi, targetApi);
                    var results = await syncer.FindAllKnownPackageNames(true);

                    results.Should().NotBeEmpty();

                    foreach (var toSync in results)
                    {
                        var missingVersions = await syncer.FindMissingNuGetsAsync(toSync, true);

                        if (missingVersions is { Count: > 0 })
                        {
                            await syncer.PushMissingNuGetsAsync(toSync, missingVersions.ToArray());
                        }
                    }

                    success = true;
                }
                catch (Exception ex)
                {
                    lastException = ex.ToString();
                    Thread.Sleep(10000);
                }
            }

            if (!success) throw new InvalidOperationException(lastException);
        }
    }
}