// 解答 03: レコードとモジュール

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

let makeTrack (index: int) (title: string) (seconds: float) (source: TrackSource) : Track =
    {
        Index = index
        Number = index + 1
        Title = title
        Duration = TimeSpan.FromSeconds seconds
        Source = source
    }

let renameTrack (track: Track) (newTitle: string) : Track =
    { track with Title = newTitle }

let totalSeconds (disc: Disc) : int =
    disc.Tracks
    |> Array.sumBy (fun t -> int t.Duration.TotalSeconds)

type EqBand = { Label: string; Frequency: float; GainDb: float }

module EqPreset =
    let createBands () : EqBand array =
        [|
            { Label = "32"; Frequency = 32.0; GainDb = 0.0 }
            { Label = "1k"; Frequency = 1000.0; GainDb = 0.0 }
            { Label = "16k"; Frequency = 16000.0; GainDb = 0.0 }
        |]

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
if failed > 0 then failwith "解答 03 が落ちています"
