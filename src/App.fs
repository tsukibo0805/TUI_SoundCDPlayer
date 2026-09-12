namespace Sound

open System
open Spectre.Console

module Input =
    let handle (player: AudioPlayer) (key: ConsoleKeyInfo) =
        let alt = key.Modifiers &&& ConsoleModifiers.Alt = ConsoleModifiers.Alt

        if player.IsRipping then
            match key.Key with
            | ConsoleKey.S
            | ConsoleKey.Escape -> player.CancelRip()
            | ConsoleKey.Q ->
                player.CancelRip()
                player.RequestQuit()
            | _ -> ()
        else
            match key.Key, key.KeyChar, player.Focus with
            | ConsoleKey.Q, _, _ -> player.RequestQuit()
            | ConsoleKey.X, _, _ -> player.RequestRip()
            | ConsoleKey.Spacebar, _, _ -> player.TogglePlay()
            | ConsoleKey.S, _, _ -> player.Stop()
            | ConsoleKey.Enter, _, _ -> player.PlaySelected()
            | ConsoleKey.N, _, _ -> player.Next()
            | ConsoleKey.P, _, _ -> player.Previous()
            | ConsoleKey.Tab, _, _ -> player.ToggleFocus()
            | ConsoleKey.R, _, _ -> player.CycleRepeat()
            | ConsoleKey.H, _, _ -> player.ToggleShuffle()
            | ConsoleKey.D, _, _ -> player.DetectCd()
            | ConsoleKey.O, _, _ -> player.RequestOpenFolder()
            | ConsoleKey.M, _, _ -> player.LoadDemo()
            | ConsoleKey.E, _, _ -> player.Eject()
            | ConsoleKey.T, _, _ -> player.ToggleEq()
            | ConsoleKey.F, _, _
            | ConsoleKey.D0, _, _ -> player.ResetEq()
            | ConsoleKey.Oem2, _, _ -> player.ToggleHelp()
            | _, '?', _ -> player.ToggleHelp()
            | ConsoleKey.LeftArrow, _, Equalizer -> player.MoveEq -1
            | ConsoleKey.RightArrow, _, Equalizer -> player.MoveEq 1
            | ConsoleKey.UpArrow, _, Equalizer -> player.AdjustEq 1.f
            | ConsoleKey.DownArrow, _, Equalizer -> player.AdjustEq -1.f
            | ConsoleKey.LeftArrow, _, TrackList -> player.Seek(TimeSpan.FromSeconds -5.0)
            | ConsoleKey.RightArrow, _, TrackList -> player.Seek(TimeSpan.FromSeconds 5.0)
            | ConsoleKey.UpArrow, _, TrackList -> player.MoveSelection -1
            | ConsoleKey.DownArrow, _, TrackList -> player.MoveSelection 1
            | _, ('+' | '='), _ -> player.AdjustEq 1.f
            | _, ('-' | '_'), _ -> player.AdjustEq -1.f
            | _, (',' | '['), _ -> player.AdjustVolume -0.05f
            | _, ('.' | ']'), _ -> player.AdjustVolume 0.05f
            | ConsoleKey.Escape, _, _ when player.ShowHelp -> player.ToggleHelp()
            | _ when alt -> ()
            | _ -> ()

module App =
    let private promptFolder (player: AudioPlayer) =
        AnsiConsole.WriteLine()

        let path =
            AnsiConsole.Prompt(
                TextPrompt<string>("音声フォルダのパス:")
                    .AllowEmpty())

        if not (String.IsNullOrWhiteSpace path) then
            player.LoadFolder(path.Trim('"'))

    let private promptRip (player: AudioPlayer) =
        AnsiConsole.WriteLine()
        let suggested = Ripper.defaultDirectory player.Disc.Title

        let dest =
            AnsiConsole.Prompt(
                TextPrompt<string>($"抽出先フォルダ [grey]({Markup.Escape suggested})[/]:")
                    .AllowEmpty())

        let dir =
            if String.IsNullOrWhiteSpace dest then
                suggested
            else
                dest.Trim('"')

        let scope =
            AnsiConsole.Prompt(
                SelectionPrompt<string>()
                    .Title("何を抽出しますか？")
                    .AddChoices("選択中のトラック", "すべてのトラック"))

        let allTracks = scope = "すべてのトラック"
        player.StartRip(dir, allTracks)

    let rec private live (player: AudioPlayer) =
        AnsiConsole.Clear()

        AnsiConsole
            .Live(View.render player)
            .AutoClear(true)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(fun ctx ->
                let mutable exit = false

                while player.Running && not exit do
                    while Console.KeyAvailable do
                        Input.handle player (Console.ReadKey true)

                    match player.Pending with
                    | OpenFolder
                    | RipPrompt
                    | Quit -> exit <- true
                    | Idle ->
                        player.Tick()
                        ctx.UpdateTarget(View.render player)
                        ctx.Refresh()
                        System.Threading.Thread.Sleep 50)

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

    let private trySetConsole () =
        try
            Console.OutputEncoding <- Text.Encoding.UTF8
            Console.InputEncoding <- Text.Encoding.UTF8
        with _ ->
            ()

        try
            Console.Title <- "SOUND — TUI CD Player"
        with _ ->
            ()

        try
            Console.CursorVisible <- false
        with _ ->
            ()

    let renderSnapshot () =
        trySetConsole ()
        use player = new AudioPlayer()
        player.LoadDemo()
        AnsiConsole.Write(View.render player)

    let run (args: string array) =
        trySetConsole ()

        try
            use player = new AudioPlayer()

            let rec consume i =
                if i >= args.Length then
                    ()
                else
                    match args[i] with
                    | "--demo"
                    | "-d" ->
                        player.LoadDemo()
                        consume (i + 1)
                    | "--cd"
                    | "-c" ->
                        player.DetectCd()
                        consume (i + 1)
                    | "--help"
                    | "-h" ->
                        player.ToggleHelp()
                        consume (i + 1)
                    | path when path.StartsWith("-") -> consume (i + 1)
                    | path ->
                        if IO.Directory.Exists path then
                            player.LoadFolder path
                        elif IO.File.Exists path then
                            match IO.Path.GetDirectoryName path with
                            | null | "" -> ()
                            | dir -> player.LoadFolder dir
                        else
                            ()

                        consume (i + 1)

            consume 0

            if args.Length = 0 then
                match CdDrive.tryLoadFirst () with
                | Ok disc -> player.LoadDisc(disc, $"{disc.Kind.Label}: {disc.Tracks.Length} トラック")
                | Error _ -> ()

            AnsiConsole.MarkupLine("[grey]SOUND CD Player を起動しています…[/]")
            live player
            AnsiConsole.MarkupLine("[grey]停止しました。[/]")
        finally
            try
                Console.CursorVisible <- true
            with _ ->
                ()
