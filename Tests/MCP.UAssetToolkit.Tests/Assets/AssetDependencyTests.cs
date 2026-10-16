using System.Text.Json;
using MCP_UAsset_Toolkit.Source.Models;
using MCP_UAsset_Toolkit.Source.Services;
using MCP_UAsset_Toolkit.Source.Tools;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using Xunit;

namespace MCP.UAssetToolkit.Tests;

[Collection("Asset sessions")]
public sealed class AssetDependencyTests
{
    [Fact]
    public void GetDependencies_ResolvesSignedImportAndExportReferences()
    {
        var (path, session) = RegisterAsset();
        try
        {
            var dependencies = AssetDependencyService.GetDependencies(session).Dependencies;
            var import = Assert.Single(dependencies, dependency => dependency.Category == "dependsMap" && dependency.RawIndex == -1);
            Assert.Equal("import", import.Resolution);
            Assert.Equal(1, import.TargetIndex);
            Assert.Equal("ImportedThing", import.TargetName);
            Assert.Equal("/Game/ImportedPackage", import.PackageName);

            var export = Assert.Single(dependencies, dependency => dependency.Category == "dependsMap" && dependency.RawIndex == 2);
            Assert.Equal("export", export.Resolution);
            Assert.Equal(2, export.TargetIndex);
            Assert.Equal("SecondExport", export.TargetName);
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public void GetDependencies_ReportsInvalidAndNullIndexesWithoutThrowing()
    {
        var (path, session) = RegisterAsset();
        try
        {
            var dependencies = AssetDependencyService.GetDependencies(session).Dependencies;
            Assert.Contains(dependencies, dependency => dependency.RawIndex == 99 && dependency.Resolution == "invalid");
            Assert.Contains(dependencies, dependency => dependency.RawIndex == -99 && dependency.Resolution == "invalid");
            Assert.Contains(dependencies, dependency => dependency.RawIndex == 0 && dependency.Resolution == "null");
            foreach (var unresolved in dependencies.Where(dependency => dependency.Resolution is "invalid" or "null"))
            {
                Assert.Null(unresolved.TargetIndex);
                Assert.Null(unresolved.TargetName);
                Assert.Null(unresolved.PackageName);
            }
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public void GetDependencies_IncludesEveryHeaderAndDependencyCategory()
    {
        var (path, session) = RegisterAsset();
        try
        {
            var categories = AssetDependencyService.GetDependencies(session).Dependencies.Select(dependency => dependency.Category).ToHashSet(StringComparer.Ordinal);
            Assert.Subset(categories, new HashSet<string>(StringComparer.Ordinal)
            {
                "dependsMap", "exportHeader", "importHeader", "serializationBeforeSerialization",
                "createBeforeSerialization", "serializationBeforeCreate", "createBeforeCreate",
                "softPackageReference", "extraPackageDependency"
            });
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public void GetDependencies_FiltersByExportAndPaginates()
    {
        var (path, session) = RegisterAsset();
        try
        {
            var all = AssetDependencyService.GetDependencies(session);
            var filtered = AssetDependencyService.GetDependencies(session, exportIndex: 1, offset: 1, limit: 2);
            Assert.Equal(2, filtered.Dependencies.Count);
            Assert.Equal(1, filtered.Offset);
            Assert.Equal(2, filtered.Limit);
            Assert.True(filtered.TotalCount > filtered.Dependencies.Count);
            Assert.True(filtered.HasMore);
            Assert.All(filtered.Dependencies, dependency => Assert.Equal(1, dependency.SourceExportIndex));
            Assert.True(all.TotalCount > filtered.TotalCount);
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    [Fact]
    public async Task GetAssetDependenciesAsync_IncludesSoftReferencesAndKeepsSessionClean()
    {
        var (path, session) = RegisterAsset();
        try
        {
            using var response = JsonDocument.Parse(await AssetDependencyTools.GetAssetDependenciesAsync(path));
            var data = response.RootElement.GetProperty("data");
            var dependencies = data.GetProperty("dependencies").EnumerateArray().ToList();
            Assert.True(response.RootElement.GetProperty("success").GetBoolean());
            Assert.False(data.GetProperty("isDirty").GetBoolean());
            Assert.False(session.IsDirty);
            Assert.Contains(dependencies, dependency => dependency.GetProperty("category").GetString() == "softPackageReference" && dependency.GetProperty("targetName").GetString() == "/Game/SoftPackage");
            Assert.Contains(dependencies, dependency => dependency.GetProperty("category").GetString() == "extraPackageDependency" && dependency.GetProperty("targetName").GetString() == "/Game/ExtraPackage");
        }
        finally { AssetSessionService.CloseSession(path, discardChanges: true); }
    }

    private static (string Path, AssetSession Session) RegisterAsset()
    {
        var path = Path.Combine(Path.GetTempPath(), $"asset-dependencies-{Guid.NewGuid():N}.uasset");
        var asset = new UAsset { Imports = [], Exports = [] };
        var first = new RawExport { ObjectName = FName.DefineDummy(asset, "FirstExport"), OuterIndex = new FPackageIndex(0), ClassIndex = FPackageIndex.FromImport(0), SuperIndex = FPackageIndex.FromExport(1), TemplateIndex = new FPackageIndex(0) };
        first.SerializationBeforeSerializationDependencies = [FPackageIndex.FromImport(0)];
        first.CreateBeforeSerializationDependencies = [FPackageIndex.FromExport(1)];
        first.SerializationBeforeCreateDependencies = [new FPackageIndex(99)];
        first.CreateBeforeCreateDependencies = [new FPackageIndex(-99)];
        var second = new RawExport { ObjectName = FName.DefineDummy(asset, "SecondExport"), OuterIndex = FPackageIndex.FromExport(0), ClassIndex = new FPackageIndex(0), SuperIndex = new FPackageIndex(0), TemplateIndex = new FPackageIndex(0) };
        asset.Exports.Add(first);
        asset.Exports.Add(second);
        asset.Imports.Add(new Import
        {
            ObjectName = FName.DefineDummy(asset, "ImportedThing"),
            PackageName = FName.DefineDummy(asset, "/Game/ImportedPackage"),
            OuterIndex = FPackageIndex.FromExport(0),
            ClassName = FName.DefineDummy(asset, "Object"),
            ClassPackage = FName.DefineDummy(asset, "/Script/CoreUObject")
        });
        asset.DependsMap = [[-1, 2, 99, -99], []];
        asset.SoftPackageReferenceList = [new FString("/Game/SoftPackage")];
        asset.ExtraPackageDependencies = [new KeyValuePair<FName, uint>(FName.DefineDummy(asset, "/Game/ExtraPackage"), 0)];
        return (path, AssetSessionService.RegisterSession(path, asset));
    }
}
