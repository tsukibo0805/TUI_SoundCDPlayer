// 解答 04: パターンマッチ

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

let handle (key: string) (focus: FocusPane) : Command =
    match key, focus with
    | "space", _ -> PlayPause
    | "left", TrackList -> Seek -5
    | "right", TrackList -> Seek 5
    | "left", Equalizer -> MoveEq -1
    | "right", Equalizer -> MoveEq 1
    | "up", Equalizer -> AdjustGain 1
    | "down", Equalizer -> AdjustGain -1
    | "q", _ -> Quit
    | _ -> Ignore

let nextIndex (current: int) (count: int) (repeat: RepeatMode) : int =
    if count = 0 then
        0
    else
        let n = current + 1

        if n < count then
            n
        else
            match repeat with
            | RepeatAll -> 0
            | RepeatOne
            | RepeatOff -> current

let parseMmssff (text: string) : int option =
    let parts = text.Split(':')

    let tryInt (s: string) =
        match Int32.TryParse s with
        | true, n -> Some n
        | _ -> None

    let toSeconds m s f =
        match tryInt m, tryInt s, tryInt f with
        | Some mv, Some sv, Some fv -> Some(mv * 60 + sv + fv / 75)
        | _ -> None

    match parts with
    | [| m; s; f |] -> toSeconds m s f
    | [| _; m; s; f |] -> toSeconds m s f
    | _ -> None

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

check "mmssff" (Some 1) (parseMmssff "00:01:00")
check "frames" (Some 1) (parseMmssff "00:00:75")
check "tmsf" (Some 0) (parseMmssff "01:00:00:00")
check "bad" None (parseMmssff "nope")

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "解答 04 が落ちています"
