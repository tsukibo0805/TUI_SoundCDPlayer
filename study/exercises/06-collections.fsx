// 演習 06: コレクションとパイプライン
// 実行: dotnet fsi study/exercises/06-collections.fsx
// 対応レッスン: 07-コレクションとパイプライン.md

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

// ------------------------------------------------------------
// 1. 音声ファイルだけ残し、拡張子を小文字にしてソートした配列を返す。
//    入力はフルパスではなくファイル名でよい。
// ------------------------------------------------------------
let audioFiles (names: string array) : string array =
    failwith "TODO: audioFiles"

// ------------------------------------------------------------
// 2. ファイル名配列から Track 配列を作る。
//    Number は 1 始まり。Title は拡張子なし。Rip は None。
//    Ext は小文字の拡張子（先頭ドット付き）。
// ------------------------------------------------------------
let toTracks (names: string array) : Track array =
    failwith "TODO: toTracks"

// ------------------------------------------------------------
// 3. Rip があるトラックだけ (Number, Sectors) にする。
//    trackFilter が Some n なら、その番号だけ。
// ------------------------------------------------------------
let ripTargets (tracks: Track array) (trackFilter: int option) : (int * int) array =
    failwith "TODO: ripTargets"

// ------------------------------------------------------------
// 4. タイトルを "NN title" 形式で最大 n 件、改行連結する。
//    NN は 2 桁。例: "01 ノイズ"
// ------------------------------------------------------------
let listTitles (tracks: Track array) (n: int) : string =
    failwith "TODO: listTitles"

// --- 自動検査 ---

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
if failed > 0 then failwith "演習 06 は未完了です"
