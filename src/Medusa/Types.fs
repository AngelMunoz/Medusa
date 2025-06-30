namespace Medusa

open System
open System.Collections.Generic

module Constants =
  [<Literal>]
  let JSPM_API_URL = "https://api.jspm.io/"

module Types =

  type ImportMap = {
    imports: Map<string, string>
    scopes: Map<string, Map<string, string>>
    integrity: Map<string, string>
  }

  type PartialImportMap = {
    imports: Map<string, string> option
    scopes: Map<string, Map<string, string>> option
    integrity: Map<string, string> option
  }

  /// Represents the options for caching, corresponding to 'boolean | "offline"'.
  type CacheOption =
    | Enabled of bool
    | Offline

  type ExportCondition =
    | Development
    | Browser
    | Module
    | Custom of string

  type Provider =
    | JspmIo
    | JspmIoSystem
    | NodeModules
    | Skypack
    | JsDelivr
    | Unpkg
    | EsmSh
    | Custom of string

  /// These options are gathered from the serializable properties in this interface
  /// https://jspm.org/docs/generator/interfaces/GeneratorOptions.html
  type GeneratorOption =
    | BaseUrl of Uri
    | MapUrl of Uri
    | RootUrl of Uri
    | InputMap of ImportMap
    | DefaultProvider of Provider
    | Providers of Map<string, string>
    | ProviderConfig of Map<string, Map<string, string>>
    | Resolutions of Map<string, string>
    | Env of Set<ExportCondition>
    | Cache of CacheOption
    | Ignore of Set<string>
    | FlattenScopes of bool
    | CombineSubPaths of bool

  type DownloadProvider =
    | JspmIo
    | JsDelivr
    | Unpkg

  type ExcludeOption =
    | Unused
    | Types
    | SourceMaps
    | Readme
    | License

  type DownloadOption =
    | Provider of DownloadProvider
    | Exclude of Set<ExcludeOption>

  type DownloadPackage = { pkgUrl: Uri; files: string array }

  type DownloadResponseError = { error: string }

  type DownloadResponse =
    | DownloadError of DownloadResponseError
    | DownloadSuccess of Map<string, DownloadPackage>

  type GeneratorResponse = {
    staticDeps: string array
    dynamicDeps: string array
    map: PartialImportMap
  }

module GeneratorOption =
  open Types

  let toDict(options: GeneratorOption seq) =
    let finalOptions = Dictionary<string, obj>()

    for option in options do
      match option with
      | BaseUrl uri -> finalOptions.TryAdd("baseUrl", uri.ToString()) |> ignore
      | MapUrl uri -> finalOptions.TryAdd("mapUrl", uri.ToString()) |> ignore
      | RootUrl value ->
        finalOptions.TryAdd("rootUrl", value.ToString()) |> ignore
      | InputMap value -> finalOptions.TryAdd("inputMap", value) |> ignore
      | DefaultProvider value ->
        let providerString =
          match value with
          | Provider.JspmIo -> "jspm.io"
          | Provider.JspmIoSystem -> "jspm.io#system"
          | Provider.NodeModules -> "nodemodles"
          | Provider.Skypack -> "skypack"
          | Provider.JsDelivr -> "jsdelivr"
          | Provider.Unpkg -> "unpkg"
          | Provider.EsmSh -> "esm.sh"
          | Provider.Custom customProvider -> customProvider

        finalOptions.TryAdd("defaultProvider", providerString) |> ignore
      | Providers value -> finalOptions.TryAdd("providers", value) |> ignore
      | ProviderConfig value ->
        finalOptions.TryAdd("providerConfig", value) |> ignore
      | Resolutions value -> finalOptions.TryAdd("resolutions", value) |> ignore
      | Env value ->
        let envStrings =
          value
          |> Set.map (function
            | Development -> "development"
            | Browser -> "browser"
            | Module -> "module"
            | ExportCondition.Custom customEnv -> customEnv)

        finalOptions.TryAdd("env", envStrings) |> ignore
      | Cache value ->
        match value with
        | Enabled enabled -> finalOptions.TryAdd("cache", enabled) |> ignore
        | Offline -> finalOptions.TryAdd("cache", "offline") |> ignore
      | Ignore value -> finalOptions.TryAdd("ignore", value) |> ignore
      | FlattenScopes value ->
        finalOptions.TryAdd("flattenScopes", value) |> ignore
      | CombineSubPaths value ->
        finalOptions.TryAdd("combineSubPaths", value) |> ignore

    finalOptions

module PartialImportMap =
  open Types

  let toImportMap(partial: PartialImportMap) : ImportMap = {
    imports = defaultArg partial.imports Map.empty
    scopes = defaultArg partial.scopes Map.empty
    integrity = defaultArg partial.integrity Map.empty
  }

module JsonOptions =
  open System.Text.Json
  open JDeck
  open Types

  let private downloadResponseDecoder: Decoder<DownloadResponse> =
    fun res -> decode {
      let attempt = Decode.auto<Map<string, DownloadPackage>> res

      match attempt with
      | Ok success -> return DownloadSuccess success
      | Error _ ->
        let! err = Decode.auto<DownloadResponseError> res
        return DownloadError err
    }

  /// Shared JSON options for consistent serialization/deserialization throughout the application
  let shared =
    lazy
      (JsonSerializerOptions(WriteIndented = true)
       |> Codec.useDecoder downloadResponseDecoder)
