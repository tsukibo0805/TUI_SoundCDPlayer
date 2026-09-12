namespace Sound

open System
open System.IO
open NAudio.Wave

module Ripper =
    let sanitizeFileName (name: string) =
        let invalid = Set.ofArray (Path.GetInvalidFileNameChars())

        let cleaned =
            name
            |> Seq.map (fun c -> if invalid.Contains c then '_' else c)
            |> Seq.toArray
            |> String
            |> fun s -> s.Trim()

        if String.IsNullOrWhiteSpace cleaned then "track" else cleaned

    let defaultDirectory (discTitle: string) =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            "SOUND",
            sanitizeFileName discTitle)

    let wavName (track: Track) =
        sprintf "%02d %s.wav" track.Number (sanitizeFileName track.Title)

    let writePcmWav (path: string) (pcm: byte array) =
        match Path.GetDirectoryName path with
        | null | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

        use writer = new WaveFileWriter(path, WaveFormat(44100, 16, 2))
        writer.Write(pcm, 0, pcm.Length)

    let private readChunk handle lba sectors dest =
        let rec attempt n last =
            match CdDrive.readRaw handle lba sectors dest 0 with
            | Ok() -> Ok(sectors * Win32.bytesPerRawSector)
            | Error code when n > 1 ->
                System.Threading.Thread.Sleep 40
                attempt (n - 1) (Some code)
            | Error code ->
                if sectors > 1 then
                    let rec oneByOne i written =
                        if i >= sectors then
                            Ok written
                        else
                            match CdDrive.readRaw handle (lba + i) 1 dest (i * Win32.bytesPerRawSector) with
                            | Ok() -> oneByOne (i + 1) (written + Win32.bytesPerRawSector)
                            | Error inner -> Error inner

                    oneByOne 0 0
                else
                    Error(last |> Option.defaultValue code)

        attempt 3 None

    let extractTrack
        (handle: nativeint)
        (info: RipInfo)
        (destPath: string)
        (progress: int -> int -> unit)
        (isCanceled: unit -> bool)
        =
        let dir = Path.GetDirectoryName destPath

        if not (String.IsNullOrWhiteSpace dir) then
            Directory.CreateDirectory(dir) |> ignore

        let format = WaveFormat(44100, 16, 2)
        let totalBytes = info.Sectors * Win32.bytesPerRawSector
        let chunkSectors = 26
        let buf = Array.zeroCreate (chunkSectors * Win32.bytesPerRawSector)

        try
            let result =
                use writer = new WaveFileWriter(destPath, format)
                let mutable sector = 0
                let mutable written = 0
                let mutable error = None

                while sector < info.Sectors && error.IsNone do
                    if isCanceled () then
                        error <- Some "キャンセルしました"
                    else
                        let n = min chunkSectors (info.Sectors - sector)

                        match readChunk handle (info.StartLba + sector) n buf with
                        | Error code ->
                            error <- Some $"セクタ {info.StartLba + sector} の読み取りに失敗しました (Win32 {code})"
                        | Ok bytes ->
                            writer.Write(buf, 0, bytes)
                            written <- written + bytes
                            progress written totalBytes
                            sector <- sector + n

                match error with
                | Some msg -> Error msg
                | None -> Ok destPath

            match result with
            | Error msg ->
                try
                    if File.Exists destPath then
                        File.Delete destPath
                with _ ->
                    ()

                Error msg
            | Ok path -> Ok path
        with ex ->
            Error $"書き出しエラー: {ex.Message}"

    let extractTracks
        (existingHandle: nativeint)
        (tracks: (Track * RipInfo) array)
        (outputDir: string)
        (progress: int -> int -> string -> float -> unit)
        (isCanceled: unit -> bool)
        =
        Directory.CreateDirectory(outputDir) |> ignore
        let mutable opened = 0n
        let mutable files = 0
        let mutable firstError = None

        let handleFor (info: RipInfo) =
            if existingHandle <> 0n then
                Ok existingHandle
            elif opened <> 0n then
                Ok opened
            else
                match CdDrive.openHandle info.Drive with
                | Ok h ->
                    opened <- h
                    Ok h
                | Error e -> Error e

        try
            let mutable i = 0

            while i < tracks.Length && firstError.IsNone && not (isCanceled ()) do
                let track, info = tracks[i]
                let dest = Path.Combine(outputDir, wavName track)
                progress (i + 1) tracks.Length track.Title 0.0

                match handleFor info with
                | Error e -> firstError <- Some e
                | Ok handle ->
                    match extractTrack handle info dest (fun doneBytes total ->
                        let frac =
                            if total <= 0 then
                                0.0
                            else
                                float doneBytes / float total

                        progress (i + 1) tracks.Length track.Title frac) isCanceled
                    with
                    | Error e -> firstError <- Some e
                    | Ok _ -> files <- files + 1

                i <- i + 1

            if opened <> 0n && opened <> existingHandle then
                CdDrive.closeHandle opened

            if isCanceled () && firstError.IsNone then
                Error("キャンセルしました", files)
            else
                match firstError with
                | Some e -> Error(e, files)
                | None -> Ok files
        with ex ->
            if opened <> 0n && opened <> existingHandle then
                CdDrive.closeHandle opened

            Error(ex.Message, files)
