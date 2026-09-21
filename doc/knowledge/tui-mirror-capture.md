# TUI ミラー撮影: 調査が本命に届かなかった理由

日付: 2026-09-20  
対象: `src/ShareWindow.fs`  
確定した修正: `967b7a6`（黒判定）、`18e337f`（撮影元の固定）

## 本命だった結論

最大化で色が消えた主因は、**撮影対象 HWND の選び方ではない**。

`PrintWindow` は成功していた。そのあと黒い失敗画像とみなす判定が、最大化した暗い TUI を誤って捨て、Spectre 色のない文字コピー（`drawFallback`）に落としていた。

当時の判定:

```fsharp
// 格子サンプリングで lit/n < 5% なら黒とみなす
n > 0 && lit * 20 < n
```

サンプル点は概ね横 12 × 縦 8 で、窓の大きさに対する**割合**で捨てる。最大化すると Windows Terminal のクライアントは大きくなり、Spectre TUI は上側の数十行に留まり、残りは暗い空行になる。格子の大半が背景に当たり、5% を下回って棄却される。

Codex の修正 (`967b7a6`) は割合をやめ、明るい画素が **64 個**あれば採用する。暗い最大化画面でも、文字や枠が少しあれば通る。コミット内コメント:

> A percentage of the whole window falsely rejects a dark terminal when it is maximized.

別 PowerShell を拾う問題は別件で、次のコミット `18e337f` が `EnumWindows` 全列挙をやめ、`W` 押下時の窓を固定した。

## 公式仕様を先に読めば拾えたこと

自前ヒューリスティックや「たぶんこの HWND」より、使っている Win32 の Remarks を先に当たるべきだった。

### PrintWindow

[PrintWindow function (winuser.h)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow)

- 成功時は nonzero。失敗は zero。
- 対象窓のプロセスが `WM_PRINT` / `WM_PRINTCLIENT` で、渡した DC にその窓を描く。
- 公式に載っているフラグは `PW_CLIENTONLY` のみ。最大化で色が消える、という仕様はない。

ここから言えるのは、**成功したなら DC にはその窓が描いた画素が入っている**ということ。最大化はクライアントが大きいだけで、仕様上「成功なのに色だけ消える」ことは起きない。最大化で色なしなら、次に疑うのは戻り値が false か、**成功したビットマップを自前で捨てているか**である。HWND 列挙を先にいじるのは、この仕様と矛盾する。

### GetConsoleWindow

