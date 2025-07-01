namespace Medusa

open System
open System.IO
open System.Text.Json
open System.Threading
open Microsoft.Extensions.Logging
open FsHttp
open IcedTasks
open Medusa
open Medusa.Types
open System.Threading.Tasks

module ImportMap =

  type ImportMapServiceArgs = {
    reqHandler: RequestHandler.JspmService
    logger: ILogger
  }

  let private cachePath =
    lazy
      Path.Combine(
        Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData,
        "medusa",
        "v1",
        "store"
      )

  let private localCachePath =
    lazy Path.Combine(Directory.GetCurrentDirectory(), "node_modules")

  /// strictly speaking, these are not node modules; however, I think
  /// it might help with existing tooling trying to discover the sources
  [<Literal>]
  let private LOCAL_CACHE_PREFIX = "/node_modules"

  // Use shared JSON options for consistent serialization/deserialization
  let private jsonOptions: Lazy<JsonSerializerOptions> = JsonOptions.shared

  let private extractPackagesWithScopes(map: ImportMap) =
    let imports = map.imports |> Map.values
    let scopeImports = map.scopes |> Map.values |> Seq.collect Map.values

    [
      for value in [| yield! imports; yield! scopeImports |] do
        let uri = Uri(value)

        match Provider.extractFromUri uri with
        | Ok package -> package
        | Error _ -> ()
    ]
    |> Set

  let private cacheResponse
    (logger: ILogger)
    (response: Map<string, DownloadPackage>)
    =
    cancellableTask {
      let cacheDir = Directory.CreateDirectory(cachePath.Value)
      let localCacheDir = Directory.CreateDirectory(localCachePath.Value)

      let medusaDir =
        Directory.CreateDirectory(
          Path.Combine(localCacheDir.FullName, ".medusa")
        )

      logger.LogDebug(
        "Caching downloaded packages to: {cacheDir}",
        cacheDir.FullName
      )

      logger.LogTrace("Working with Download Map: {downloadMap}", response)

      let tasks =
        response
        |> Map.toArray
        |> Array.map(fun (package, content) -> asyncEx {
          let! token = Async.CancellationToken
          let medusaPkgPath = Path.Combine(medusaDir.FullName, package)
          let localPkgTarget = Path.Combine(cacheDir.FullName, package)

          // Extract the package name without a version for flat structure
          let packageName =
            if package.StartsWith("@") then
              // Scoped package: @scope/package@version -> @scope/package
              let parts = package.Split('@')

              if parts.Length > 2 then
                "@" + parts[1] + "@" + parts[2]
              else
                package
            else
              // Regular package: package@version -> package
              let parts = package.Split('@')
              if parts.Length > 1 then parts[0] else package

          let flatPkgPath = Path.Combine(localCacheDir.FullName, packageName)

          // Create Parent Directories for the medusa path
          Path.GetDirectoryName(medusaPkgPath)
          |> nonNull
          |> Directory.CreateDirectory
          |> ignore

          // Create Parent Directories for flat path
          Path.GetDirectoryName(flatPkgPath)
          |> nonNull
          |> Directory.CreateDirectory
          |> ignore

          // If the medusa store already has the package, skip creating the symbolic link
          if Directory.Exists(medusaPkgPath) then
            logger.LogDebug(
              "Package '{package}' already exists in medusa store, skipping symbolic link creation.",
              package
            )
          else
            logger.LogDebug(
              "Creating symlink to store: {medusaPkgPath} -> {localPkgTarget}",
              medusaPkgPath,
              localPkgTarget
            )

            Directory.CreateSymbolicLink(medusaPkgPath, localPkgTarget)
            |> ignore

          // Create flat symlink if it doesn't exist
          if Directory.Exists(flatPkgPath) then
            logger.LogDebug(
              "Flat package '{packageName}' already exists, skipping flat symlink creation.",
              packageName
            )
          else
            logger.LogDebug(
              "Creating flat symlink: {flatPkgPath} -> {medusaPkgPath}",
              flatPkgPath,
              medusaPkgPath
            )

            Directory.CreateSymbolicLink(flatPkgPath, medusaPkgPath) |> ignore

          if Directory.Exists(Path.Combine(cacheDir.FullName, package)) then
            logger.LogDebug(
              "Package '{package}' already exists, skipping download.",
              package
            )

            return ()
          else
            logger.LogDebug("Downloading package '{package}'...", package)

            logger.LogDebug(
              "Working through {content.files.Length} files...",
              content.files.Length
            )

            for file in content.files do
              let filePath = Path.Combine(cacheDir.FullName, package, file)
              let downloadUri = Uri(content.pkgUrl, file)

              let! response =
                get(downloadUri.ToString())
                |> Config.timeoutInSeconds 10
                |> Config.cancellationToken token
                |> Request.sendAsync

              use! content = response |> Response.toStreamAsync

              Directory.CreateDirectory(
                Path.GetDirectoryName filePath |> nonNull |> Path.GetFullPath
              )
              |> ignore

              use file = File.OpenWrite filePath

              do! content.CopyToAsync(file, cancellationToken = token)

              logger.LogTrace("Downloaded file: {filePath}", filePath)

            return ()
        })

      do! Async.Parallel tasks |> Async.Ignore
      return ()
    }

  let private download
    (dependencies: ImportMapServiceArgs)
    (options: DownloadOption seq)
    (map: ImportMap)
    =
    cancellableTask {
      let! token = CancellableTask.getCancellationToken()
      let packages = extractPackagesWithScopes map

      let {
            reqHandler = reqHandler
            logger = logger
          } =
        dependencies

      let options = [
        for option in options do
          match option with
          | Provider provider ->
            "provider",
            match provider with
            | JspmIo -> "jspm.io"
            | JsDelivr -> "jsdelivr"
            | Unpkg -> "unpkg"
          | Exclude excludes ->
            "exclude",
            [|
              for exclude in excludes ->
                match exclude with
                | Unused -> "unused"
                | Types -> "types"
                | SourceMaps -> "sourcemaps"
                | Readme -> "readme"
                | License -> "license"
            |]
            |> String.concat ","
      ]

      let packages = packages |> String.concat ","

      logger.LogTrace("Downloading packages: {packages}", packages)
      logger.LogTrace("Download options: {options}", options)

      let! response =
        reqHandler.Download(packages, options, cancellationToken = token)

      match response with
      | DownloadError err ->
        return raise(Exception $"Download failed: {err.error}")
      | DownloadSuccess response ->
        logger.LogDebug(
          "Download Success: {count} packages downloaded",
          response.Count
        )

        return response
    }

  let install
    (dependencies: ImportMapServiceArgs)
    (options: GeneratorOption seq)
    (packages: Set<string>)
    =
    cancellableTask {
      let! token = CancellableTask.getCancellationToken()
      let { reqHandler = reqHandler } = dependencies

      let finalOptions = GeneratorOption.toDict options
      finalOptions.Add("install", packages)

      return! reqHandler.Install(finalOptions, cancellationToken = token)
    }

  let update
    (dependencies: ImportMapServiceArgs)
    (options: GeneratorOption seq)
    (map: ImportMap)
    (packages: Set<string>)
    =
    cancellableTask {
      let! token = CancellableTask.getCancellationToken()
      let { reqHandler = reqHandler } = dependencies

      let finalOptions = GeneratorOption.toDict options
      finalOptions.Add("update", packages)
      finalOptions["inputMap"] <- map

      return! reqHandler.Update(finalOptions, cancellationToken = token)
    }

  let uninstall
    (dependencies: ImportMapServiceArgs)
    (options: GeneratorOption seq)
    (map: ImportMap)
    (packages: Set<string>)
    =
    cancellableTask {
      let! token = CancellableTask.getCancellationToken()
      let { reqHandler = reqHandler } = dependencies

      let finalOptions = GeneratorOption.toDict options
      finalOptions.Add("uninstall", packages)
      finalOptions["inputMap"] <- map

      return! reqHandler.Uninstall(finalOptions, cancellationToken = token)
    }

  /// Generates a map that can be persisted to disk for offline use
  let goOffline
    (dependencies: ImportMapServiceArgs)
    (options: DownloadOption seq)
    (map: ImportMap)
    =
    cancellableTask {
      let { logger = logger } = dependencies

      let allScopedImports =
        map.scopes |> Map.values |> Seq.collect Map.toSeq |> Map.ofSeq

      let combinedImports =
        map.imports
        |> Map.fold (fun state k v -> state |> Map.add k v) allScopedImports

      let! pkgs =
        download dependencies options { map with imports = combinedImports }

      // Cache the downloaded packages
      do! cacheResponse logger pkgs

      let localPrefix = LOCAL_CACHE_PREFIX

      // Helper function to extract the package name from a key
      let extractPackageName(key: string) =
        let parts = key.Split('@')
        if parts.Length > 2 then "@" + parts[1] else parts[0]

      // Helper function to find a matching package key
      let findMatchingKey (pkgName: string) (importUrl: string) =
        pkgs
        |> Map.keys
        |> Seq.tryFind(fun k ->
          let pkgNameFromKey = extractPackageName k
          pkgNameFromKey = pkgName || importUrl.Contains(k))

      // Helper function to convert URL to the local cache path
      let convertToLocalPath importUrl matchingKey isScoped =
        match matchingKey with
        | None -> importUrl
        | Some key ->
          let uri = Uri importUrl
          let filePath = Provider.extractFilePath logger uri
          // If extractFilePath returned the original URL (couldn't extract), keep it as is
          if filePath = importUrl then
            importUrl
          else
            let basePath =
              if isScoped then
                // Scoped packages point to .medusa/<package@version>
                Path.Combine(localPrefix, ".medusa", key)
              else
                // Non-scoped packages point to a flat structure
                let packageName =
                  if key.StartsWith("@") then
                    let parts = key.Split('@')

                    if parts.Length > 2 then
                      "@" + parts[1] + "@" + parts[2]
                    else
                      key
                  else
                    let parts = key.Split('@')
                    if parts.Length > 1 then parts[0] else key

                Path.Combine(localPrefix, packageName)

            Path.Combine(basePath, filePath).Replace('\\', '/')

      // Helper function to update a scope map
      let updateScopeMap(scopeMap: Map<string, string>) =
        scopeMap
        |> Map.map(fun pkgName importUrl ->
          let matchingKey = findMatchingKey pkgName importUrl

          logger.LogDebug(
            "Updating scope '{pkgName}' with import URL '{importUrl}'",
            pkgName,
            importUrl
          )

          let converted = convertToLocalPath importUrl matchingKey true

          logger.LogDebug(
            "Converted import URL to local path: '{converted}'",
            converted
          )

          converted)

      // Build a new imports map with local paths
      let updatedImports =
        map.imports
        |> Map.map(fun pkgName importUrl ->
          let matchingKey = findMatchingKey pkgName importUrl
          convertToLocalPath importUrl matchingKey false)

      let updatedScopes =
        map.scopes
        |> Seq.map(fun (KeyValue(_, scopeMap)) ->
          localPrefix + "/", updateScopeMap scopeMap)
        |> Map.ofSeq

      let offlineMap = {
        map with
            imports = updatedImports
            scopes = updatedScopes
      }

      logger.LogDebug("Generated offline map {map}", offlineMap)

      return offlineMap
    }

