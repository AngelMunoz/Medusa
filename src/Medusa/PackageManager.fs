namespace Medusa

open System.Threading
open System.Threading.Tasks

/// Public API for the package manager functionality
type PackageManager =

    static member install
        (packages: string seq, ?options: Types.GeneratorOption seq, ?cancellationToken: CancellationToken)
        =
        let token = defaultArg cancellationToken CancellationToken.None
        let opts = defaultArg options Seq.empty
        ImportMap.install opts (packages |> Set.ofSeq) token

    static member update
        (
            packages: string seq,
            map: Types.ImportMap,
            ?options: Types.GeneratorOption seq,
            ?cancellationToken: CancellationToken
        ) =
        let token = defaultArg cancellationToken CancellationToken.None
        let opts = defaultArg options Seq.empty
        ImportMap.update opts map (packages |> Set.ofSeq) token

    static member uninstall
        (
            packages: string seq,
            map: Types.ImportMap,
            ?options: Types.GeneratorOption seq,
            ?cancellationToken: CancellationToken
        ) =
        let token = defaultArg cancellationToken CancellationToken.None
        let opts = defaultArg options Seq.empty
        ImportMap.uninstall opts map (packages |> Set.ofSeq) token

    static member download
        (map: Types.ImportMap, ?options: Types.DownloadOption seq, ?cancellationToken: CancellationToken)
        =
        let token = defaultArg cancellationToken CancellationToken.None
        let opts = defaultArg options Seq.empty
        ImportMap.download opts map token

    static member toOffline
        (map: Types.ImportMap, ?options: Types.DownloadOption seq, ?cancellationToken: CancellationToken)
        =
        let token = defaultArg cancellationToken CancellationToken.None
        let opts = defaultArg options Seq.empty
        ImportMap.toOffline opts map token
