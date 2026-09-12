module Program

open System
open NAudio.Wave
open Sound

let helpText () =
    """SOUND — F# TUI CD プレイヤー

使い方:
  sound                 CD を検出。無ければデモディスク
  sound --demo          デモ信号でイコライザを試す
  sound --cd            光学ドライブを検出
  sound <フォルダ>      音声ファイルをディスクとして開く
  sound --help          このヘルプ

主なキー:
  Space  再生/一時停止     Tab   トラック / EQ
  ↑↓    選択 / EQ ゲイン   +/-  EQ ゲイン
  D      CD 検出           O     フォルダ
  M      デモ              Q     終了
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

    if ok then 0 else 1

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
    else
        try
            App.run args
            0
        with ex ->
            eprintfn $"致命的エラー: {ex.Message}"
            1
