# To enable tests.
In the test project folder, open commandline and execute

```
dotnet user-secrets init
```

```
dotnet user-secrets set "SyncNuGetTestsSouceToken" "MYPAT_FOR_DEVCORE3_ACCESS"
```

```
dotnet user-secrets set "SyncNuGetTestsTargetToken" "MYPAT_FOR_AZURE_ACCESS"
```