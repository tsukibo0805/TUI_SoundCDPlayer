// 解答 05: Option と Result

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

let parseTrackNumber (text: string) : int option =
    match Int32.TryParse text with
    | true, n when n >= 1 -> Some n
    | _ -> None

let displayTitle (title: string option) : string =
    title |> Option.defaultValue "—"

let validateOutputDir (path: string) : Result<string, string> =
    if String.IsNullOrWhiteSpace path then
        Error "empty"
    else
        Ok(path.Trim())

let openDrive (cmds: string list) : Result<string, string> =
    let send (cmd: string) =
        if cmd.StartsWith("ok:") then Ok()
        else Error cmd

    let rec run remaining =
        match remaining with
        | [] -> Ok "ready"
        | cmd :: rest -> send cmd |> Result.bind (fun _ -> run rest)

    run cmds

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
if failed > 0 then failwith "解答 05 が落ちています"
