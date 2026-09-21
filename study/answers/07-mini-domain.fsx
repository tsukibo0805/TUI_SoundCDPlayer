// 解答 07: ミニ SOUND ドメイン

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

let mmss (t: TimeSpan) : string =
    let total = max 0.0 t.TotalSeconds
    let m = int total / 60
    let s = int total % 60
    sprintf "%02d:%02d" m s

let demoDisc () : Disc =
    let specs =
        [|
            "ノイズ", 40.0, DemoTone "Noise"
            "和音", 50.0, DemoTone "Chord"
        |]

    let tracks =
        specs
        |> Array.mapi (fun i (title, seconds, source) ->
            {
                Index = i
                Number = i + 1
                Title = title
                Duration = TimeSpan.FromSeconds seconds
                Source = source
            })

    { Title = "デモディスク"; Tracks = tracks }

let nextIndex (disc: Disc) (current: int) (repeat: RepeatMode) : int =
    let count = disc.Tracks.Length

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

let stateLabel state =
    match state with
    | Playing -> "PLAY"
    | Paused -> "PAUSE"
    | Stopped -> "STOP"

let statusLine (state: PlaybackState) (disc: Disc) (current: int) (position: TimeSpan) : string =
    let label = stateLabel state

    if disc.Tracks.Length = 0 then
        $"{label}  --/--  —  00:00/00:00"
    else
        let track = disc.Tracks[current]
        let num = sprintf "%02d" track.Number
        let total = sprintf "%02d" disc.Tracks.Length
        $"{label}  {num}/{total}  {track.Title}  {mmss position}/{mmss track.Duration}"

let toggle (state: PlaybackState) : PlaybackState =
    match state with
    | Stopped
    | Paused -> Playing
    | Playing -> Paused

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
if failed > 0 then failwith "解答 07 が落ちています"
