// 演習 05: Option と Result
// 実行: dotnet fsi study/exercises/05-option-result.fsx
// 対応レッスン: 06-OptionとResult.md
// Option / Result のケースを開く骨組みは用意済みです。

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

// ------------------------------------------------------------
// 1. トラック番号をパースする。1以上の整数だけ Some にする。
//    TryParse は `(成功したか, 変換後の値)` を返す。
// ------------------------------------------------------------
let parseTrackNumber (text: string) : int option =
    match Int32.TryParse text with
    | true, number when number >= 1 -> failwith "TODO 1-a: 成功した値を option に包む"
    | _ -> failwith "TODO 1-b: 0・負数・文字列の場合"

// ------------------------------------------------------------
// 2-a. option の曲名を match で表示用にする。None なら "—"。
// ------------------------------------------------------------
let displayTitleWithMatch (title: string option) : string =
    match title with
    | Some value -> failwith "TODO 2-a: 中の文字列"
    | None -> failwith "TODO 2-a: 曲がないときの表示"

// 2-b. 同じ処理を Option.defaultValue を使って1行で書く。
let displayTitle (title: string option) : string =
    failwith "TODO 2-b: Option.defaultValue"

// ------------------------------------------------------------
// 3. 出力先パスを検査する。
//    空 / 空白のみ -> Error "empty"
//    それ以外     -> Ok (前後空白を除いた文字列)
// ------------------------------------------------------------
let validateOutputDir (path: string) : Result<string, string> =
    let trimmed = failwith "TODO 3-a: path の前後空白を除く"

    if String.IsNullOrWhiteSpace trimmed then
        failwith "TODO 3-b: 失敗理由を Result に入れる"
    else
        failwith "TODO 3-c: trimmed を成功値にする"

// ------------------------------------------------------------
// 4. コマンド列を順に実行する。どれかが Error ならそこで止める。
//    全部 Ok なら Ok "ready"
//    fold の枠は用意済み。4-a → 4-b → 4-c の順に埋める。
// ------------------------------------------------------------
let openDrive (cmds: string list) : Result<string, string> =
    let send (cmd: string) =
        if cmd.StartsWith("ok:") then Ok()
        else Error cmd

    let initial: Result<unit, string> = failwith "TODO 4-a: まだ失敗していない初期値"

    cmds
    |> List.fold (fun previous cmd ->
        previous
        |> Result.bind (fun () -> failwith "TODO 4-b: send で現在の cmd を実行")) initial
    |> Result.map (fun () -> failwith "TODO 4-c: 全成功時の文字列")

// --- 自動検査 ---

check "n1" (Some 3) (parseTrackNumber "3")
check "n0" None (parseTrackNumber "0")
check "neg" None (parseTrackNumber "-1")
check "nan" None (parseTrackNumber "x")

check "match some" "和音" (displayTitleWithMatch (Some "和音"))
check "match none" "—" (displayTitleWithMatch None)
check "some title" "和音" (displayTitle (Some "和音"))
check "none title" "—" (displayTitle None)

check "dir ok" (Ok "C:\\rips") (validateOutputDir "  C:\\rips  ")
check "dir empty" (Error "empty") (validateOutputDir "   ")

check "bind ok" (Ok "ready") (openDrive [ "ok:open"; "ok:format" ])
check "bind stop" (Error "bad") (openDrive [ "ok:open"; "bad"; "ok:play" ])

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 05 は未完了です"
