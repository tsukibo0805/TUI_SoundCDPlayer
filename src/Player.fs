namespace Sound

open System
open System.IO
open System.Threading
open NAudio.Wave
open NAudio.Wave.SampleProviders

type AudioPlayer() =
    let gate = obj ()
    let bands = EqPreset.createBands ()
    let analyzer = SpectrumAnalyzer(2048, 44100, 48)
    let mutable disc = DemoDisc.create ()
    let mutable state = Stopped
    let mutable currentIndex = 0
    let mutable selectedIndex = 0
    let mutable eqIndex = 0
    let mutable focus = TrackList
    let mutable volume = 0.85f
    let mutable repeat = RepeatAll
    let mutable shuffle = false
    let mutable eqEnabled = true
    let mutable showHelp = false
    let mutable running = true
    let mutable pending = Idle
    let mutable ripState = RipIdle
    let mutable ripCancel = false
    let mutable status = "デモディスクをロードしました。D で CD を検出、O でフォルダを開きます。"
    let mutable output: IWavePlayer option = None
    let mutable stream: WaveStream option = None
    let mutable equalizer: EqualizerProvider option = None
    let mutable volumeProvider: VolumeSampleProvider option = None
    let mutable ignoreStop = false
    let mutable digitalHandle = 0n
    let mutable usingMci = false
    let rng = Random()

    let audioExtensions =
        set [ ".wav"; ".mp3"; ".aiff"; ".aif"; ".wma"; ".m4a"; ".aac" ]

    let closeDigitalHandle () =
        if digitalHandle <> 0n then
            CdDrive.closeHandle digitalHandle
            digitalHandle <- 0n

    let releaseWave () =
        ignoreStop <- true

        match output with
        | Some player ->
            try
                player.Stop()
            with _ ->
                ()

            try
                player.Dispose()
            with _ ->
                ()
        | None -> ()

        match stream with
        | Some s ->
            try
                s.Dispose()
            with _ ->
                ()
        | None -> ()

        output <- None
        stream <- None
        equalizer <- None
        volumeProvider <- None
        ignoreStop <- false

    let rec playAt (index: int) =
        if index < 0 || index >= disc.Tracks.Length then
            ()
        else
            currentIndex <- index
            selectedIndex <- index
            analyzer.Reset()
            releaseWave ()
            Mci.stop ()
            usingMci <- false

            let track = disc.Tracks[index]

            match track.Source with
            | MciCd(drive, number) ->
                match Mci.openDrive $"{drive}:" with
                | Error e ->
                    state <- Stopped
                    status <- e
                | Ok _ ->
                    let next =
                        if index + 1 < disc.Tracks.Length then
                            Some disc.Tracks[index + 1].Number
                        else
                            None

                    match Mci.play number next with
                    | Error e ->
                        state <- Stopped
                        status <- e
                    | Ok _ ->
                        usingMci <- true
                        state <- Playing
                        status <- $"MCI 再生中（イコライザはデジタル経路のみ）: {track.Title}"
            | source ->
                try
                    let wave =
                        match source with
                        | DigitalCd(handle, lba, sectors) -> new CdWaveStream(handle, lba, sectors) :> WaveStream
                        | AudioFile path -> new AudioFileReader(path) :> WaveStream
                        | DemoTone kind -> new DemoWaveStream(kind, track.Duration) :> WaveStream
                        | MciCd _ -> failwith "unreachable"

                    stream <- Some wave
                    let samples = wave.ToSampleProvider()
                    let eq = EqualizerProvider(samples, bands)
                    eq.Bypass <- not eqEnabled
                    equalizer <- Some eq
                    let vol = VolumeSampleProvider(eq)
                    vol.Volume <- volume
                    volumeProvider <- Some vol
                    let tap = SpectrumTapProvider(vol, analyzer)
                    let player = new WaveOutEvent()
                    player.DesiredLatency <- 200

                    player.PlaybackStopped.Add(fun _ ->
                        if not ignoreStop && state = Playing then
                            lock gate (fun () -> onEnded ()))

                    player.Init(tap)
                    player.Play()
                    output <- Some player
                    state <- Playing

                    let eqNote = if eqEnabled then "EQ ON" else "EQ OFF"
                    status <- $"{track.Title}  /  {eqNote}"
                with ex ->
                    state <- Stopped
                    status <- $"再生エラー: {ex.Message}"

    and onEnded () =
        match repeat with
        | RepeatOne -> playAt currentIndex
        | RepeatAll -> playAt (nextIndex true)
        | RepeatOff ->
            let n = currentIndex + 1

            if n < disc.Tracks.Length then
                playAt n
            else
                state <- Stopped
                status <- "再生終了"

    and nextIndex (wrap: bool) =
        if disc.Tracks.Length = 0 then
            0
        elif shuffle && disc.Tracks.Length > 1 then
            let mutable n = rng.Next(disc.Tracks.Length)

            while n = currentIndex do
                n <- rng.Next(disc.Tracks.Length)

            n
        else
            let n = currentIndex + 1

            if n >= disc.Tracks.Length then
                if wrap then 0 else currentIndex
            else
                n

    let adoptDisc (next: Disc) (message: string) =
        releaseWave ()
        Mci.stop ()
        Mci.close ()
        closeDigitalHandle ()
        usingMci <- false

        digitalHandle <-
            next.Tracks
            |> Array.tryPick (fun t ->
                match t.Source with
                | DigitalCd(h, _, _) when h <> 0n -> Some h
                | _ -> None)
            |> Option.defaultValue 0n

        disc <- next
        currentIndex <- 0
        selectedIndex <- 0
        state <- Stopped
        status <- message

    member _.Bands = bands
    member _.Analyzer = analyzer
    member _.Disc = disc
    member _.State = state
    member _.CurrentIndex = currentIndex
    member _.SelectedIndex = selectedIndex
    member _.EqIndex = eqIndex
    member _.Focus = focus
    member _.Volume = volume
    member _.Repeat = repeat
    member _.Shuffle = shuffle
    member _.EqEnabled = eqEnabled
    member _.ShowHelp = showHelp
    member _.Running = running
    member _.Status = status
    member _.Pending = pending
    member _.RipState = ripState

    member _.IsRipping =
        match ripState with
        | RipRunning _ -> true
        | _ -> false

    member _.CurrentTrack =
        if disc.Tracks.Length = 0 then
            None
        else
            Some disc.Tracks[currentIndex]

    member _.Position =
        if usingMci then
            match Mci.position () with
            | Some(_, pos) -> pos
            | None -> TimeSpan.Zero
        else
            match stream with
            | Some s ->
                try
                    s.CurrentTime
                with _ ->
                    TimeSpan.Zero
            | None -> TimeSpan.Zero

    member this.Duration =
        match this.CurrentTrack with
        | Some t -> t.Duration
        | None -> TimeSpan.Zero

    member _.LoadDisc(next: Disc, message: string) =
        lock gate (fun () -> adoptDisc next message)

    member this.LoadDemo() =
        this.LoadDisc(DemoDisc.create (), "デモディスクをロードしました")

    member this.LoadFolder(path: string) =
        if String.IsNullOrWhiteSpace path then
            status <- "パスが空です"
        elif not (Directory.Exists path) then
            status <- $"フォルダがありません: {path}"
        else
            let files =
                Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                |> Seq.filter (fun f -> audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                |> Seq.sort
                |> Seq.toArray

            if files.Length = 0 then
                status <- "対応する音声ファイルがありません"
            else
                let tracks =
                    files
                    |> Array.mapi (fun i file ->
                        let dur =
                            try
                                use reader = new AudioFileReader(file)
                                reader.TotalTime
                            with _ ->
                                TimeSpan.Zero

                        {
                            Index = i
                            Number = i + 1
                            Title = Path.GetFileNameWithoutExtension(file)
                            Duration = dur
                            Source = AudioFile file
                            Rip = None
                        })

                let folder = DirectoryInfo(path).Name
                this.LoadDisc(
                    {
                        Title = folder
                        Artist = path
                        Kind = FolderDisc
                        Drive = None
                        Tracks = tracks
                    },
                    $"{tracks.Length} 曲をロードしました")

    member this.DetectCd() =
        match CdDrive.tryLoadFirst () with
        | Ok next ->
            this.LoadDisc(next, $"{next.Kind.Label}: {next.Tracks.Length} トラック")
        | Error e -> status <- e

    member this.Eject() =
        match disc.Drive with
        | None -> status <- "取り出せる CD がありません"
        | Some letter ->
            releaseWave ()
            Mci.close ()

            match CdDrive.eject letter with
            | Ok() ->
                closeDigitalHandle ()
                this.LoadDemo()
                status <- $"{letter}: を取り出しました。デモディスクに戻ります。"
            | Error e -> status <- e

    member _.PlaySelected() =
        lock gate (fun () -> playAt selectedIndex)

    member this.TogglePlay() =
        lock gate (fun () ->
            match state with
            | Stopped -> playAt currentIndex
            | Playing when usingMci ->
                Mci.pause () |> ignore
                state <- Paused
                status <- "一時停止"
            | Playing ->
                match output with
                | Some p ->
                    p.Pause()
                    state <- Paused
                    status <- "一時停止"
                | None -> ()
            | Paused when usingMci ->
                Mci.resume () |> ignore
                state <- Playing
                status <- "再生"
            | Paused ->
                match output with
                | Some p ->
                    p.Play()
                    state <- Playing
                    status <- "再生"
                | None -> playAt currentIndex)

    member _.Stop() =
        lock gate (fun () ->
            releaseWave ()
            Mci.stop ()
            usingMci <- false
            state <- Stopped
            analyzer.Reset()
            status <- "停止")

    member this.Next() =
        lock gate (fun () -> playAt (nextIndex true))

    member this.Previous() =
        lock gate (fun () ->
            if this.Position.TotalSeconds > 3.0 then
                playAt currentIndex
            else
                let n = if currentIndex <= 0 then disc.Tracks.Length - 1 else currentIndex - 1
                playAt n)

    member _.Seek(delta: TimeSpan) =
        lock gate (fun () ->
            if usingMci then
                status <- "MCI 再生ではシークできません"
            else
                match stream with
                | None -> ()
                | Some s ->
                    let next = s.CurrentTime + delta
                    let maxT = s.TotalTime
                    let clamped =
                        if next < TimeSpan.Zero then TimeSpan.Zero
                        elif next > maxT then maxT
                        else next

                    try
                        s.CurrentTime <- clamped
                    with _ ->
                        ())

    member _.MoveSelection(delta: int) =
        if disc.Tracks.Length > 0 then
            selectedIndex <-
                (selectedIndex + delta + disc.Tracks.Length * 4) % disc.Tracks.Length

    member _.MoveEq(delta: int) =
        eqIndex <- (eqIndex + delta + bands.Length * 4) % bands.Length

    member _.AdjustEq(delta: float32) =
        let band = bands[eqIndex]
        band.GainDb <- EqPreset.clampGain (band.GainDb + delta)

        match equalizer with
        | Some eq -> eq.Update()
        | None -> ()

    member _.ResetEq() =
        for band in bands do
            band.GainDb <- 0.f

        match equalizer with
        | Some eq -> eq.Update()
        | None -> ()

        status <- "イコライザをフラットにしました"

    member _.ToggleEq() =
        eqEnabled <- not eqEnabled

        match equalizer with
        | Some eq -> eq.Bypass <- not eqEnabled
        | None -> ()

        status <- if eqEnabled then "イコライザ ON" else "イコライザ OFF"

    member _.AdjustVolume(delta: float32) =
        volume <- Math.Clamp(volume + delta, 0.f, 1.f)

        match volumeProvider with
        | Some v -> v.Volume <- volume
        | None -> ()

    member _.ToggleFocus() = focus <- focus.Next()
    member _.ToggleHelp() = showHelp <- not showHelp
    member _.CycleRepeat() = repeat <- repeat.Next()
    member _.ToggleShuffle() = shuffle <- not shuffle
    member _.RequestOpenFolder() = pending <- OpenFolder
    member _.RequestRip() = pending <- RipPrompt
    member _.RequestQuit() = pending <- Quit

    member _.CancelRip() =
        match ripState with
        | RipRunning _ ->
            ripCancel <- true
            status <- "抽出をキャンセルしています…"
        | _ -> ()

    member this.StartRip(outputDir: string, allTracks: bool) =
        if this.IsRipping then
            status <- "すでに抽出中です"
        elif String.IsNullOrWhiteSpace outputDir then
            status <- "出力先が空です"
        else
            let attachRip () =
                match disc.Drive with
                | None -> ()
                | Some letter ->
                    match CdDrive.readTocTracks letter with
                    | Error _ -> ()
                    | Ok toc ->
                        let byNumber =
                            toc
                            |> Array.choose (fun t -> t.Rip |> Option.map (fun r -> t.Number, r))
                            |> Map.ofArray

                        disc <-
                            { disc with
                                Tracks =
                                    disc.Tracks
                                    |> Array.map (fun t ->
                                        match t.Rip, Map.tryFind t.Number byNumber with
                                        | Some _, _ -> t
                                        | None, Some rip -> { t with Rip = Some rip }
                                        | None, None -> t)
                            }

            if disc.Tracks |> Array.forall (fun t -> t.Rip.IsNone) then
                attachRip ()

            let pool =
                if allTracks then
                    disc.Tracks
                elif disc.Tracks.Length = 0 then
                    [||]
                else
                    [| disc.Tracks[selectedIndex] |]

            let targets =
                pool
                |> Array.choose (fun t -> t.Rip |> Option.map (fun r -> t, r))

            if targets.Length = 0 then
                status <- "このディスクは抽出できません。CD のデジタル読み取りが必要です。"
                ripState <- RipFailed status
            else
                this.Stop()
                ripCancel <- false

                ripState <-
                    RipRunning {
                        Current = 1
                        Total = targets.Length
                        Label = (fst targets[0]).Title
                        Fraction = 0.0
                        Destination = outputDir
                    }

                status <- $"WAV 抽出を開始: {outputDir}"
                let handle = digitalHandle

                Tasks.Task.Run(fun () ->
                    match
                        Ripper.extractTracks
                            handle
                            targets
                            outputDir
                            (fun cur total label frac ->
                                ripState <-
                                    RipRunning {
                                        Current = cur
                                        Total = total
                                        Label = label
                                        Fraction = frac
                                        Destination = outputDir
                                    }

                                status <- sprintf "抽出中 %d/%d  %s  %.0f%%" cur total label (frac * 100.0))
                            (fun () -> ripCancel)
                    with
                    | Ok n ->
                        ripState <- RipDone(n, outputDir)
                        status <- sprintf "%d 曲を書き出しました: %s" n outputDir
                    | Error(msg, n) when ripCancel ->
                        ripState <- RipCanceled outputDir
                        status <-
                            if n > 0 then
                                sprintf "キャンセル（%d 曲まで完了）: %s" n outputDir
                            else
                                "抽出をキャンセルしました"
                    | Error(msg, n) ->
                        ripState <- RipFailed msg
                        status <-
                            if n > 0 then
                                sprintf "%s（%d 曲まで完了）" msg n
                            else
                                msg)
                |> ignore

    member _.ClearPending() =
        let p = pending
        pending <- Idle
        p

    member _.Tick() =
        if usingMci && state = Playing then
            let mode = Mci.mode ()

            if mode = "stopped" then
                lock gate (fun () -> onEnded ())
        elif state <> Playing then
            analyzer.Decay()

    member _.Finish() =
        ripCancel <- true
        running <- false

    interface IDisposable with
        member _.Dispose() =
            ripCancel <- true
            running <- false
            releaseWave ()
            Mci.close ()
            closeDigitalHandle ()
