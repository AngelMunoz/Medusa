namespace Medusa.Tests

open System
open System.Collections.Generic
open Microsoft.VisualStudio.TestTools.UnitTesting
open Medusa
open Medusa.Types

[<TestClass>]
type TypesTests() =

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert BaseUrl correctly``() =
        // Arrange
        let baseUrl = Uri("https://example.com/")
        let options = [ BaseUrl baseUrl ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("baseUrl"))
        Assert.AreEqual<string>("https://example.com/", result["baseUrl"] :?> string)

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert DefaultProvider correctly``() =
        // Arrange
        let options = [ DefaultProvider Provider.JspmIo ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("defaultProvider"))
        Assert.AreEqual<string>("jspm.io", result["defaultProvider"] :?> string)

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert custom Provider correctly``() =
        // Arrange
        let options = [ DefaultProvider(Provider.Custom "custom-provider") ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("defaultProvider"))
        Assert.AreEqual<string>("custom-provider", result["defaultProvider"] :?> string)

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert Cache option correctly``() =
        // Arrange
        let options = [ Cache(CacheOption.Enabled true) ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("cache"))
        Assert.AreEqual<bool>(true, result["cache"] :?> bool)

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert offline Cache option correctly``() =
        // Arrange
        let options = [ Cache CacheOption.Offline ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("cache"))
        Assert.AreEqual<string>("offline", result["cache"] :?> string)

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert Env option correctly``() =
        // Arrange
        let envSet = Set.ofList [ ExportCondition.Development; ExportCondition.Browser ]

        let options = [ Env envSet ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("env"))
        let envResult = result["env"] :?> Set<string>
        Assert.IsTrue(envResult.Contains("development"))
        Assert.IsTrue(envResult.Contains("browser"))
        Assert.AreEqual<int>(2, envResult.Count)

    [<TestMethod>]
    member this.``GeneratorOption toDict should handle custom ExportCondition``() =
        // Arrange
        let envSet = Set.ofList [ ExportCondition.Custom "custom-env" ]
        let options = [ Env envSet ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("env"))
        let envResult = result["env"] :?> Set<string>
        Assert.IsTrue(envResult.Contains("custom-env"))

    [<TestMethod>]
    member this.``GeneratorOption toDict should convert multiple options correctly``() =
        // Arrange
        let options =
            [ BaseUrl(Uri("https://example.com/"))
              FlattenScopes true
              CombineSubPaths false ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.AreEqual<int>(3, result.Count)
        Assert.IsTrue(result.ContainsKey("baseUrl"))
        Assert.IsTrue(result.ContainsKey("flattenScopes"))
        Assert.IsTrue(result.ContainsKey("combineSubPaths"))
        Assert.AreEqual<string>("https://example.com/", result["baseUrl"] :?> string)
        Assert.AreEqual<bool>(true, result["flattenScopes"] :?> bool)
        Assert.AreEqual<bool>(false, result["combineSubPaths"] :?> bool)

    [<TestMethod>]
    member this.``GeneratorOption toDict should handle Providers map``() =
        // Arrange
        let providers = Map.ofList [ ("react", "jsdelivr"); ("lodash", "unpkg") ]
        let options = [ Providers providers ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("providers"))
        let providersResult = result["providers"] :?> Map<string, string>
        Assert.AreEqual<string>("jsdelivr", providersResult["react"])
        Assert.AreEqual<string>("unpkg", providersResult["lodash"])

    [<TestMethod>]
    member this.``GeneratorOption toDict should handle Ignore set``() =
        // Arrange
        let ignoreSet = Set.ofList [ "node_modules"; ".git" ]
        let options = [ Ignore ignoreSet ]

        // Act
        let result = GeneratorOption.toDict options

        // Assert
        Assert.IsTrue(result.ContainsKey("ignore"))
        let ignoreResult = result["ignore"] :?> Set<string>
        Assert.IsTrue(ignoreResult.Contains("node_modules"))
        Assert.IsTrue(ignoreResult.Contains(".git"))
        Assert.AreEqual<int>(2, ignoreResult.Count)
