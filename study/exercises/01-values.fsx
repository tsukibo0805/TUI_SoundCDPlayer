// 演習 01: 値と関数
// 実行: dotnet fsi study/exercises/01-values.fsx
// 対応レッスン: 02-値と関数.md
// 大きな処理を一度に書かず、a → b → c の小問を使って完成させます。

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
// 1-a. 負の秒を 0.0 に直す。
//      まず例を頭の中で確認: nonNegativeSeconds -3.0 = 0.0
// ------------------------------------------------------------
let nonNegativeSeconds (seconds: float) : float =
    failwith "TODO 1-a: max を使う"

// 1-b. 秒の小数部分を落として int にする。
let wholeSeconds (seconds: float) : int =
    seconds
    |> nonNegativeSeconds
    |> failwith "TODO 1-b: float を int に変換する関数を書く"

// 1-c. 整数秒から「分」と「60未満の秒」を別々に求める。
let minutesPart (total: int) : int =
    failwith "TODO 1-c: 60 で割る"

let secondsPart (total: int) : int =
    failwith "TODO 1-c: 60 で割った余りを求める"

// 1-d. 上の小さな関数を組み合わせ、2桁ずつの文字列にする。
//      書式の骨組みは用意済み。3つの TODO だけを埋める。
let formatMmss (totalSeconds: float) : string =
    let total = failwith "TODO 1-d: wholeSeconds を呼ぶ"
    let minutes = failwith "TODO 1-d: minutesPart を呼ぶ"
    let seconds = failwith "TODO 1-d: secondsPart を呼ぶ"
    sprintf "%02d:%02d" minutes seconds

// ------------------------------------------------------------
// 2. ゲインを -12.0 以上 12.0 以下に収める。
//    補助線: まず min db 12.0 で上限を作り、その結果に下限を適用する。
// ------------------------------------------------------------
let clampGain (db: float) : float =
    failwith "TODO: clampGain"

// ------------------------------------------------------------
// 3. セクタ数を秒にする。CD は 1 秒 = 75 セクタ。
//    式の左側は用意済み。int の sectors を何に変換すれば 75.0 で割れるか考える。
// ------------------------------------------------------------
let secondsFromSectors (sectors: int) : float =
    let sectorsAsFloat = failwith "TODO 3-a: float に変換"
    sectorsAsFloat / 75.0

// ------------------------------------------------------------
// 4. パイプラインの2つの空欄を埋めて「整数化 → 文字列化」する。
// ------------------------------------------------------------
let asIntString (x: float) : string =
    x
    |> failwith "TODO 4-a: 整数化する関数"
    |> failwith "TODO 4-b: 文字列化する関数"

// --- 自動検査（ここから下は変えなくてよい） ---

check "non-negative positive" 65.7 (nonNegativeSeconds 65.7)
check "non-negative negative" 0.0 (nonNegativeSeconds -3.0)
check "whole seconds" 65 (wholeSeconds 65.7)
check "minutes part" 1 (minutesPart 65)
check "seconds part" 5 (secondsPart 65)
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