[GetConsoleWindow](https://learn.microsoft.com/en-us/windows/console/getconsolewindow)

Remarks にこうある。

> For an application that is hosted inside a **pseudoconsole** session, this function returns a window handle for message queue purposes only. The associated window is not displayed locally as the *pseudoconsole* is serializing all actions to a stream for presentation on another terminal window elsewhere.

Windows Terminal は ConPTY（pseudoconsole）である。[Pseudoconsoles](https://learn.microsoft.com/en-us/windows/console/pseudoconsoles) も、表示用のホスト窓は OS が作らず、別のターミナルが描くと書いている。

つまり `GetConsoleWindow()` を「見える TUI」として `PrintWindow` しても、ユーザーが見ている色付き画面にはならない。コンソールを優先する分岐は、この Remarks を読んだ時点で却下できた。最大化の誤棄却そのものはこの API の話ではないが、**仕様で「何を撮っているか」を固定してから、成功画像の判定を疑う**、という順番はここから出る。

Windows Terminal 側の実装メモでは、表示中の本体 HWND は概ね `GetAncestor(GetConsoleWindow(), GA_ROOTOWNER)` である（親を `SetParent` しなくなったあと、`GA_ROOT` では届かない）。全ターミナルを `EnumWindows` する前に、まずこの公式・準公式の関係を確認する。

### 調査手順への含意

1. 呼びている API の learn.microsoft.com を開く。戻り値と Remarks を読む。
2. 成功の定義（`PrintWindow` なら nonzero）と、失敗時だけフォールバックする、を仕様どおりに分ける。
3. 成功なのに画面がおかしいなら、仕様外の自前判定（割合黒判定など）を疑う。
4. `GetConsoleWindow` のように「非推奨・疑似コンソールでは表示されない」と明記されているハンドルを、見える窓の代わりに使わない。

## なぜこの調査ではその結論にならなかったか

公式の戻り値と Remarks を開かず、自前の黒判定と HWND 仮説だけで進めた。仕様を先に読んでいれば、「PrintWindow 成功＝その窓の画素」と「GetConsoleWindow は WT では非表示」で切り分けが終わっていた。

### 1. 棄却理由を測らなかった

`PrintWindow` の成否、クライアントサイズ、`lit` / `n` / 比率、HWND / クラス名を一度もログしなかった。失敗を「撮影できていない」と一括りにし、原因を HWND 探索にだけ求めた。

正しい切り分けは次の 3 段だった。

1. `PrintWindow` は true か
2. ビットマップは色付き TUI か、真っ黒か
3. 真っ黒でないのに捨てているなら、**判定側**が本命

最大化で色が消える報告を受けても、2 と 3 を見ずに 1 の「別の窓を撮っている」へ進んだ。

### 2. `isMostlyBlack` を正解のフィルタだと思い込んだ

この関数は全イテレーションでほぼそのまま残した。黒判定を疑わず、「通った画像 = 正しい窓」「落ちた = 窓が違う」と読んだ。最大化で落ちるのはヒューリスティックのスケール問題であり、窓が違う証拠ではなかった。

### 3. 先に立てた HWND 仮説を、後の症状にも使い回した

色が乗らない／枠と EQ が消える／全部オレンジ、は当初本当に別経路だった。

- `GetConsoleWindow()` のダミーを優先 → 色の薄い画像
- 失敗後の `View.plain` + GDI → 枠なし、または枠線判定で全文オレンジ

その物語が固まったあと、「最大化すると色が反映されない」も同じ原因に回収した。実際は、**大きい暗い実写を 5% ルールが捨てて、同じフォールバックに落ちていた**。症状が似ているので区別できなかった。

### 4. 「複数ターミナルを拾う版は色が良い」を、黒判定まで正しい証拠にした

`EnumWindows` 版（`38af5c2` 付近）は色が乗っていた。ただし黒判定は同じ 5% ルールである。色が出ていたのは、列挙が**小さい別コンソール**（TUI が画面を埋める窓）を拾い、割合判定を通っていた可能性がある。最大化した本体を撮ると、同じルールで落ちる。

「列挙に戻すと色が戻る」は、HWND が本命である証明にはならない。**どの大きさの窓が判定を通ったか**を見ないと、再現試験の意味が変わる。

### 5. Codex のコミット順が、優先度の答えだった

| 順 | コミット | 直したこと |
|----|----------|------------|
| 1 | `967b7a6` | 黒判定を個数へ。Discord 文言削除 |
| 2 | `18e337f` | 撮影元を `W` 時点で固定。全列挙を削除 |

色と最大化が先、別窓の混入が後。調査では HWND 固定と列挙の話を先にやり、5% ルールに触れなかった。

## 次に同じ症状を見たら

撮影パイプラインを疑うときは、推測の前に 1 フレーム分の数値を残す。

- HWND、クラス名、タイトル、クライアント `w×h`
- `PrintWindow` の戻り値
- 明るい画素数と、（割合判定を使うなら）分母と比率
- 採用したか、フォールバックしたか、その理由

棄却したビットマップをファイルに書けば、黒なのか色付き TUI なのかは一目で分かる。

割合で「ほぼ黒」を決めるのは、暗い UI × 可変サイズと相性が悪い。内容が画面の一部にしかない最大化ターミナルでは、固定個数か、文字領域に限った判定の方が壊れにくい。

症状が「色がない」でも、フォールバック描画と、実写棄却は別問題として扱う。

## 関連コード

- 撮影と判定: `src/ShareWindow.fs`（`TuiMirror`）
- フォールバック用テキスト: `src/View.fs` の `plain`
- 呼び出し: `src/App.fs` の `W` / `TuiMirror.toggle`
