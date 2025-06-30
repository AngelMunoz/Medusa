module Medusa.RequestHandler


open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

open FsHttp

open Medusa.Types

type JspmService =
  abstract member Install:
    options: IDictionary<string, obj> * ?cancellationToken: CancellationToken ->
      Task<GeneratorResponse>

  abstract member Update:
    options: IDictionary<string, obj> * ?cancellationToken: CancellationToken ->
      Task<GeneratorResponse>

  abstract member Uninstall:
    options: IDictionary<string, obj> * ?cancellationToken: CancellationToken ->
      Task<GeneratorResponse>

  abstract member Download:
    packages: string *
    options: (string * string) seq *
    ?cancellationToken: CancellationToken ->
      Task<DownloadResponse>


module JspmService =
  open System.Text.Json

  let create(serializerOptions: JsonSerializerOptions option) =
    { new JspmService with
        member _.Download(packages, options, ?cancellationToken) = task {
          let token = defaultArg cancellationToken CancellationToken.None
          let url = $"{Constants.JSPM_API_URL}download/"

          let! req =
            http {
              GET url

              query [ "packages", packages; yield! options ]

              config_cancellationToken token
            }
            |> Request.sendTAsync

          use! response = Response.toStreamAsync req

          let! response =
            JsonSerializer.DeserializeAsync<DownloadResponse>(
              response,
              ?options = serializerOptions,
              cancellationToken = token
            )

          match response with
          | null -> return failwith "Failed to deserialize DownloadResponse"
          | response -> return response
        }

        member _.Install(options, ?cancellationToken) = task {
          let token = defaultArg cancellationToken CancellationToken.None
          let url = $"{Constants.JSPM_API_URL}generate"

          let! req =
            http {
              POST url
              body
              jsonSerialize options
            }
            |> Config.cancellationToken token
            |> Request.sendTAsync

          use! responseStream = Response.toStreamAsync req

          let! response =
            JsonSerializer.DeserializeAsync<GeneratorResponse>(
              responseStream,
              ?options = serializerOptions,
              cancellationToken = token
            )

          match response with
          | null -> return failwith "Failed to deserialize GeneratorResponse"
          | response -> return response
        }

        member _.Uninstall(options, ?cancellationToken) = task {
          let token = defaultArg cancellationToken CancellationToken.None
          let url = $"{Constants.JSPM_API_URL}generate"

          let! req =
            http {
              POST url
              body
              jsonSerialize options
            }
            |> Config.cancellationToken token
            |> Request.sendTAsync

          use! responseStream = Response.toStreamAsync req

          let! response =
            JsonSerializer.DeserializeAsync<GeneratorResponse>(
              responseStream,
              ?options = serializerOptions,
              cancellationToken = token
            )

          match response with
          | null -> return failwith "Failed to deserialize GeneratorResponse"
          | response -> return response
        }

        member _.Update(options, ?cancellationToken) = task {
          let token = defaultArg cancellationToken CancellationToken.None
          let url = $"{Constants.JSPM_API_URL}generate"

          let! req =
            http {
              POST url
              body
              jsonSerialize options
            }
            |> Config.cancellationToken token
            |> Request.sendTAsync

          use! responseStream = Response.toStreamAsync req

          let! response =
            JsonSerializer.DeserializeAsync<GeneratorResponse>(
              responseStream,
              ?options = serializerOptions,
              cancellationToken = token
            )

          match response with
          | null -> return failwith "Failed to deserialize GeneratorResponse"
          | response -> return response
        }
    }
