# 06. Option と Result

「無いかもしれない」と「失敗するかもしれない」を、`null` や例外の代わりに型で表します。

対応コード: `CdDrive.tryLoadFirst`、`Ripper.extractTracks`、`Mci.send`

演習: `study/exercises/05-option-result.fsx`

## option

```fsharp
type Option<'T> =
    | None
    | Some of 'T
```

`Track.Rip` は `RipInfo option` です。デモトラックは `None`、デジタル CD は `Some rip` です。

`Disc.Drive` も `string option` です。フォルダ盤にドライブレターはありません。

作るとき:

```fsharp
let drive = Some "D"
let empty: string option = None
```

使うとき:

```fsharp
match disc.Drive with
| None -> status <- "取り出せる CD がありません"
| Some letter ->
    match CdDrive.eject letter with
    | Ok() -> ...
    | Error e -> status <- e
```

## よく使う関数

```fsharp
Option.map (fun t -> t.Title) track
Option.defaultValue "—" titleOpt
Option.isSome track.Rip
Option.bind (fun x -> ...) opt
```

`map` は「中身があるときだけ変換」です。`None` はそのまま `None` です。

`View.header` のトラック名がこれです。曲が無いときは `"—"` を出します。

`Array.tryPick` / `Array.tryFind` も option を返します。`Player` がデジタルハンドルを拾う箇所です。

```fsharp
next.Tracks
|> Array.tryPick (fun t ->
    match t.Source with
    | DigitalCd(h, _, _) when h <> 0n -> Some h
    | _ -> None)
|> Option.defaultValue 0n
```

「最初に見つかったハンドル、無ければ 0」です。

## Result

```fsharp
type Result<'T, 'TError> =
    | Ok of 'T
    | Error of 'TError
```

option は「無い」だけです。Result は「なぜ失敗したか」を残せます。

```fsharp
let send (command: string) =
    let code = Win32.mciSendStringW(...)
    if code <> 0 then
        Error $"MCI {code}: {command}"
    else
        Ok(buf.ToString())
```

成功したら文字列、失敗したら理由です。`try/with` でも書けますが、呼び出し側が無視しにくくなります。

このアプリの失敗型はだいたい `string` です。ユーザーにそのままステータス行へ出せるからです。

```fsharp
match CdDrive.tryLoadFirst () with
| Ok disc -> player.LoadDisc(disc, ...)
| Error e -> status <- e
```

`Ripper.extractTracks` は `Result<int, string * int>` です。成功なら曲数、失敗なら「メッセージと、そこまで書けた曲数」です。キャンセルと失敗で表示を変えるために、ペアを載せています。

## bind で繋ぐ

失敗したらそこで止める処理は `Result.bind` です。

```fsharp
send $"open {drive} type cdaudio alias {aliasName} wait"
|> Result.bind (fun _ -> send $"set {aliasName} time format tmsf wait")
```

1 つ目が `Error` なら、2 つ目は実行されません。`if` をネストせず、手順を縦に書けます。

option にも `Option.bind` があります。「無かったら終わり」です。

## 例外との使い分け

F# にも `try/with` はあります。SOUND では、.NET のライブラリが投げる例外を境界で受けています。

```fsharp
try
    use reader = new AudioFileReader(file)
    reader.TotalTime
with _ ->
    TimeSpan.Zero
```

壊れたファイルの長さが読めなくても、アルバム全体は落とさない、という判断です。

自分で設計する関数は、次の指針で十分です。

| 状況 | 使う型 |
|------|--------|
| 無いことが普通 | `option` |
| 失敗理由を呼び出し側が扱う | `Result` |
| 本当に続行不能（バグ、致命的 I/O） | 例外 |

`Program.main` の一番外は `try/with` で致命的エラーを表示して終了コード 1 を返します。

## 相互運用で出る option

`Array.tryFindIndex` は「見つからないかもしれない」ので option です。`ripCli` の `--out` 解析がそれです。

```fsharp
match Array.tryFindIndex (fun a -> a = "--out" || a = "-o") args with
| Some i when i + 1 < args.Length -> args[i + 1]
| _ -> Ripper.defaultDirectory disc.Title
```

`TryParse` は C# 由来で、`bool * 出力` です。

```fsharp
let mutable n = 0
if Int32.TryParse(args[i + 1], &n) then Some n else None
```

F# らしく option に包み直しています。外側の API は Result/option、内側の BCL は例外や `bool`、という二層です。

## 本番コードを読む

1. `Mci.send` が `Ok` / `Error` のどちらを返すか確認する
2. `App.run` の起動時、CD が無いとき `Error` をどう扱っているか見る（デモに落とさず、単に無視して既存のデモのまま）
3. `Track.Rip` が `None` のトラックを `X` で抽出すると、どんなメッセージになるか `StartRip` を読む

## 演習

`study/exercises/05-option-result.fsx` で、option / Result のケースを1段ずつ扱います。

1. `TryParse` の成功だけを `Some` にする
2. 同じ `option` 処理を、最初は `match`、次は `Option.defaultValue` で書く
3. パスの整形と検査を分け、成功値を `Ok`、理由を `Error` にする
4. `Ok()` から始め、コマンドを1件ずつ `Result.bind` でつなぐ

最初から短い書き方を狙わず、まず `Some` / `None`、`Ok` / `Error` の両方が見える形を完成させます。
