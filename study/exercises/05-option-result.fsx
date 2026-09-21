// 演習 05: Option と Result
// 実行: dotnet fsi study/exercises/05-option-result.fsx
// 対応レッスン: 06-OptionとResult.md

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
// 1. トラック番号をパースする。1 以上の整数だけ Some。
// ------------------------------------------------------------
let parseTrackNumber (text: string) : int option =
    failwith "TODO: parseTrackNumber"

// ------------------------------------------------------------
// 2. option の曲名を表示用にする。None なら "—"。
// ------------------------------------------------------------
let displayTitle (title: string option) : string =
    failwith "TODO: displayTitle"

// ------------------------------------------------------------
// 3. 出力先パスを検査する。
//    空 / 空白のみ -> Error "empty"
//    それ以外     -> Ok (前後空白を除いた文字列)
// ------------------------------------------------------------
let validateOutputDir (path: string) : Result<string, string> =
    failwith "TODO: validateOutputDir"

// ------------------------------------------------------------
// 4. コマンド列を順に実行する。どれかが Error ならそこで止める。
//    全部 Ok なら Ok "ready"
//    ヒント: Result.bind
// ------------------------------------------------------------
let openDrive (cmds: string list) : Result<string, string> =
    let send (cmd: string) =
        if cmd.StartsWith("ok:") then Ok()
        else Error cmd

    failwith "TODO: openDrive  上の send を bind で繋ぐ"

// --- 自動検査 ---

check "n1" (Some 3) (parseTrackNumber "3")
check "n0" None (parseTrackNumber "0")
check "neg" None (parseTrackNumber "-1")
check "nan" None (parseTrackNumber "x")

check "some title" "和音" (displayTitle (Some "和音"))
check "none title" "—" (displayTitle None)

check "dir ok" (Ok "C:\\rips") (validateOutputDir "  C:\\rips  ")
check "dir empty" (Error "empty") (validateOutputDir "   ")

check "bind ok" (Ok "ready") (openDrive [ "ok:open"; "ok:format" ])
check "bind stop" (Error "bad") (openDrive [ "ok:open"; "bad"; "ok:play" ])

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 05 は未完了です"
