namespace Medusa

open System
open System.IO
open System.Text.Json
open System.Threading
open FsHttp
open IcedTasks
open JDeck

module ImportMap =
    open Types

    let private cachePath =
        lazy
            (Path.Combine(
                Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData,
                "perla",
                "v1",
                "store"
            ))

    let private localCachePath =
        lazy (Path.Combine(Directory.GetCurrentDirectory(), "web_dependencies"))

    let private localCachePrefix = lazy (Path.Combine("web_dependencies"))

    module private Required =
        let map<'T> : Decoder<Map<string, 'T>> = fun map -> Decode.auto<Map<string, 'T>> map

    let private importMapDecoder: Decoder<Types.ImportMap> =
        fun map ->
            decode {
                let! imports = map |> Optional.Property.get ("imports", Required.map<string>)
                let! scopes = map |> Optional.Property.get ("scopes", Required.map<Map<string, string>>)
                let! integrity = map |> Optional.Property.get ("integrity", Required.map<string>)

                return
                    { imports = defaultArg imports Map.empty
                      scopes = defaultArg scopes Map.empty
                      integrity = defaultArg integrity Map.empty }
            }

    let private downloadResponseDecoder: Decoder<Types.DownloadResponse> =
        fun res ->
            decode {
                let attempt = Decode.auto<Map<string, Types.DownloadPackage>> res

                match attempt with
                | Ok success -> return Types.DownloadSuccess success
                | Error _ ->
                    let! err = Decode.auto<Types.DownloadResponseError> res
                    return Types.DownloadError err
            }

    let private extractPackagesWithScopes (map: Types.ImportMap) =
        let imports = map.imports |> Map.values
        let scopeImports = map.scopes |> Map.values |> Seq.collect Map.values

        [ for value in [| yield! imports; yield! scopeImports |] do
              let uri = Uri(value)

              match Provider.extractFromUri uri with
              | Ok package -> package
              | Error _ -> () ]
        |> Set

    let private cacheResponse (response: Map<string, Types.DownloadPackage>) =
        cancellableTask {
            let cacheDir = Directory.CreateDirectory(cachePath.Value)
            let localCacheDir = Directory.CreateDirectory(localCachePath.Value)

            let tasks =
                response
                |> Map.toArray
                |> Array.map (fun (package, content) ->
                    asyncEx {
                        let! token = Async.CancellationToken
                        let localPkgPath = Path.Combine(localCacheDir.FullName, package)
                        let localPkgTarget = Path.Combine(cacheDir.FullName, package)

                        // Create Parent Directories
                        Path.GetDirectoryName(localPkgPath)
                        |> nonNull
                        |> Directory.CreateDirectory
                        |> ignore

                        // If the local store already has the package, skip creating the symbolic link
                        if Directory.Exists(localPkgPath) then
                            printfn "Package '%s' already exists, skipping symbolic link creation." package
                        else
                            Directory.CreateSymbolicLink(localPkgPath, localPkgTarget) |> ignore

                        if Directory.Exists(Path.Combine(cacheDir.FullName, package)) then
                            printfn "Package '%s' already exists, skipping download." package
                            return ()
                        else
                            printfn "Downloading package '%s'..." package

                            for file in content.files do
                                let filePath = Path.Combine(cacheDir.FullName, package, file)
                                let downloadUri = Uri(content.pkgUrl, file)

                                let! response =
                                    get (downloadUri.ToString())
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
                    })

            do! Async.Parallel tasks |> Async.Ignore
            return ()
        }

    let install (options: Types.GeneratorOption seq) (packages: Set<string>) =
        cancellableTask {
            let! token = CancellableTask.getCancellationToken ()
            let url = $"{Constants.JSPM_API_URL}generate"

            let finalOptions = GeneratorOption.toDict options
            finalOptions.Add("install", packages)

            let! req =
                http {
                    POST url
                    body
                    jsonSerialize finalOptions
                }
                |> Config.cancellationToken token
                |> Request.sendTAsync

            let! response = Response.deserializeJsonTAsync<Types.GeneratorResponse> token req
            return response
        }

    let update (options: Types.GeneratorOption seq) (map: Types.ImportMap) (packages: Set<string>) =
        cancellableTask {
            let! token = CancellableTask.getCancellationToken ()
            let url = $"{Constants.JSPM_API_URL}generate"

            let finalOptions = GeneratorOption.toDict options
            finalOptions.Add("update", packages)
            finalOptions["inputMap"] <- map

            let! req =
                http {
                    POST url
                    body
                    jsonSerialize finalOptions
                }
                |> Config.cancellationToken token
                |> Request.sendTAsync

            let! response = Response.deserializeJsonTAsync<Types.GeneratorResponse> token req
            return response
        }

    let uninstall (options: Types.GeneratorOption seq) (map: Types.ImportMap) (packages: Set<string>) =
        cancellableTask {
            let! token = CancellableTask.getCancellationToken ()
            let url = $"{Constants.JSPM_API_URL}generate"

            let finalOptions = GeneratorOption.toDict options
            finalOptions.Add("uninstall", packages)
            finalOptions["inputMap"] <- map

            let! req =
                http {
                    POST url
                    body
                    jsonSerialize finalOptions
                }
                |> Config.cancellationToken token
                |> Request.sendTAsync

            let! response = Response.deserializeJsonTAsync<Types.GeneratorResponse> token req
            return response
        }

    let download (options: Types.DownloadOption seq) (map: Types.ImportMap) =
        cancellableTask {
            let! token = CancellableTask.getCancellationToken ()
            let url = $"{Constants.JSPM_API_URL}download/"

            let! req =
                http {
                    GET url

                    query
                        [ "packages", extractPackagesWithScopes map |> String.concat ","
                          for option in options do
                              match option with
                              | Types.Provider provider ->
                                  "provider",
                                  match provider with
                                  | Types.DownloadProvider.JspmIo -> "jspm.io"
                                  | Types.DownloadProvider.JsDelivr -> "jsdelivr"
                                  | Types.DownloadProvider.Unpkg -> "unpkg"
                              | Types.Exclude excludes ->
                                  "exclude",
                                  [| for exclude in excludes ->
                                         match exclude with
                                         | Types.ExcludeOption.Unused -> "unused"
                                         | Types.ExcludeOption.Types -> "types"
                                         | Types.ExcludeOption.SourceMaps -> "sourcemaps"
                                         | Types.ExcludeOption.Readme -> "readme"
                                         | Types.ExcludeOption.License -> "license" |]
                                  |> String.concat "," ]

                    config_cancellationToken token
                }
                |> Request.sendTAsync

            use! response = Response.toStreamAsync req

            let jsonOptions =
                JsonSerializerOptions(WriteIndented = true)
                |> Codec.useDecoder downloadResponseDecoder

            let! response =
                JsonSerializer.DeserializeAsync<Types.DownloadResponse>(
                    response,
                    jsonOptions,
                    cancellationToken = token
                )

            match response with
            | Types.DownloadError err -> return raise (Exception $"Download failed: {err.error}")
            | Types.DownloadSuccess response ->
                printfn "Download Success: %d packages downloaded" response.Count
                // Cache the downloaded packages
                do! cacheResponse response
                return response
        }

    let toOffline (options: Types.DownloadOption seq) (map: Types.ImportMap) =
        cancellableTask {
            let allScopedImports =
                map.scopes |> Map.values |> Seq.collect Map.toSeq |> Map.ofSeq

            let combinedImports =
                map.imports |> Map.fold (fun state k v -> state |> Map.add k v) allScopedImports

            let! pkgs = download options { map with imports = combinedImports }
            let localPrefix = $"/{localCachePrefix.Value.Replace('\\', '/')}"

            // Helper function to extract package name from key
            let extractPackageName (key: string) =
                let parts = key.Split('@')
                if parts.Length > 2 then "@" + parts[1] else parts[0]

            // Helper function to find matching package key
            let findMatchingKey (pkgName: string) (importUrl: string) =
                pkgs
                |> Map.keys
                |> Seq.tryFind (fun k ->
                    let pkgNameFromKey = extractPackageName k
                    pkgNameFromKey = pkgName || importUrl.Contains(k))

            // Helper function to convert URL to local cache path
            let convertToLocalPath importUrl matchingKey =
                match matchingKey with
                | None -> importUrl
                | Some key ->
                    let uri = Uri importUrl
                    let filePath = Provider.extractFilePath uri
                    // If extractFilePath returned the original URL (couldn't extract), keep it as is
                    if filePath = importUrl then
                        importUrl
                    else
                        Path.Combine(localPrefix, key, filePath).Replace('\\', '/')

            // Helper function to update a scope map
            let updateScopeMap (scopeMap: Map<string, string>) =
                scopeMap
                |> Map.map (fun pkgName importUrl ->
                    let matchingKey = findMatchingKey pkgName importUrl
                    convertToLocalPath importUrl matchingKey)

            // Build a new imports map with local paths
            let updatedImports =
                map.imports
                |> Map.map (fun pkgName importUrl ->
                    let matchingKey = findMatchingKey pkgName importUrl
                    convertToLocalPath importUrl matchingKey)

            let updatedScopes =
                map.scopes
                |> Seq.map (fun (KeyValue(_, scopeMap)) -> localPrefix, updateScopeMap scopeMap)
                |> Map.ofSeq

            let offlineMap =
                { map with
                    imports = updatedImports
                    scopes = updatedScopes }

            // Write the offline import map to disk
            File.WriteAllText(
                "./offline.importmap",
                JsonSerializer.Serialize(offlineMap, JsonSerializerOptions(WriteIndented = true))
            )

            return offlineMap
        }
