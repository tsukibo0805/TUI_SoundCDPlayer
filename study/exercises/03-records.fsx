// 演習 03: レコードとモジュール
// 実行: dotnet fsi study/exercises/03-records.fsx
// 対応レッスン: 04-レコードとモジュール.md

open System

let mutable passed = 0
let mutable failed = 0

let check name expected actual =
    if expected = actual then
        passed <- passed + 1
        printfn "OK   %s" name
    else
        failed <- failed + 1
        printfn "FAIL %s  expected=%A  actual=%A" name expected actual

type TrackSource =
    | AudioFile of path: string
    | DemoTone of name: string

type Track = {
    Index: int
    Number: int
    Title: string
    Duration: TimeSpan
    Source: TrackSource
}

type Disc = {
    Title: string
    Artist: string
    Tracks: Track array
}

// ------------------------------------------------------------
// 1. 1 曲作る。Number は Index + 1。Duration は秒から。
// ------------------------------------------------------------
let makeTrack (index: int) (title: string) (seconds: float) (source: TrackSource) : Track =
    failwith "TODO: makeTrack"

// ------------------------------------------------------------
// 2. タイトルだけ変えた新しい Track を返す（元は変更しない）。
// ------------------------------------------------------------
let renameTrack (track: Track) (newTitle: string) : Track =
    failwith "TODO: renameTrack"

// ------------------------------------------------------------
// 3. ディスクの合計時間（秒、整数）。
// ------------------------------------------------------------
let totalSeconds (disc: Disc) : int =
    failwith "TODO: totalSeconds"

// ------------------------------------------------------------
// 4. EqPreset.createBands と同じ要領で、3 バンドを返す。
//    ラベルと周波数: ("32", 32.0), ("1k", 1000.0), ("16k", 16000.0)
//    ゲインはすべて 0.0
// ------------------------------------------------------------
type EqBand = { Label: string; Frequency: float; GainDb: float }

module EqPreset =
    let createBands () : EqBand array =
        failwith "TODO: EqPreset.createBands"

// --- 自動検査 ---

let t0 = makeTrack 0 "ノイズ" 40.0 (DemoTone "Noise")
check "index" 0 t0.Index
check "number" 1 t0.Number
check "title" "ノイズ" t0.Title
check "dur" 40.0 t0.Duration.TotalSeconds
check "source" (DemoTone "Noise") t0.Source

let t1 = renameTrack t0 "ホワイトノイズ"
check "rename new" "ホワイトノイズ" t1.Title
check "rename old intact" "ノイズ" t0.Title

let t2 = makeTrack 1 "和音" 50.0 (AudioFile "chord.wav")
let disc = { Title = "デモ"; Artist = "LAB"; Tracks = [| t0; t2 |] }
check "total" 90 (totalSeconds disc)

let bands = EqPreset.createBands ()
check "band count" 3 bands.Length
check "band0" "32" bands.[0].Label
check "band1 freq" 1000.0 bands.[1].Frequency
check "band2 gain" 0.0 bands.[2].GainDb

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 03 は未完了です"
