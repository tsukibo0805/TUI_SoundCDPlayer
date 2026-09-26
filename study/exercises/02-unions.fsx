// 演習 02: 判別共用体
// 実行: dotnet fsi study/exercises/02-unions.fsx
// 対応レッスン: 03-判別共用体.md
// match の外枠と一部の枝は用意済みです。未完成の枝を上から埋めます。

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
// 1. 再生状態の短いラベルを返す。
//    Stopped の枝を見本に、残り2枝の右辺だけを埋める。
//    仕様: Stopped="STOP"、Playing="PLAY"、Paused="PAUSE"。
// ------------------------------------------------------------
let stateLabel (state: PlaybackState) : string =
    match state with
    | Stopped -> "STOP"
    | Playing -> failwith "TODO 1-a: Playing のラベル"
    | Paused -> failwith "TODO 1-b: Paused のラベル"

// ------------------------------------------------------------
// 2. リピートを OFF → ALL → ONE → OFF と循環させる。
//    矢印の左側がパターン、右側が戻り値になる。
//    仕様: RepeatOff → RepeatAll → RepeatOne → RepeatOff の一方向に循環する。
// ------------------------------------------------------------
let nextRepeat (mode: RepeatMode) : RepeatMode =
    match mode with
    | RepeatOff -> RepeatAll
    | RepeatAll -> failwith "TODO 2-a: ALL の次"
    | RepeatOne -> failwith "TODO 2-b: ONE の次"

// ------------------------------------------------------------
// 3. ケースの中のデータを取り出し、音源の説明文を作る。
//    仕様: 接頭辞と区切り記号は次の完成例どおりとする。
//    AudioFile "a.wav"          -> "file:a.wav"
//    DemoTone Bass              -> "demo:Bass"
//    DigitalCd (150, 75)        -> "cd:150+75"
//    パターンで付けた名前が、右辺で使えることを確認する。
// ------------------------------------------------------------
let describeSource (source: TrackSource) : string =
    match source with
    | AudioFile path -> $"file:{path}"
    | DemoTone kind -> failwith "TODO 3-a: kind を string にして demo: の後ろへ置く"
    | DigitalCd(startLba, sectors) -> failwith "TODO 3-b: 2つの数を cd:開始+長さ の形にする"

// ------------------------------------------------------------
// 4. デジタル CD ならセクタ数、それ以外は 0。
//    開始位置は使わないため `_` で捨てる。2枝だけで書ける。
//    仕様: AudioFile と DemoTone はどちらも 0。DigitalCd は2番目の値 sectors を返す。
// ------------------------------------------------------------
let sectorCount (source: TrackSource) : int =
    match source with
    | DigitalCd(_, sectors) -> failwith "TODO 4-a: 取り出した値を返す"
    | _ -> failwith "TODO 4-b: CD 以外の値"

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
