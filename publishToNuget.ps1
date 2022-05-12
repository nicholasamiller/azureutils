param ($packageFile)

& dotnet nuget push $packageFile --api-key $nugetKey --source https://api.nuget.org/v3/index.json