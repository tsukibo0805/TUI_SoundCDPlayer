// 演習 06: コレクションとパイプライン
// 実行: dotnet fsi study/exercises/06-collections.fsx
// 対応レッスン: 07-コレクションとパイプライン.md
// 1段だけを担当する小さな関数を作り、最後にパイプでつなぎます。

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

// ------------------------------------------------------------
// 1-a. ファイル名から、小文字の拡張子を得る。
// ------------------------------------------------------------
let lowerExtension (name: string) : string =
    name
    |> Path.GetExtension
    |> failwith "TODO 1-a: 小文字にする"

// 1-b. audioExtensions に含まれるかを bool で返す。
let isAudioFile (name: string) : bool =
    let extension = failwith "TODO 1-b: lowerExtension を呼ぶ"
    failwith "TODO 1-b: Set に extension が含まれるか調べる"

// 1-c. 音声ファイルだけ残し、名前でソートする。
let audioFiles (names: string array) : string array =
    names
    |> Array.filter (fun name -> failwith "TODO 1-c: isAudioFile で判定")
    |> failwith "TODO 1-c: 配列をソートする"

// ------------------------------------------------------------
// 2-a. 1つのファイル名から Track を作る。
//    Number は 1 始まり。Title は拡張子なし。Rip は None。
//    Ext は小文字の拡張子（先頭ドット付き）。
// ------------------------------------------------------------
let toTrack (index: int) (name: string) : Track =
    {
        Number = failwith "TODO 2-a: 1始まりの番号"
        Title = failwith "TODO 2-a: 拡張子を除いた名前"
        Ext = failwith "TODO 2-a: lowerExtension を呼ぶ"
        Rip = None
    }

// 2-b. mapi から、上で作った toTrack を呼ぶ。
let toTracks (names: string array) : Track array =
    names
    |> Array.mapi (fun index name -> failwith "TODO 2-b: toTrack を呼ぶ")

// ------------------------------------------------------------
// 3. Rip があるトラックだけ (Number, Sectors) にする。
//    trackFilter が Some n なら、その番号だけ。
// ------------------------------------------------------------
let ripTargets (tracks: Track array) (trackFilter: int option) : (int * int) array =
    tracks
    |> Array.choose (fun track ->
        match track.Rip with
        | Some rip when trackFilter.IsNone || trackFilter = Some track.Number ->
            failwith "TODO 3-a: 番号とセクタ数を Some に入れる"
        | _ -> failwith "TODO 3-b: 対象外を choose から捨てる")

// ------------------------------------------------------------
// 4. タイトルを "NN title" 形式で最大 n 件、改行連結する。
//    NN は 2 桁。例: "01 ノイズ"
// ------------------------------------------------------------
let listTitles (tracks: Track array) (n: int) : string =
    tracks
    |> Array.truncate (failwith "TODO 4-a: 最大件数")
    |> Array.map (fun track -> failwith "TODO 4-b: 2桁番号とタイトルの文字列")
    |> failwith "TODO 4-c: 改行で連結する関数"

// --- 自動検査 ---

let files =
    [| "b.MP3"; "notes.txt"; "a.wav"; "cover.png"; "c.m4a" |]

check "lower extension" ".mp3" (lowerExtension "b.MP3")
check "audio yes" true (isAudioFile "b.MP3")
check "audio no" false (isAudioFile "notes.txt")
check
    "filter-sort"
    [| "a.wav"; "b.MP3"; "c.m4a" |]
    (audioFiles files)

let singleTrack = toTrack 0 "noise.WAV"
check "single number" 1 singleTrack.Number
check "single title" "noise" singleTrack.Title
check "single ext" ".wav" singleTrack.Ext

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
