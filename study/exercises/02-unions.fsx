// 演習 02: 判別共用体
// 実行: dotnet fsi study/exercises/02-unions.fsx
// 対応レッスン: 03-判別共用体.md

let mutable passed = 0
let mutable failed = 0

let check name expected actual =
    if expected = actual then
        passed <- passed + 1
        printfn "OK   %s" name
    else
        failed <- failed + 1
        printfn "FAIL %s  expected=%A  actual=%A" name expected actual

type PlaybackState =
    | Stopped
    | Playing
    | Paused

type RepeatMode =
    | RepeatOff
    | RepeatAll
    | RepeatOne

type DemoKind =
    | Noise
    | Bass
    | Chord

type TrackSource =
    | AudioFile of path: string
    | DemoTone of kind: DemoKind
    | DigitalCd of startLba: int * sectors: int

// ------------------------------------------------------------
// 1. 再生状態の短いラベル。
//    Stopped -> "STOP" / Playing -> "PLAY" / Paused -> "PAUSE"
// ------------------------------------------------------------
let stateLabel (state: PlaybackState) : string =
    failwith "TODO: stateLabel"

// ------------------------------------------------------------
// 2. リピートを OFF → ALL → ONE → OFF と循環させる。
// ------------------------------------------------------------
let nextRepeat (mode: RepeatMode) : RepeatMode =
    failwith "TODO: nextRepeat"

// ------------------------------------------------------------
// 3. 音源の説明文。
//    AudioFile "a.wav"          -> "file:a.wav"
//    DemoTone Bass              -> "demo:Bass"
//    DigitalCd (150, 75)        -> "cd:150+75"
// ------------------------------------------------------------
let describeSource (source: TrackSource) : string =
    failwith "TODO: describeSource"

// ------------------------------------------------------------
// 4. デジタル CD ならセクタ数、それ以外は 0。
// ------------------------------------------------------------
let sectorCount (source: TrackSource) : int =
    failwith "TODO: sectorCount"

// --- 自動検査 ---

check "stop" "STOP" (stateLabel Stopped)
check "play" "PLAY" (stateLabel Playing)
check "pause" "PAUSE" (stateLabel Paused)
check "rep1" RepeatAll (nextRepeat RepeatOff)
check "rep2" RepeatOne (nextRepeat RepeatAll)
check "rep3" RepeatOff (nextRepeat RepeatOne)
check "file" "file:a.wav" (describeSource (AudioFile "a.wav"))
check "demo" "demo:Bass" (describeSource (DemoTone Bass))
check "cd" "cd:150+75" (describeSource (DigitalCd(150, 75)))
check "sectors cd" 75 (sectorCount (DigitalCd(150, 75)))
check "sectors file" 0 (sectorCount (AudioFile "x.mp3"))

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 02 は未完了です"
