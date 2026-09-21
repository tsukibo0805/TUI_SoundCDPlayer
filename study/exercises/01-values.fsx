// 演習 01: 値と関数
// 実行: dotnet fsi study/exercises/01-values.fsx
// 対応レッスン: 02-値と関数.md

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
// 1. 秒数を mm:ss にする。負の数は 00:00 にする。
//    ヒント: Domain.fs の TimeFmt.mmss
// ------------------------------------------------------------
let formatMmss (totalSeconds: float) : string =
    failwith "TODO: formatMmss"

// ------------------------------------------------------------
// 2. ゲインを -12.0 .. 12.0 に収める（float）。
// ------------------------------------------------------------
let clampGain (db: float) : float =
    failwith "TODO: clampGain"

// ------------------------------------------------------------
// 3. セクタ数を秒にする。CD は 1 秒 = 75 セクタ。
// ------------------------------------------------------------
let secondsFromSectors (sectors: int) : float =
    failwith "TODO: secondsFromSectors"

// ------------------------------------------------------------
// 4. パイプラインで「整数化 → 文字列」にする。
//    125.7 |> ... が "125" になるように。
// ------------------------------------------------------------
let asIntString (x: float) : string =
    failwith "TODO: asIntString"

// --- 自動検査（ここから下は変えなくてよい） ---

check "format 0" "00:00" (formatMmss 0.0)
check "format 65" "01:05" (formatMmss 65.0)
check "format 599" "09:59" (formatMmss 599.0)
check "format negative" "00:00" (formatMmss -3.0)
check "clamp high" 12.0 (clampGain 40.0)
check "clamp low" -12.0 (clampGain -99.0)
check "clamp mid" 3.5 (clampGain 3.5)
check "sectors 75" 1.0 (secondsFromSectors 75)
check "sectors 150" 2.0 (secondsFromSectors 150)
check "pipeline" "125" (asIntString 125.7)

printfn ""
printfn "%d passed, %d failed" passed failed
if failed > 0 then failwith "演習 01 は未完了です"
