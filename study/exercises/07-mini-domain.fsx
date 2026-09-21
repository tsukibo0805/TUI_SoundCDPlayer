// 演習 07: ミニ SOUND ドメイン（総合）
// 実行: dotnet fsi study/exercises/07-mini-domain.fsx
// 対応レッスン: 11-ソースコード案内と作り方.md
//
// 本番の Player.fs を見ずに解けると理想です。
// 見ながらでも、同じ形を自分の名前で書いてください。

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
// 1. mm:ss
// ------------------------------------------------------------
let mmss (t: TimeSpan) : string =
    failwith "TODO: mmss"

// ------------------------------------------------------------
// 2. デモディスク。次の 2 曲:
//    ノイズ / 40 秒 / DemoTone "Noise"
//    和音 / 50 秒 / DemoTone "Chord"
// ------------------------------------------------------------
let demoDisc () : Disc =
    failwith "TODO: demoDisc"

// ------------------------------------------------------------
// 3. 次の曲。シャッフルは無し。
//    RepeatOne は current のまま。
//    RepeatAll は末尾の次が 0。
//    RepeatOff は末尾の次も current のまま。
// ------------------------------------------------------------
let nextIndex (disc: Disc) (current: int) (repeat: RepeatMode) : int =
    failwith "TODO: nextIndex"

// ------------------------------------------------------------
// 4. 再生中の 1 行表示。
//    "PLAY  01/02  ノイズ  00:10/00:40"
//    トラックが無ければ "STOP  --/--  —  00:00/00:00"
// ------------------------------------------------------------
let statusLine (state: PlaybackState) (disc: Disc) (current: int) (position: TimeSpan) : string =
    failwith "TODO: statusLine"

// ------------------------------------------------------------
// 5. Space 相当。Stopped/Paused -> Playing、Playing -> Paused。
// ------------------------------------------------------------
let toggle (state: PlaybackState) : PlaybackState =
    failwith "TODO: toggle"

// --- 自動検査 ---

check "mmss" "01:05" (mmss (TimeSpan.FromSeconds 65.0))

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