type ImportMapService =

  abstract member Install:
    packages: string seq *
    ?options: GeneratorOption seq *
    ?cancellationToken: CancellationToken ->
      Task<GeneratorResponse>

  abstract member Update:
    map: ImportMap *
    packages: string seq *
    ?options: GeneratorOption seq *
    ?cancellationToken: CancellationToken ->
      Task<GeneratorResponse>

  abstract member Uninstall:
    map: ImportMap *
    packages: string seq *
    ?options: GeneratorOption seq *
    ?cancellationToken: CancellationToken ->
      Task<GeneratorResponse>

  abstract member GoOffline:
    map: ImportMap *
    ?options: DownloadOption seq *
    ?cancellationToken: CancellationToken ->
      Task<ImportMap>


module ImportMapService =
  let create(dependencies: ImportMap.ImportMapServiceArgs) : ImportMapService =
    { new ImportMapService with
        member _.Install(packages, options, cancellationToken) =
          ImportMap.install
            dependencies
            (defaultArg options Seq.empty)
            (Set packages)
            (defaultArg cancellationToken CancellationToken.None)

        member _.Update(map, packages, options, cancellationToken) =
          ImportMap.update
            dependencies
            (defaultArg options Seq.empty)
            map
            (Set packages)
            (defaultArg cancellationToken CancellationToken.None)

        member _.Uninstall(map, packages, options, cancellationToken) =
          ImportMap.uninstall
            dependencies
            (defaultArg options Seq.empty)
            map
            (Set packages)
            (defaultArg cancellationToken CancellationToken.None)

        member _.GoOffline(map, options, cancellationToken) =
          ImportMap.goOffline
            dependencies
            (defaultArg options Seq.empty)
            map
            (defaultArg cancellationToken CancellationToken.None)
    }
