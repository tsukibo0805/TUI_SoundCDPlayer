// 解答 02: 判別共用体

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

let stateLabel (state: PlaybackState) : string =
    match state with
    | Stopped -> "STOP"
    | Playing -> "PLAY"
    | Paused -> "PAUSE"

let nextRepeat (mode: RepeatMode) : RepeatMode =
    match mode with
    | RepeatOff -> RepeatAll
    | RepeatAll -> RepeatOne
    | RepeatOne -> RepeatOff

let describeSource (source: TrackSource) : string =
    match source with
    | AudioFile path -> $"file:{path}"
    | DemoTone kind -> $"demo:{kind}"
    | DigitalCd(lba, sectors) -> $"cd:{lba}+{sectors}"

let sectorCount (source: TrackSource) : int =
    match source with
    | DigitalCd(_, sectors) -> sectors
    | _ -> 0

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
if failed > 0 then failwith "解答 02 が落ちています"
