namespace Sound

open System
open System.Text
open Spectre.Console
open Spectre.Console.Rendering

module View =
    let private esc (text: string) = Markup.Escape(text)

    let private blocks = [| " "; "▁"; "▂"; "▃"; "▄"; "▅"; "▆"; "▇"; "█" |]

    let private barChar (value: float32) =
        let i = int (Math.Clamp(value, 0.f, 1.f) * float32 (blocks.Length - 1))
        blocks[i]

    let private transport (state: PlaybackState) =
        match state with
        | Playing -> "[bold green]▶ PLAY[/]"
        | Paused -> "[bold yellow]❚❚ PAUSE[/]"
        | Stopped -> "[grey]■ STOP[/]"

    let private progressBar (pos: TimeSpan) (dur: TimeSpan) (width: int) =
        let w = max 12 width
        let ratio =
            if dur.TotalSeconds <= 0.0 then
                0.0
            else
                Math.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0.0, 1.0)

        let filled = int (ratio * float (w - 1))
        let sb = StringBuilder(w)

        for i = 0 to w - 1 do
            if i = filled then
                sb.Append("●") |> ignore
            elif i < filled then
                sb.Append("━") |> ignore
            else
                sb.Append("─") |> ignore

        sb.ToString()

    let private volumeBar (volume: float32) =
        let w = 12
        let n = int (volume * float32 w)
        let filled = String.replicate n "▓"
        let empty = String.replicate (w - n) "░"
        $"{filled}{empty} {int (volume * 100.f)}" + "%"

    let private header (player: AudioPlayer) =
        let disc = player.Disc
        let track = player.CurrentTrack
        let title = track |> Option.map (fun t -> t.Title) |> Option.defaultValue "—"
        let number = track |> Option.map (fun t -> t.Number) |> Option.defaultValue 0
        let total = disc.Tracks.Length
        let pos = TimeFmt.mmss player.Position
        let dur = TimeFmt.mmss player.Duration
        let width = max 40 (AnsiConsole.Profile.Width - 10)
        let bar = progressBar player.Position player.Duration (width - 16)
        let drive = disc.Drive |> Option.map (fun d -> $"{d}:") |> Option.defaultValue "—"
        let shuffle = if player.Shuffle then "[gold1]SHUFFLE[/]" else "[grey]SHUFFLE[/]"
        let eq = if player.EqEnabled then "[springgreen1]EQ[/]" else "[grey]EQ[/]"
        let numberText = sprintf "%02d" number
        let totalText = sprintf "%02d" total
        let mciHint =
            match disc.Kind with
            | MciAudioCd -> "  [grey]※ MCI 経路では EQ は音に反映されません[/]"
            | _ -> ""

        let body =
            $"""[bold gold1]◆ SOUND[/]  [grey]TUI CD PLAYER[/]
{transport player.State}   [khaki1]{esc disc.Kind.Label}[/]  {esc drive}   {esc disc.Title}
[grey]{esc disc.Artist}[/]

[bold]{numberText}[/] / {totalText}   [bold wheat1]{esc title}[/]

[grey]{pos}[/] [gold1]{bar}[/] [grey]{dur}[/]

VOL {volumeBar player.Volume}   REPEAT [italic]{player.Repeat.Label}[/]   {shuffle}   {eq}{mciHint}"""

        Panel(Markup(body))
            .Header(" TRANSPORT ")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("gold1"))
            .Padding(1, 0, 1, 0)

    let private ripPanel (player: AudioPlayer) =
        match player.RipState with
        | RipIdle -> None
        | RipRunning p ->
            let width = max 20 (AnsiConsole.Profile.Width - 18)
            let bar = progressBar (TimeSpan.FromSeconds(p.Fraction * 100.0)) (TimeSpan.FromSeconds 100.0) width
            let pct = int (p.Fraction * 100.0)
            let pctText = string pct + "%"
            let body =
                $"""[bold gold1]CD → WAV[/]  {p.Current} / {p.Total}   [wheat1]{esc p.Label}[/]
[gold1]{bar}[/]  {pctText}
[grey]{esc p.Destination}[/]
[grey]S または Esc でキャンセル[/]"""

            Some(
                Panel(Markup(body))
                    .Header(" EXTRACT ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("springgreen1"))
                    .Padding(1, 0, 1, 0)
                :> IRenderable)
        | RipDone(n, dest) ->
            Some(
                Panel(Markup($"[springgreen1]{n} 曲を書き出しました[/]\n[grey]{esc dest}[/]"))
                    .Header(" EXTRACT ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("springgreen1"))
                :> IRenderable)
        | RipFailed msg ->
            Some(
                Panel(Markup($"[red1]{esc msg}[/]"))
                    .Header(" EXTRACT ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("red1"))
                :> IRenderable)
        | RipCanceled dest ->
            Some(
                Panel(Markup($"[yellow]抽出をキャンセルしました[/]\n[grey]{esc dest}[/]"))
                    .Header(" EXTRACT ")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("yellow"))
                :> IRenderable)

    let private spectrum (player: AudioPlayer) =
        let width = max 24 (AnsiConsole.Profile.Width - 8)
        let bars, peaks = player.Analyzer.Snapshot()
        let take = min bars.Length width
        let sb = StringBuilder(take * 2)

        for i = 0 to take - 1 do
            let v = bars[i]
            let ch = barChar v
            let color =
                if v > 0.78f then "red1"
                elif v > 0.5f then "gold1"
                elif v > 0.22f then "khaki1"
                else "grey39"

            let peak = if peaks[i] > v + 0.08f then "[grey]˙[/]" else ""
            sb.Append($"[{color}]{ch}[/]{peak}") |> ignore

        Panel(Markup(sb.ToString()))
            .Header(" SPECTRUM ")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey39"))
            .Padding(1, 0, 1, 0)

    let private eqColumn (gain: float32) (selected: bool) (focused: bool) (rows: int) =
        let mid = rows / 2
        let span = 12.f
        let cells = ResizeArray<string>()

        for row = 0 to rows - 1 do
            let level = span - (float32 row / float32 (rows - 1)) * (span * 2.f)
            let on =
                if gain >= 0.f then
                    row <= mid && level <= gain + 0.75f && level >= 0.f || row = mid
                else
                    row >= mid && level >= gain - 0.75f && level <= 0.f || row = mid

            let glyph =
                if row = mid && abs gain < 0.4f then "─"
                elif on then "█"
                else "│"

            let color =
                if selected && focused then "gold1"
                elif on && gain > 0.f then "springgreen1"
                elif on && gain < 0.f then "deepskyblue1"
                else "grey35"

            cells.Add($"[{color}]{glyph}[/]")

        cells

    let private equalizer (player: AudioPlayer) =
        let focused = player.Focus = Equalizer
        let rows = 9
        let grid = Grid().AddColumn()

        let meters = Grid()

        for _ in player.Bands do
            meters.AddColumn(GridColumn(Alignment = Justify.Center)) |> ignore

        let columns =
            player.Bands
            |> Array.mapi (fun i band -> eqColumn band.GainDb (i = player.EqIndex) focused rows)

        for row = 0 to rows - 1 do
            let cells = columns |> Array.map (fun col -> Markup(col[row]) :> IRenderable)
            meters.AddRow(cells) |> ignore

        let labels =
            player.Bands
            |> Array.mapi (fun i band ->
                let selected = i = player.EqIndex && focused
                let style = if selected then "underline bold gold1" else "grey"
                Markup($"[{style}]{band.Label}[/]") :> IRenderable)

        meters.AddRow(labels) |> ignore

        let gains =
            player.Bands
            |> Array.mapi (fun i band ->
                let selected = i = player.EqIndex && focused
                let style = if selected then "bold gold1" else "grey70"
                let sign = if band.GainDb > 0.f then "+" else ""
                let gain = int (Math.Round(float band.GainDb))
                Markup($"[{style}]{sign}{gain}[/]") :> IRenderable)

        meters.AddRow(gains) |> ignore

        let hint =
            if focused then
                Markup("[grey]←→ バンド   ↑↓ または +/-  ゲイン   F フラット   T バイパス[/]")
            else
                Markup("[grey]Tab でイコライザにフォーカス[/]")

        grid.AddRow(meters :> IRenderable) |> ignore
        grid.AddRow(hint :> IRenderable) |> ignore

        let headerText = if focused then " EQUALIZER  ● " else " EQUALIZER "

        Panel(grid)
            .Header(headerText)
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(if focused then "gold1" else "grey39"))
            .Padding(1, 0, 1, 0)

    let private tracks (player: AudioPlayer) =
        let focused = player.Focus = TrackList
        let table = Table().HideHeaders().NoBorder()
        table.AddColumn("cur") |> ignore
        table.AddColumn("num") |> ignore
        table.AddColumn("title") |> ignore
        table.AddColumn("dur") |> ignore

        let height = max 5 (min 12 (AnsiConsole.Profile.Height / 4))
        let start =
            if player.SelectedIndex < height then
                0
            else
                player.SelectedIndex - height + 1

        let finish = min player.Disc.Tracks.Length (start + height)

        for i = start to finish - 1 do
            let t = player.Disc.Tracks[i]
            let isCurrent = i = player.CurrentIndex && player.State <> Stopped
            let isSel = i = player.SelectedIndex
            let cursor =
                if isSel && focused then "[gold1]▶[/]"
                elif isCurrent then "[springgreen1]●[/]"
                else " "
            let numStyle = if isCurrent then "bold springgreen1" else "grey"
            let titleStyle =
                if isSel && focused then "bold underline wheat1"
                elif isCurrent then "wheat1"
                else "grey78"
            let numText = sprintf "%02d" t.Number

            table.AddRow(
                Markup(cursor),
                Markup($"[{numStyle}]{numText}[/]"),
                Markup($"[{titleStyle}]{esc t.Title}[/]"),
                Markup($"[grey]{TimeFmt.mmss t.Duration}[/]"))
            |> ignore

        let headerText = if focused then " TRACKS  ● " else " TRACKS "

        Panel(table)
            .Header(headerText)
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(if focused then "gold1" else "grey39"))
            .Padding(1, 0, 1, 0)

    let private footer (player: AudioPlayer) =
        let keys =
            " [grey]Space[/] 再生/停止  [grey]S[/] ストップ  [grey]X[/] 抽出  [grey]W[/] 共有窓  [grey]N/P[/] 次/前  [grey]←→[/] シーク  [grey],[/] [grey].[/] 音量  [grey]Tab[/] 切替  [grey]R[/] リピート  [grey]H[/] シャッフル  [grey]D[/] CD  [grey]O[/] フォルダ  [grey]M[/] デモ  [grey]E[/] 取り出し  [grey]?[/] ヘルプ  [grey]Q[/] 終了 "

        let status = $"[italic grey70]{esc player.Status}[/]"
        Markup($"{status}\n{keys}")

    let private helpPanel () =
        let text =
            """[bold gold1]操作[/]
  [wheat1]Space[/]  再生 / 一時停止          [wheat1]S[/]      停止
  [wheat1]Enter[/]  選択トラックを再生        [wheat1]N / P[/]  次 / 前のトラック
  [wheat1]← / →[/]  5秒シーク                [wheat1],[/] / [wheat1].[/] 音量
  [wheat1]Tab[/]    トラック ↔ イコライザ     [wheat1]↑ / ↓[/] 選択 / EQ ゲイン
  [wheat1]+ / -[/]  EQ ゲイン                 [wheat1]F[/]      EQ フラット
  [wheat1]T[/]      EQ バイパス               [wheat1]0[/]      EQ リセット
  [wheat1]R[/]      リピート OFF/ALL/ONE      [wheat1]H[/]      シャッフル
  [wheat1]D[/]      CD 検出                   [wheat1]O[/]      フォルダを開く
  [wheat1]M[/]      デモディスク              [wheat1]E[/]      CD 取り出し
  [wheat1]X[/]      CD を WAV 抽出            [wheat1]W[/]      Discord 共有窓
  [wheat1]Q[/]      終了

[bold gold1]音源[/]
  光学ドライブの CD-DA をデジタル読み取りし、10バンド EQ をかけて再生します。
  生読み取りができない場合は MCI にフォールバックします（その場合 EQ は無効）。
  [wheat1]X[/] で選択トラックまたは全トラックを 44.1kHz / 16bit / ステレオ WAV に書き出します。
  フォルダ内の WAV / MP3 / M4A などもディスクとして扱えます。
  ディスクが無いときはデモ信号で EQ を試せます。

[bold gold1]Discord[/]
  コマンドプロンプトの窓は Discord から見ると別プロセス（無音）です。
  起動時に開く [wheat1]SOUND CD Player[/] 窓を共有すると、映像と音の両方が乗ります。
  [wheat1]W[/] で共有窓の表示を切り替えます。"""

        Panel(Markup(text))
            .Header(" HELP ")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("khaki1"))

    let render (player: AudioPlayer) : IRenderable =
        let children = [
            yield header player :> IRenderable

            match ripPanel player with
            | Some panel -> yield panel
            | None -> ()

            yield spectrum player :> IRenderable
            yield equalizer player :> IRenderable
            yield (if player.ShowHelp then helpPanel () :> IRenderable else tracks player)
            yield footer player :> IRenderable
        ]

        Rows(Array.ofList children) :> IRenderable

    let plain (player: AudioPlayer) =
        let disc = player.Disc
        let track = player.CurrentTrack
        let title = track |> Option.map (fun t -> t.Title) |> Option.defaultValue "—"
        let number = track |> Option.map (fun t -> t.Number) |> Option.defaultValue 0
        let pos = TimeFmt.mmss player.Position
        let dur = TimeFmt.mmss player.Duration
        let bar = progressBar player.Position player.Duration 36
        let bars, _ = player.Analyzer.Snapshot()
        let spec =
            bars
            |> Array.truncate 56
            |> Array.map barChar
            |> String.concat ""

        let eq =
            player.Bands
            |> Array.map (fun b ->
                let g = int (Math.Round(float b.GainDb))
                let sign = if g > 0 then "+" else ""
                sprintf "%s%s%d" b.Label sign g)
            |> String.concat "  "

        let tracks =
            disc.Tracks
            |> Array.truncate 8
            |> Array.map (fun t ->
                let mark =
                    if t.Index = player.CurrentIndex && player.State <> Stopped then ">"
                    else " "

                sprintf "%s %02d  %s  %s" mark t.Number t.Title (TimeFmt.mmss t.Duration))
            |> String.concat "\n"

        $"""◆ SOUND  TUI CD PLAYER
{player.State.Label}   {disc.Kind.Label}   {disc.Title}
{sprintf "%02d" number} / {sprintf "%02d" disc.Tracks.Length}   {title}
{pos}  {bar}  {dur}
VOL {string (int (player.Volume * 100.f)) + "%"}   REPEAT {player.Repeat.Label}   EQ {if player.EqEnabled then "ON" else "OFF"}

SPECTRUM
{spec}

EQ  {eq}

TRACKS
{tracks}"""
