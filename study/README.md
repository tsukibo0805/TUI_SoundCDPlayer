# SOUND F# 学習講座

このフォルダは、**SOUND**（F# 製 TUI CD プレイヤー）を読める・直せる・自分で組み直せるようになるための初心者講座です。

本番コードは親フォルダにあります。ここ (`study/`) は学習専用です。

## この講座のゴール

終わったとき、次ができるようになります。

1. F# の値・関数・型・パターンマッチを自分で書ける
2. `src/Domain.fs` の型設計を説明できる
3. 再生・停止・EQ・TUI がどのファイルで動いているか追える
4. 小さな機能を、既存の型に合わせて追加できる

C# や Python の経験があると読みやすいですが、必須ではありません。

## 進め方

各レッスンは同じ流れです。

1. レッスンを読む（概念）
2. 指示された本番ファイルを開く（実物）
3. `exercises/` を解く
4. 詰まったら `answers/` を見る

演習は F# Interactive で実行します。

```powershell
dotnet fsi study/exercises/01-values.fsx
```

解答も同じです。

```powershell
dotnet fsi study/answers/01-values.fsx
```

1 レッスンあたり 30〜60 分が目安です。毎日 1 本で約 2 週間です。

## カリキュラム

| # | レッスン | 本番コード | 演習 |
|---|----------|------------|------|
| 00 | [はじめに](00-はじめに.md) | `README.md` | なし |
| 01 | [環境と最初のプログラム](01-環境と最初のプログラム.md) | `Sound.CdPlayer.fsproj` | なし |
| 02 | [値と関数](02-値と関数.md) | `src/Domain.fs` の `TimeFmt` | [01](exercises/01-values.fsx) |
| 03 | [判別共用体](03-判別共用体.md) | `PlaybackState`, `TrackSource` | [02](exercises/02-unions.fsx) |
| 04 | [レコードとモジュール](04-レコードとモジュール.md) | `Track`, `Disc`, `EqPreset` | [03](exercises/03-records.fsx) |
| 05 | [パターンマッチ](05-パターンマッチ.md) | `src/App.fs` の `Input` | [04](exercises/04-matching.fsx) |
| 06 | [Option と Result](06-OptionとResult.md) | `CdDrive`, `Ripper` | [05](exercises/05-option-result.fsx) |
| 07 | [コレクションとパイプライン](07-コレクションとパイプライン.md) | `Player.LoadFolder` | [06](exercises/06-collections.fsx) |
| 08 | [クラスと可変状態](08-クラスと可変状態.md) | `src/Player.fs` | なし |
| 09 | [インターフェイスと音声パイプライン](09-インターフェイスと音声パイプライン.md) | `Equalizer.fs`, `Spectrum.fs` | なし |
| 10 | [TUI とエントリポイント](10-TUIとエントリポイント.md) | `View.fs`, `Program.fs` | なし |
| 11 | [ソースコード案内と作り方](11-ソースコード案内と作り方.md) | 全体 | [07](exercises/07-mini-domain.fsx) |

08 以降は読む＋本番コードを追うレッスンです。手を動かす総合演習は 11 のあとに `07-mini-domain.fsx` で行います。

## 推奨する学習順（アプリを「作る」視点）

SOUND をゼロから組み立てるなら、この順が自然です。レッスンもこの順に沿っています。

```
Domain（型）
  → TimeFmt / CdMath（純関数）
    → DemoSignal（ハードウェア不要の音源）
      → Player（状態機械）
        → View / App（画面とキー入力）
          → Program（起動と CLI）
            → Equalizer / Spectrum（音声加工）
              → CdDrive / Ripper（Windows 連携）
```

最初の 6 段まで理解できれば、アプリの骨格は自分で書けます。CD 読み取りと FFT は後回しで構いません。

## 必要なもの

- Windows
- [.NET SDK](https://dotnet.microsoft.com/)（このアプリは `net10.0-windows`）
- 任意のエディタ（Cursor / VS Code + Ionide が扱いやすい）

光学ドライブは学習には不要です。デモディスクで十分です。

```powershell
dotnet run -- --demo
```

## このフォルダの約束

- 教材はここだけに置く
- 本番の `src/` はレッスンで「読む」対象。学習用に書き換えない
- 試したい実験は `study/scratch.fsx` など、このフォルダ内に置く
