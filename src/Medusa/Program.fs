namespace Medusa

open System
open System.Text.Json
open System.IO
open System.Threading
open System.Threading.Tasks

module Program =

    [<EntryPoint>]
    let main args =
        let cts = new CancellationTokenSource()

        let work =
            task {
                printfn "Installing packages..."

                let! genResponse =
                    PackageManager.install ([ "jquery"; "react"; "vue"; "lit" ], cancellationToken = cts.Token)

                File.WriteAllText("./dependencies.json", JsonSerializer.Serialize genResponse)

                printfn "Started downloading packages... %s" (DateTime.Now.ToString "o")

                let! response = PackageManager.download (genResponse.map, cancellationToken = cts.Token)

                printfn "Finished downloading packages... %s" (DateTime.Now.ToString "o")

                File.WriteAllText("./downloaded.json", JsonSerializer.Serialize response)

                File.WriteAllText("./dependencies.importmap", JsonSerializer.Serialize genResponse.map)

                let! result = PackageManager.toOffline (genResponse.map, cancellationToken = cts.Token)

                printfn "Offline import map created: %A" result
            }

        work.GetAwaiter().GetResult()
        0
