namespace Medusa.Tests

open System
open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
open Medusa
open Medusa.Types

[<TestClass>]
type ImportMapTests() =

    [<TestMethod>]
    member this.``install should create proper request with packages``() =
        // This test would be better as an integration test, but we can test the shape
        // Arrange
        let packages = Set.ofList [ "react"; "vue" ]
        let options = []

        // For unit testing we would need to mock the HTTP client
        // For now, we'll test that the function signature is correct

        // Act & Assert
        let installFunc = ImportMap.install options packages
        Assert.IsNotNull(installFunc)
        // The function should be a CancellableTask<GeneratorResponse>
        Assert.IsTrue(
            typeof<CancellationToken -> System.Threading.Tasks.Task<GeneratorResponse>>
                .IsAssignableFrom(installFunc.GetType())
        )

    [<TestMethod>]
    member this.``update should accept ImportMap and packages``() =
        // Arrange
        let importMap =
            { imports = Map.ofList [ ("react", "https://example.com/react.js") ]
              scopes = Map.empty
              integrity = Map.empty }

        let packages = Set.ofList [ "vue" ]
        let options = []

        // Act & Assert
        let updateFunc = ImportMap.update options importMap packages
        Assert.IsNotNull(updateFunc)
        // The function should be a CancellableTask<GeneratorResponse>
        Assert.IsTrue(
            typeof<CancellationToken -> System.Threading.Tasks.Task<GeneratorResponse>>
                .IsAssignableFrom(updateFunc.GetType())
        )

    [<TestMethod>]
    member this.``uninstall should accept ImportMap and packages``() =
        // Arrange
        let importMap =
            { imports =
                Map.ofList
                    [ ("react", "https://example.com/react.js")
                      ("vue", "https://example.com/vue.js") ]
              scopes = Map.empty
              integrity = Map.empty }

        let packages = Set.ofList [ "vue" ]
        let options = []

        // Act & Assert
        let uninstallFunc = ImportMap.uninstall options importMap packages
        Assert.IsNotNull(uninstallFunc)
        // The function should be a CancellableTask<GeneratorResponse>
        Assert.IsTrue(
            typeof<CancellationToken -> System.Threading.Tasks.Task<GeneratorResponse>>
                .IsAssignableFrom(uninstallFunc.GetType())
        )

    [<TestMethod>]
    member this.``download should accept ImportMap and options``() =
        // Arrange
        let importMap =
            { imports = Map.ofList [ ("react", "https://ga.jspm.io/npm:react@18.2.0/index.js") ]
              scopes = Map.empty
              integrity = Map.empty }

        let options = []

        // Act & Assert
        let downloadFunc = ImportMap.download options importMap
        Assert.IsNotNull(downloadFunc)
        // The function should be a CancellableTask<Map<string, DownloadPackage>>
        Assert.IsTrue(
            typeof<CancellationToken -> System.Threading.Tasks.Task<Map<string, DownloadPackage>>>
                .IsAssignableFrom(downloadFunc.GetType())
        )

    [<TestMethod>]
    member this.``toOffline should accept ImportMap and options``() =
        // Arrange
        let importMap =
            { imports = Map.ofList [ ("react", "https://ga.jspm.io/npm:react@18.2.0/index.js") ]
              scopes = Map.empty
              integrity = Map.empty }

        let options = []

        // Act & Assert
        let toOfflineFunc = ImportMap.toOffline options importMap
        Assert.IsNotNull(toOfflineFunc)
        // The function should be a CancellableTask<ImportMap>
        Assert.IsTrue(
            typeof<CancellationToken -> System.Threading.Tasks.Task<ImportMap>>
                .IsAssignableFrom(toOfflineFunc.GetType())
        )

    [<TestMethod>]
    member this.``ImportMap creation should work with empty maps``() =
        // Arrange & Act
        let importMap =
            { imports = Map.empty
              scopes = Map.empty
              integrity = Map.empty }

        // Assert
        Assert.AreEqual<int>(0, importMap.imports.Count)
        Assert.AreEqual<int>(0, importMap.scopes.Count)
        Assert.AreEqual<int>(0, importMap.integrity.Count)

    [<TestMethod>]
    member this.``ImportMap creation should work with populated maps``() =
        // Arrange & Act
        let imports =
            Map.ofList
                [ ("react", "https://example.com/react.js")
                  ("vue", "https://example.com/vue.js") ]

        let scopes =
            Map.ofList [ ("scope1", Map.ofList [ ("lodash", "https://example.com/lodash.js") ]) ]

        let integrity = Map.ofList [ ("react", "sha384-abc123") ]

        let importMap =
            { imports = imports
              scopes = scopes
              integrity = integrity }

        // Assert
        Assert.AreEqual<int>(2, importMap.imports.Count)
        Assert.AreEqual<int>(1, importMap.scopes.Count)
        Assert.AreEqual<int>(1, importMap.integrity.Count)
        Assert.AreEqual<string>("https://example.com/react.js", importMap.imports["react"])
        Assert.AreEqual<string>("https://example.com/lodash.js", importMap.scopes["scope1"]["lodash"])
        Assert.AreEqual<string>("sha384-abc123", importMap.integrity["react"])
