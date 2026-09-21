// 解答 01: 値と関数

let mutable passed = 0
let mutable failed = 0

let check name expected actual =
    if expected = actual then
        passed <- passed + 1
        printfn "OK   %s" name
    else
        failed <- failed + 1
        printfn "FAIL %s  expected=%A  actual=%A" name expected actual

let formatMmss (totalSeconds: float) : string =
    let total = max 0.0 totalSeconds
    let m = int total / 60
    let s = int total % 60
    sprintf "%02d:%02d" m s

let clampGain (db: float) : float =
    min 12.0 (max -12.0 db)

let secondsFromSectors (sectors: int) : float =
    float sectors / 75.0

let asIntString (x: float) : string =
    x |> int |> string

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
if failed > 0 then failwith "解答 01 が落ちています"
