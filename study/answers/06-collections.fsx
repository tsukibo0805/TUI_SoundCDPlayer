// 解答 06: コレクションとパイプライン

open System.IO

let mutable passed = 0
let mutable failed = 0

let check name expected actual =
    if expected = actual then
        passed <- passed + 1
        printfn "OK   %s" name
    else
        failed <- failed + 1
        printfn "FAIL %s  expected=%A  actual=%A" name expected actual

let audioExtensions = set [ ".wav"; ".mp3"; ".m4a" ]

type RipInfo = { StartLba: int; Sectors: int }

type Track = {
    Number: int
    Title: string
    Ext: string
    Rip: RipInfo option
}

let audioFiles (names: string array) : string array =
    names
    |> Array.filter (fun name ->
        audioExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
    |> Array.sort

let toTracks (names: string array) : Track array =
    names
    |> Array.mapi (fun i name ->
        {
            Number = i + 1
            Title = Path.GetFileNameWithoutExtension name
            Ext = Path.GetExtension(name).ToLowerInvariant()
            Rip = None
        })

let ripTargets (tracks: Track array) (trackFilter: int option) : (int * int) array =
    tracks
    |> Array.choose (fun t ->
        match t.Rip with
        | Some rip when trackFilter.IsNone || trackFilter = Some t.Number ->
            Some(t.Number, rip.Sectors)
        | _ -> None)

let listTitles (tracks: Track array) (n: int) : string =
    tracks
    |> Array.truncate n
    |> Array.map (fun t -> sprintf "%02d %s" t.Number t.Title)
    |> String.concat "\n"

let files =
    [| "b.MP3"; "notes.txt"; "a.wav"; "cover.png"; "c.m4a" |]

check
    "filter-sort"
    [| "a.wav"; "b.MP3"; "c.m4a" |]
    (audioFiles files)

let tracks = toTracks [| "noise.wav"; "bass.mp3" |]
check "len" 2 tracks.Length
check "num1" 1 tracks.[0].Number
check "title" "bass" tracks.[1].Title
check "ext" ".mp3" tracks.[1].Ext
check "rip none" None tracks.[0].Rip

let mixed =
    [|
        { Number = 1; Title = "A"; Ext = ".wav"; Rip = Some { StartLba = 0; Sectors = 75 } }
        { Number = 2; Title = "B"; Ext = ".wav"; Rip = None }
        { Number = 3; Title = "C"; Ext = ".wav"; Rip = Some { StartLba = 75; Sectors = 150 } }
    |]

check "all rips" [| (1, 75); (3, 150) |] (ripTargets mixed None)
check "one rip" [| (3, 150) |] (ripTargets mixed (Some 3))
check "list2" "01 A\n02 B" (listTitles mixed 2)

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "解答 06 が落ちています"
