// 演習 04: パターンマッチ
// 実行: dotnet fsi study/exercises/04-matching.fsx
// 対応レッスン: 05-パターンマッチ.md
// 分岐の骨組みは用意済みです。1つの枝ずつ動作を決めます。

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

type FocusPane =
    | TrackList
    | Equalizer

type RepeatMode =
    | RepeatOff
    | RepeatAll
    | RepeatOne

type Command =
    | PlayPause
    | Seek of seconds: int
    | MoveEq of delta: int
    | AdjustGain of delta: int
    | Quit
    | Ignore

// ------------------------------------------------------------
// 1. キーとフォーカスからコマンドを決める。
//    "space"            -> PlayPause
//    "left" + TrackList -> Seek -5
//    "right" + TrackList -> Seek 5
//    "left" + Equalizer -> MoveEq -1
//    "right" + Equalizer -> MoveEq 1
//    "up"   + Equalizer -> AdjustGain 1
//    "down" + Equalizer -> AdjustGain -1
//    "q"                -> Quit
//    それ以外           -> Ignore
// ------------------------------------------------------------
let handle (key: string) (focus: FocusPane) : Command =
    match key, focus with
    | "space", _ -> PlayPause
    | "left", TrackList -> failwith "TODO 1-a: 5秒戻る Command"
    | "right", TrackList -> failwith "TODO 1-b: 5秒進む Command"
    | "left", Equalizer -> failwith "TODO 1-c: 左のバンドへ移る Command"
    | "right", Equalizer -> failwith "TODO 1-d: 右のバンドへ移る Command"
    | "up", Equalizer -> failwith "TODO 1-e: ゲインを上げる Command"
    | "down", Equalizer -> failwith "TODO 1-f: ゲインを下げる Command"
    | "q", _ -> failwith "TODO 1-g: 終了 Command"
    | _ -> failwith "TODO 1-h: 未知のキーの Command"

// ------------------------------------------------------------
// 2. 今の曲 index と曲数、リピートから「次の index」を返す。
//    最後の曲の次:
//      RepeatAll -> 0
//      RepeatOne -> 同じ index
//      RepeatOff -> 同じ index（進まない）
//    曲数が 0 なら 0。
// ------------------------------------------------------------
let nextIndex (current: int) (count: int) (repeat: RepeatMode) : int =
    if count = 0 then
        failwith "TODO 2-a: 空の配列で使える index"
    elif current + 1 < count then
        failwith "TODO 2-b: まだ次の曲がある場合"
    else
        match repeat with
        | RepeatAll -> failwith "TODO 2-c: 先頭へ戻る index"
        | RepeatOne -> failwith "TODO 2-d: 同じ曲の index"
        | RepeatOff -> failwith "TODO 2-e: 進まない場合の index"

// ------------------------------------------------------------
// 3-a. 3つの文字列を整数にできたときだけ、秒に変換する。
// ------------------------------------------------------------
let tryTimeParts (m: string) (s: string) (f: string) : int option =
    match Int32.TryParse m, Int32.TryParse s, Int32.TryParse f with
    | (true, minutes), (true, seconds), (true, frames) ->
        failwith "TODO 3-a: 計算結果を Some に入れる"
    | _ -> failwith "TODO 3-a: パース失敗を表す option"

// ------------------------------------------------------------
// 3-b. "mm:ss:ff" または "t:mm:ss:ff" を秒（整数）にする。
//    1 秒 = 75 フレーム。パースできなければ None。
//    例: "00:01:00" -> Some 1
//        "01:00:00:00" -> Some 0   （トラック番号は捨てて mm:ss:ff）
// ------------------------------------------------------------
let parseMmssff (text: string) : int option =
    match text.Split(':') with
    | [| m; s; f |] -> failwith "TODO 3-b: tryTimeParts を呼ぶ"
    | [| _track; m; s; f |] -> failwith "TODO 3-b: track を捨てて tryTimeParts を呼ぶ"
    | _ -> failwith "TODO 3-b: 要素数が違う場合"

// --- 自動検査 ---

check "space" PlayPause (handle "space" TrackList)
check "seek-" (Seek -5) (handle "left" TrackList)
check "seek+" (Seek 5) (handle "right" TrackList)
check "eq-" (MoveEq -1) (handle "left" Equalizer)
check "eq+" (MoveEq 1) (handle "right" Equalizer)
check "gain+" (AdjustGain 1) (handle "up" Equalizer)
check "gain-" (AdjustGain -1) (handle "down" Equalizer)
check "quit" Quit (handle "q" TrackList)
check "ignore" Ignore (handle "z" TrackList)

check "mid" 2 (nextIndex 1 4 RepeatOff)
check "wrap all" 0 (nextIndex 3 4 RepeatAll)
check "stay one" 3 (nextIndex 3 4 RepeatOne)
check "stay off" 3 (nextIndex 3 4 RepeatOff)
check "empty" 0 (nextIndex 0 0 RepeatAll)

check "time parts" (Some 1) (tryTimeParts "00" "01" "00")
check "bad parts" None (tryTimeParts "x" "01" "00")
check "mmssff" (Some 1) (parseMmssff "00:01:00")
check "tmsf" (Some 0) (parseMmssff "01:00:00:00")
check "bad" None (parseMmssff "nope")

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 04 は未完了です"
