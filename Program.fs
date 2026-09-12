module Program

open System
open System.IO
open NAudio.Wave
open Sound

let helpText () =
    """SOUND — F# TUI CD プレイヤー

使い方:
  sound                 CD を検出。無ければデモディスク
  sound --demo          デモ信号でイコライザを試す
  sound --cd            光学ドライブを検出
  sound <フォルダ>      音声ファイルをディスクとして開く
  sound --rip           CD 全トラックを WAV 抽出
  sound --rip --track 3 指定トラックだけ抽出
  sound --rip --out DIR 出力先を指定
  sound --help          このヘルプ

主なキー:
  Space  再生/一時停止     Tab   トラック / EQ
  ↑↓    選択 / EQ ゲイン   +/-  EQ ゲイン
  X      CD を WAV 抽出    D     CD 検出
  O      フォルダ          Q     終了
"""

let selfCheck () =
    let cases =
        [
            (0, 2, 0), 0
            (0, 0, 0), -150
            (1, 0, 0), 4350
        ]

    let mutable ok = true

    for (m, s, f), expected in cases do
        let lba = CdMath.msfToLba m s f

        if lba <> expected then
            ok <- false
            printfn $"FAIL msfToLba {m}:{s}:{f} => {lba} (expected {expected})"
        else
            printfn $"OK   msfToLba {m}:{s}:{f} => {lba}"

        let m2, s2, f2 = CdMath.lbaToMsf lba

        if (m2, s2, f2) <> (m, s, f) then
            ok <- false
            printfn $"FAIL lbaToMsf {lba} => {m2}:{s2}:{f2}"

    let gain = EqPreset.clampGain 40.f

    if gain <> 12.f then
        ok <- false
        printfn $"FAIL clampGain {gain}"
    else
        printfn "OK   clampGain"

    try
        use stream = new DemoWaveStream(DemoKind.Chord, TimeSpan.FromSeconds 2.0)
        let bands = EqPreset.createBands ()
        bands[2].GainDb <- 6.f
        bands[7].GainDb <- -4.f
        let eq = EqualizerProvider(stream.ToSampleProvider(), bands)
        let analyzer = SpectrumAnalyzer(1024, 44100, 24)
        let tap = SpectrumTapProvider(eq, analyzer)
        let buf = Array.zeroCreate 4096
        let read = (tap :> NAudio.Wave.ISampleProvider).Read(buf, 0, buf.Length)

        if read <= 0 then
            ok <- false
            printfn "FAIL pipeline read"
        else
            printfn $"OK   pipeline read {read} samples"

        let bars, _ = analyzer.Snapshot()

        if bars |> Array.exists (fun x -> x > 0.f) then
            printfn "OK   spectrum energy"
        else
            printfn "WARN spectrum still quiet (window may need more samples)"
    with ex ->
        ok <- false
        printfn $"FAIL pipeline {ex.Message}"

    try
        let name = Ripper.sanitizeFileName @"track:1<>.wav"
        if name.Contains ':' || name.Contains '<' then
            ok <- false
            printfn $"FAIL sanitize {name}"
        else
            printfn $"OK   sanitize {name}"

        let dir = Path.Combine(Path.GetTempPath(), "sound-rip-check")
        Directory.CreateDirectory dir |> ignore
        let wav = Path.Combine(dir, "tone.wav")
        let pcm = Array.zeroCreate (4410 * 4)
        Ripper.writePcmWav wav pcm
        use reader = new AudioFileReader(wav)

        if reader.WaveFormat.SampleRate = 44100 && reader.WaveFormat.Channels = 2 then
            printfn "OK   wav write"
        else
            ok <- false
            printfn "FAIL wav format"

        reader.Dispose()
        File.Delete wav

        use player = new AudioPlayer()
        player.LoadDemo()
        player.StartRip(dir, true)

        match player.RipState with
        | RipFailed _ -> printfn "OK   demo rip rejected"
        | other ->
            ok <- false
            printfn $"FAIL demo rip state {other}"
    with ex ->
        ok <- false
        printfn $"FAIL wav {ex.Message}"

    if ok then 0 else 1

let ripCli (args: string array) =
    match CdDrive.tryLoadFirst () with
    | Error e ->
        eprintfn "%s" e
        1
    | Ok disc ->
        let outDir =
            match Array.tryFindIndex (fun a -> a = "--out" || a = "-o") args with
            | Some i when i + 1 < args.Length -> args[i + 1]
            | _ -> Ripper.defaultDirectory disc.Title

        let trackFilter =
            match Array.tryFindIndex (fun a -> a = "--track" || a = "-t") args with
            | Some i when i + 1 < args.Length ->
                let mutable n = 0
                if Int32.TryParse(args[i + 1], &n) then Some n else None
            | _ -> None

        let targets =
            disc.Tracks
            |> Array.choose (fun t ->
                match t.Rip with
                | Some rip when trackFilter.IsNone || trackFilter = Some t.Number -> Some(t, rip)
                | _ -> None)

        if targets.Length = 0 then
            eprintfn "抽出できるオーディオトラックがありません。デジタル読み取り可能な CD を入れてください。"
            1
        else
            printfn $"抽出先: {outDir}"

            match
                Ripper.extractTracks
                    0n
                    targets
                    outDir
                    (fun cur total label frac ->
                        let pct = int (frac * 100.0)
                        printf "%s" (sprintf "\r%d/%d %s %d%%   " cur total label pct))
                    (fun () -> false)
            with
            | Ok n ->
                printfn $"\n{n} 曲を書き出しました"
                0
            | Error(msg, n) ->
                eprintfn $"\n{msg}（{n} 曲まで完了）"
                1

let smoke () =
    use player = new AudioPlayer()
    player.LoadDemo()
    player.TogglePlay()
    Threading.Thread.Sleep 800
    player.AdjustEq 3.f
    player.ToggleEq()
    player.Next()
    Threading.Thread.Sleep 400
    player.Stop()
    printfn $"OK   smoke  state={player.State.Label}  disc={player.Disc.Title}"
    0

[<EntryPoint>]
let main args =
    if args |> Array.exists (fun a -> a = "--help" || a = "-h")
       && args |> Array.exists (fun a -> a = "--tui") |> not then
        printf $"{helpText ()}"
        0
    elif args |> Array.exists (fun a -> a = "--self-check") then
        selfCheck ()
    elif args |> Array.exists (fun a -> a = "--smoke") then
        smoke ()
    elif args |> Array.exists (fun a -> a = "--render-once") then
        App.renderSnapshot ()
        0
    elif args |> Array.exists (fun a -> a = "--rip") then
        ripCli args
    else
        try
            App.run args
            0
        with ex ->
            eprintfn $"致命的エラー: {ex.Message}"
            1
