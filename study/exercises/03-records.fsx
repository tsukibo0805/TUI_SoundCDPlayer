// 演習 03: レコードとモジュール
// 実行: dotnet fsi study/exercises/03-records.fsx
// 対応レッスン: 04-レコードとモジュール.md
// レコードの外枠は用意済みです。フィールドを1つずつ完成させます。

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
// 1. 引数から Track レコードを1つ作る。
//    Index / Title / Source は見本。Number と Duration を埋める。
// ------------------------------------------------------------
let makeTrack (index: int) (title: string) (seconds: float) (source: TrackSource) : Track =
    {
        Index = index
        Number = failwith "TODO 1-a: 1始まりの番号"
        Title = title
        Duration = failwith "TODO 1-b: TimeSpan.FromSeconds を呼ぶ"
        Source = source
    }

// ------------------------------------------------------------
// 2. タイトルだけ変えた新しい Track を返す（元は変更しない）。
//    `{ 元の値 with フィールド = 新しい値 }` の空欄を埋める。
// ------------------------------------------------------------
let renameTrack (track: Track) (newTitle: string) : Track =
    { track with Title = failwith "TODO 2: 新しいタイトル" }

// ------------------------------------------------------------
// 3. ディスクの全トラックの時間を合計し、整数秒で返す。
//    3-a で1曲の秒数、3-b で配列全体の合計、3-c で整数化する。
// ------------------------------------------------------------
let totalSeconds (disc: Disc) : int =
    disc.Tracks
    |> Array.sumBy (fun track -> (failwith "TODO 3-a: Track から秒数を取り出す" : float))
    |> failwith "TODO 3-b: 合計を int にする関数"

// ------------------------------------------------------------
// 4. EqBand の配列を返す。1つ目を見本に残り2つを作る。
//    ラベルと周波数: ("32", 32.0), ("1k", 1000.0), ("16k", 16000.0)
//    ゲインはすべて 0.0
// ------------------------------------------------------------
type EqBand = { Label: string; Frequency: float; GainDb: float }

module EqPreset =
    let createBands () : EqBand array =
        [|
            { Label = "32"; Frequency = 32.0; GainDb = 0.0 }
            failwith "TODO 4-a: 1k の EqBand レコード"
            failwith "TODO 4-b: 16k の EqBand レコード"
        |]

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
