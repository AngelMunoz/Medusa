open System
open System.Text.Json
open System.IO
open System.Threading

open Microsoft.Extensions.Logging


open Medusa
open Medusa.Types
open Medusa.RequestHandler

let orchestrate() =
  let loggerFactory =
    LoggerFactory.Create(fun builder ->
      builder
        .AddConsole()
#if DEBUG
        .SetMinimumLevel(LogLevel.Debug)
#else
        .SetMinimumLevel(LogLevel.Information)
#endif
      |> ignore)

  let logger = loggerFactory.CreateLogger(nameof Medusa)

  let jsOptions = JsonOptions.shared.Value

  let jspmApi = JspmService.create(Some jsOptions)

  let importMapService =
    ImportMapService.create {
      reqHandler = jspmApi
      logger = logger
    }

  importMapService, logger


[<EntryPoint>]
let main args =
  use cts = new CancellationTokenSource()
  Console.CancelKeyPress.Add(fun args -> cts.Cancel())

  task {
    let imService, logger = orchestrate()
    logger.LogInformation("Starting installation and offline caching...")

    logger.LogInformation("Installing packages: jquery, xstate, vue")

    let! result =
      imService.Install(
        [ "jquery"; "xstate"; "vue" ],
        cancellationToken = cts.Token
      )

    let onlineMap = result.map |> PartialImportMap.toImportMap

    do!
      File.WriteAllTextAsync(
        "online-map.importmap",
        JsonSerializer.Serialize(onlineMap, JsonOptions.shared.Value),
        cts.Token
      )

    logger.LogInformation("Generated Online Map: {ImportMap}", onlineMap)

    let! result = imService.GoOffline(onlineMap, cancellationToken = cts.Token)

    logger.LogInformation(
      "Installation and offline caching completed successfully."
    )

    logger.LogInformation("Generated Offline Map: {ImportMap}", result)

    do!
      File.WriteAllTextAsync(
        "offline-map.importmap",
        JsonSerializer.Serialize(result, JsonOptions.shared.Value),
        cts.Token
      )

    return 0
  }
  |> _.GetAwaiter().GetResult()
