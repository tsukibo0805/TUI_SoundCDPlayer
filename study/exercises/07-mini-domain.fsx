// 演習 07: ミニ SOUND ドメイン（総合）
// 実行: dotnet fsi study/exercises/07-mini-domain.fsx
// 対応レッスン: 11-ソースコード案内と作り方.md
//
// 本番の Player.fs を見ずに解けると理想です。
// 見ながらでも、同じ形を自分の名前で書いてください。
// これは復習問題です。以前の演習より補助線は少しだけ減っていますが、
// 外枠は用意してあります。小問 a, b, c の順に埋めてください。

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

// ===== ここから型を完成させる =====

type PlaybackState =
    | Stopped
    | Playing
    | Paused

type RepeatMode =
    | RepeatOff
    | RepeatAll
    | RepeatOne

type TrackSource =
    | DemoTone of name: string
    | AudioFile of path: string

type Track = {
    Index: int
    Number: int
    Title: string
    Duration: TimeSpan
    Source: TrackSource
}

type Disc = {
    Title: string
    Tracks: Track array
}

// ------------------------------------------------------------
// 1. TimeSpan を "mm:ss" にする（01-values.fsx の復習）。
// ------------------------------------------------------------
let mmss (t: TimeSpan) : string =
    let total = failwith "TODO 1-a: TotalSeconds を int にする"
    let minutes = failwith "TODO 1-b: 分を求める"
    let seconds = failwith "TODO 1-c: 60未満の秒を求める"
    sprintf "%02d:%02d" minutes seconds

// ------------------------------------------------------------
// 2-a. Trackを1つ作る補助関数を完成させる。
// ------------------------------------------------------------
let makeDemoTrack index title seconds kind : Track =
    {
        Index = index
        Number = failwith "TODO 2-a: 1始まりの番号"
        Title = title
        Duration = failwith "TODO 2-a: 秒から TimeSpan を作る"
        Source = failwith "TODO 2-a: kind を DemoTone に入れる"
    }

// 2-b. 上の関数を2回呼び、Discを組み立てる。
//    Title は "デモディスク"。Tracks は次の2曲:
//    ノイズ / 40 秒 / DemoTone "Noise"
//    和音 / 50 秒 / DemoTone "Chord"
// ------------------------------------------------------------
let demoDisc () : Disc =
    {
        Title = failwith "TODO 2-b: ディスク名"
        Tracks =
            [|
                failwith "TODO 2-b: 1曲目を作る"
                failwith "TODO 2-b: 2曲目を作る"
            |]
    }

// ------------------------------------------------------------
// 3. 次の曲の index を計算する。シャッフルは無し。
//    RepeatOne は current のまま。
//    RepeatAll は末尾の次が 0。
//    RepeatOff は末尾の次も current のまま。
// ------------------------------------------------------------
let nextIndex (disc: Disc) (current: int) (repeat: RepeatMode) : int =
    if disc.Tracks.Length = 0 then
        failwith "TODO 3-a: 空の場合"
    elif repeat = RepeatOne then
        failwith "TODO 3-b: 1曲リピートの場合"
    elif current + 1 < disc.Tracks.Length then
        failwith "TODO 3-c: 次の曲がある場合"
    else
        match repeat with
        | RepeatAll -> failwith "TODO 3-d: 末尾から先頭へ"
        | RepeatOff -> failwith "TODO 3-e: 末尾で停止"
        | RepeatOne -> current // 上で処理済み。網羅性のため残す

// ------------------------------------------------------------
// 4-a. 状態を短い文字列にする。
// ------------------------------------------------------------
let stateLabel state =
    match state with
    | Stopped -> "STOP"
    | Playing -> failwith "TODO 4-a: Playing の表示"
    | Paused -> failwith "TODO 4-a: Paused の表示"

// 4-b. 状態を画面用の1行にする。
//    "PLAY  01/02  ノイズ  00:10/00:40"
//    トラックが無ければ "STOP  --/--  —  00:00/00:00"
// ------------------------------------------------------------
let statusLine (state: PlaybackState) (disc: Disc) (current: int) (position: TimeSpan) : string =
    if disc.Tracks.Length = 0 then
        failwith "TODO 4-b: 空ディスクの完成例を返す"
    else
        let track = failwith "TODO 4-c: current 番目の Track"
        let label = failwith "TODO 4-d: stateLabel を呼ぶ"
        let positionText = failwith "TODO 4-e: position を mmss にする"
        let durationText = failwith "TODO 4-f: 曲の Duration を mmss にする"
        sprintf "%s  %02d/%02d  %s  %s/%s"
            label track.Number disc.Tracks.Length track.Title positionText durationText

// ------------------------------------------------------------
// 5. Space キー相当。最初の枝を見本に、残り2枝を埋める。
// ------------------------------------------------------------
let toggle (state: PlaybackState) : PlaybackState =
    match state with
    | Stopped -> Playing
    | Paused -> failwith "TODO 5-a: 一時停止中に Space"
    | Playing -> failwith "TODO 5-b: 再生中に Space"

// --- 自動検査 ---

check "mmss" "01:05" (mmss (TimeSpan.FromSeconds 65.0))

let sampleTrack = makeDemoTrack 0 "ノイズ" 40.0 "Noise"
check "make track index" 0 sampleTrack.Index
check "make track number" 1 sampleTrack.Number
check "make track duration" 40.0 sampleTrack.Duration.TotalSeconds
check "make track source" (DemoTone "Noise") sampleTrack.Source

let disc = demoDisc ()
check "disc title" "デモディスク" disc.Title
check "track count" 2 disc.Tracks.Length
check "t0" "ノイズ" disc.Tracks.[0].Title
check "t1 num" 2 disc.Tracks.[1].Number
check "t1 src" (DemoTone "Chord") disc.Tracks.[1].Source

check "next mid" 1 (nextIndex disc 0 RepeatOff)
check "next all" 0 (nextIndex disc 1 RepeatAll)
check "next one" 1 (nextIndex disc 1 RepeatOne)
check "next off" 1 (nextIndex disc 1 RepeatOff)

check "state stop" "STOP" (stateLabel Stopped)
check "state play" "PLAY" (stateLabel Playing)
check "state pause" "PAUSE" (stateLabel Paused)

check
    "status"
    "PLAY  01/02  ノイズ  00:10/00:40"
    (statusLine Playing disc 0 (TimeSpan.FromSeconds 10.0))

check
    "status empty"
    "STOP  --/--  —  00:00/00:00"
    (statusLine Stopped { Title = "空"; Tracks = [||] } 0 TimeSpan.Zero)

check "toggle stop" Playing (toggle Stopped)
check "toggle play" Paused (toggle Playing)
check "toggle pause" Playing (toggle Paused)

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 07 は未完了です"
