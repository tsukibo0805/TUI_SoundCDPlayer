# 10. TUI とエントリポイント

状態機械ができたあと、残るのは「描く」「キーを読む」「プロセスを始める」です。

対応コード: `src/View.fs`, `src/App.fs`, `Program.fs`

## 描画は純関数に近い

`View.render` は `AudioPlayer` を受け取り、Spectre の描画ツリーを返します。プレイヤーを止めたり次の曲へ送ったりはしません。

```fsharp
let render (player: AudioPlayer) : IRenderable =
    let children = [
        yield header player :> IRenderable
        match ripPanel player with
        | Some panel -> yield panel
        | None -> ()
        yield spectrum player :> IRenderable
        yield equalizer player :> IRenderable
        yield (if player.ShowHelp then helpPanel () else tracks player)
        yield footer player :> IRenderable
    ]
    Rows(Array.ofList children) :> IRenderable
```

パネルの有無を `option` と `yield` で足しています。ヘルプを開くとトラック一覧の位置にヘルプが入ります。同じスロットです。

`plain` は色なしのテキストです。ミラー窓（`W`）がこれを表示します。TUI 用とプレーン用で、読むプロパティは同じです。

## Spectre の Live

`App.live` がメインループです。

```fsharp
AnsiConsole
    .Live(View.render player)
    .Start(fun ctx ->
        while player.Running && not exit do
            while Console.KeyAvailable do
                Input.handle player (Console.ReadKey true)
            match player.Pending with
            | OpenFolder | RipPrompt | Quit -> exit <- true
            | Idle ->
                player.Tick()
                ctx.UpdateTarget(View.render player)
                ctx.Refresh()
                Thread.Sleep 50)
```

50ms ごとに、

1. 溜まったキーを全部処理する
2. 保留が無ければ `Tick`（MCI の終端検出、スペクトル減衰）
3. 画面を作り直して差し替える

フォルダ入力や抽出確認は、このループの外です。`promptFolder` / `promptRip` のあと、`live player` を再帰呼び出しします。

## エントリポイント

実行ファイルには、起動関数が 1 つ要ります。

```fsharp
[<EntryPoint>]
let main args =
    if args |> Array.exists (fun a -> a = "--help" || a = "-h") then
        printf $"{helpText ()}"
        0
    elif args |> Array.exists (fun a -> a = "--self-check") then
        selfCheck ()
    ...
    else
        try
            App.run args
            0
        with ex ->
            eprintfn $"致命的エラー: {ex.Message}"
            1
```

- `args` は `string array`
- 戻り値 `int` がプロセスの終了コード（0 が成功）
- 属性 `[<EntryPoint>]` が「ここから始める」印

`dotnet run -- --demo` の `--demo` は、この `args` に入ります。`App.run` が順に消費します。

```fsharp
let rec consume i =
    if i >= args.Length then ()
    else
        match args[i] with
        | "--demo" | "-d" ->
            player.LoadDemo()
            consume (i + 1)
        | path when path.StartsWith("-") -> consume (i + 1)
        | path ->
            if IO.Directory.Exists path then player.LoadFolder path
            consume (i + 1)
```

再帰でインデックスを進める、典型的な CLI の書き方です。引数が空なら CD 検出を一度だけ試みます。

## 学習用の隠しコマンド

| 引数 | 動き |
|------|------|
| `--help` | テキストヘルプ。TUI を開かない |
| `--self-check` | `CdMath` とパイプラインの回帰 |
| `--smoke` | デモを短く再生して止める |
| `--render-once` | 画面を 1 フレームだけ出す |

講座中に TUI が崩れたと感じたら、`--self-check` が切り分けになります。ドメイン計算まで壊れているか、描画だけかの判断材料です。

```powershell
dotnet run -- --self-check
```

## Markup とエスケープ

Spectre は `[bold gold1]...[/]` のような色タグを持ちます。曲名に `[` が含まれるとタグとして壊れるので、`Markup.Escape` しています。

```fsharp
let private esc (text: string) = Markup.Escape(text)
```

ユーザー由来の文字列は、表示の直前で必ず逃します。F# というより UI の定番です。

## 再帰する UI

`live` は `let rec` です。プロンプトのあと自分を呼びます。`Quit` のときだけ本当に終わります。

```fsharp
match player.ClearPending() with
| Quit ->
    player.CancelRip()
    player.Stop()
    player.Finish()
| OpenFolder ->
    promptFolder player
    live player
| RipPrompt ->
    promptRip player
    live player
| Idle ->
    if player.Running then live player
```

`Finish` が `running <- false` です。ループ条件と対になっています。

## 本番コードを読む

1. `Input.handle` で `W` が `TuiMirror.toggle` を呼ぶこと
2. `View.render` の子どもパネルの順番（TRANSPORT → EXTRACT → SPECTRUM → EQ → TRACKS → footer）
3. `main` が `--rip` のとき TUI を開かず `ripCli` に行くこと

ここまでで、アプリを「上から下へ」説明できるはずです。最後のレッスンで、自分で作り直す順番を固定します。
