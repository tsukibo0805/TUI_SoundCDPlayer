namespace Sound

open System
open System.Drawing
open System.Drawing.Imaging
open System.Runtime.InteropServices
open System.Text
open System.Threading
open System.Windows.Forms

module TuiMirror =
    let private pwRenderFullContent = 2u
    let private gaRoot = 2u

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RECT =
        val mutable Left: int
        val mutable Top: int
        val mutable Right: int
        val mutable Bottom: int

    [<DllImport("kernel32.dll")>]
    extern nativeint GetConsoleWindow()

    [<DllImport("user32.dll")>]
    extern nativeint GetForegroundWindow()

    [<DllImport("user32.dll")>]
    extern bool GetClientRect(nativeint hWnd, RECT& lpRect)

    [<DllImport("user32.dll")>]
    extern bool PrintWindow(nativeint hwnd, nativeint hdcBlt, uint32 nFlags)

    [<DllImport("user32.dll")>]
    extern nativeint GetAncestor(nativeint hwnd, uint32 gaFlags)

    [<DllImport("user32.dll")>]
    extern bool IsWindowVisible(nativeint hWnd)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern int GetClassNameW(nativeint hWnd, StringBuilder lpClassName, int nMaxCount)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern int GetWindowTextW(nativeint hWnd, StringBuilder lpString, int nMaxCount)

    [<DllImport("user32.dll")>]
    extern uint32 GetWindowThreadProcessId(nativeint hWnd, uint32& lpdwProcessId)

    [<DllImport("shell32.dll", CharSet = CharSet.Unicode)>]
    extern int SetCurrentProcessExplicitAppUserModelID(string appID)

    let mutable private form: Form = null
    let mutable private shuttingDown = false
    let mutable private started = false
    let mutable private renderer: (unit -> string) = fun () -> ""
    let private renderLock = obj ()
    let private targetLock = obj ()
    let mutable private targetHwnd = 0n
    let mutable private targetPid = 0u
    let mutable private targetTitle = ""

    let setRenderer (fn: unit -> string) =
        lock renderLock (fun () -> renderer <- fn)

    let private snapshotText () =
        try
            lock renderLock (fun () -> renderer ())
        with _ ->
            ""

    let private className hwnd =
        let sb = StringBuilder(256)
        if GetClassNameW(hwnd, sb, sb.Capacity) > 0 then sb.ToString() else ""

    let private windowTitle hwnd =
        let sb = StringBuilder(512)
        if GetWindowTextW(hwnd, sb, sb.Capacity) > 0 then sb.ToString() else ""

    let private clientSize hwnd =
        let mutable rect = RECT()
        if GetClientRect(hwnd, &rect) then
            rect.Right - rect.Left, rect.Bottom - rect.Top
        else
            0, 0

    let private isTerminalClass name =
        name = "ConsoleWindowClass"
        || name.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase)

    let private ownHandle () =
        if isNull form || form.IsDisposed || not form.IsHandleCreated then
            0n
        else
            form.Handle

    let private isUsable hwnd =
        let own = ownHandle ()
        if hwnd = 0n || hwnd = own || not (IsWindowVisible hwnd) then
            false
        else
            let w, h = clientSize hwnd
            w >= 160 && h >= 80

    let private isTerminalWindow hwnd =
        isUsable hwnd && isTerminalClass (className hwnd)

    let private windowPid hwnd =
        let mutable pid = 0u
        GetWindowThreadProcessId(hwnd, &pid) |> ignore
        pid

    let private selectTerminalHwnd () =
        let cons = GetConsoleWindow()

        // A visible classic console belongs to this process. In Windows Terminal,
        // GetConsoleWindow points to a hidden pseudoconsole, so use the window
        // receiving the W key instead of enumerating unrelated terminal windows.
        if isTerminalWindow cons then
            cons
        else
            let root = if cons = 0n then 0n else GetAncestor(cons, gaRoot)

            if isTerminalWindow root then
                root
            else
                let foreground = GetForegroundWindow()
                let foregroundRoot =
                    if foreground = 0n then 0n else GetAncestor(foreground, gaRoot)

                if isTerminalWindow foregroundRoot then foregroundRoot
                elif isTerminalWindow foreground then foreground
                else 0n

    let private pinTerminalWindow () =
        let hwnd = selectTerminalHwnd ()
        let pid = if hwnd = 0n then 0u else windowPid hwnd
        let title = if hwnd = 0n then "" else windowTitle hwnd
        lock targetLock (fun () ->
            targetHwnd <- hwnd
            targetPid <- pid
            targetTitle <- title)

    let private pinnedTerminalWindow () =
        let hwnd, pid, title = lock targetLock (fun () -> targetHwnd, targetPid, targetTitle)
        if pid <> 0u
           && isTerminalWindow hwnd
           && windowPid hwnd = pid
           && (title = "" || windowTitle hwnd = title) then
            hwnd
        else
            0n

    let private litPixelsUntil64 (bmp: Bitmap) =
        let bounds = Rectangle(0, 0, bmp.Width, bmp.Height)
        let data = bmp.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)

        try
            let row = Array.zeroCreate<byte> (bmp.Width * 4)
            let mutable lit = 0
            let mutable y = 0

            // A fixed number of lit pixels is enough. A percentage of the whole
            // window falsely rejects a dark terminal when it is maximized.
            while y < bmp.Height && lit < 64 do
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length)
                let mutable x = 0

                while x < bmp.Width && lit < 64 do
                    let i = x * 4
                    if int row[i] + int row[i + 1] + int row[i + 2] > 60 then
                        lit <- lit + 1
                    x <- x + 2

                y <- y + 2

            lit
        finally
            bmp.UnlockBits(data)

    let private captureWindow hwnd =
        if hwnd = ownHandle () then
            Error "ミラー窓自身が撮影対象になっています"
        else
            let w, h = clientSize hwnd

            if w < 8 || h < 8 then
                Error "撮影対象の窓が小さすぎます"
            else
                let bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)
                try
                    let printed =
                        use g = Graphics.FromImage(bmp)
                        let hdc = g.GetHdc()
                        try
                            PrintWindow(hwnd, hdc, pwRenderFullContent)
                        finally
                            g.ReleaseHdc(hdc)

                    if not printed then
                        bmp.Dispose()
                        Error "PrintWindow が失敗しました"
                    else
                        let lit = litPixelsUntil64 bmp
                        if lit < 64 then
                            bmp.Dispose()
                            Error $"撮影画像がほぼ黒です (明るい画素 {lit} 個、必要 64 個、{w}x{h})"
                        else
                            Ok bmp
                with _ ->
                    bmp.Dispose()
                    Error "撮影処理で例外が発生しました"

    let private drawFallback (width: int) (height: int) =
        let w = max 640 width
        let h = max 400 height
        let bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)
        use g = Graphics.FromImage(bmp)
        g.Clear(Color.FromArgb(18, 17, 14))
        g.TextRenderingHint <- Text.TextRenderingHint.ClearTypeGridFit

        use font = new Font("Consolas", 13.0f, FontStyle.Regular, GraphicsUnit.Pixel)
        use gold = new SolidBrush(Color.FromArgb(224, 176, 68))
        use cream = new SolidBrush(Color.FromArgb(243, 230, 196))
        use muted = new SolidBrush(Color.FromArgb(154, 141, 114))

        let text = snapshotText ()
        let lines =
            if String.IsNullOrWhiteSpace text then
                [| "TUI を準備しています…" |]
            else
                text.Split('\n')

        let mutable y = 18
        g.DrawString("SOUND  ·  TUI ミラー", font, gold, 18.0f, float32 y)
        y <- y + 28

        for line in lines do
            if y > h - 24 then
                ()
            else
                g.DrawString(line, font, cream, 18.0f, float32 y)
                y <- y + 20

        g.DrawString("TUI の状態を簡易表示しています", font, muted, 18.0f, float32 (h - 28))
        bmp

    let private nextFrame (targetSize: Size) =
        let hwnd = pinnedTerminalWindow ()

        if hwnd = 0n then
            drawFallback targetSize.Width targetSize.Height, Some "撮影元のターミナルが見つからないか、表示先が切り替わりました"
        else
            match captureWindow hwnd with
            | Ok bmp -> bmp, None
            | Error reason -> drawFallback targetSize.Width targetSize.Height, Some reason

    let private runUi () =
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2) |> ignore
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(false)

        let f = new Form()
        f.Text <- "SOUND CD Player"
        f.StartPosition <- FormStartPosition.WindowsDefaultLocation
        f.MinimumSize <- Size(720, 440)
        f.Size <- Size(1040, 680)
        f.BackColor <- Color.FromArgb(18, 17, 14)
        f.ForeColor <- Color.FromArgb(243, 230, 196)

        let hint =
            new Label(
                Dock = DockStyle.Top,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = Padding(12, 0, 12, 0),
                Text = "TUI をミラー表示しています。",
                BackColor = Color.FromArgb(36, 32, 22),
                ForeColor = Color.FromArgb(224, 176, 68)
            )

        let picture =
            new PictureBox(
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(18, 17, 14)
            )

        f.Controls.Add(picture)
        f.Controls.Add(hint)

        let timer = new Windows.Forms.Timer()
        timer.Interval <- 50

        timer.Tick.Add(fun _ ->
            try
                let bmp, failure = nextFrame picture.ClientSize
                let old = picture.Image
                picture.Image <- bmp
                hint.Text <-
                    match failure with
                    | Some reason -> $"ミラー不可: {reason}"
                    | None -> "TUI をミラー表示しています。"

                if not (isNull old) then
                    old.Dispose()
            with _ ->
                ())

        f.FormClosing.Add(fun e ->
            if not shuttingDown then
                e.Cancel <- true
                f.Hide())

        form <- f
        timer.Start()
        Application.Run(f)
        timer.Stop()
        timer.Dispose()

    let start () =
        if not started then
            started <- true
            shuttingDown <- false
            SetCurrentProcessExplicitAppUserModelID("tsukibo0805.SOUND.CdPlayer") |> ignore

            let thread = Thread(ThreadStart(runUi))
            thread.IsBackground <- true
            thread.SetApartmentState(ApartmentState.STA)
            thread.Name <- "SOUND TUI Mirror"
            thread.Start()

    let toggle () =
        pinTerminalWindow ()

        if not started then
            start ()
        else
            let f = form

            if isNull f || f.IsDisposed then
                started <- false
                start ()
            elif not f.IsHandleCreated then
                ()
            else
                try
                    f.BeginInvoke(
                        Action(fun () ->
                            if f.Visible then
                                f.Hide()
                            else
                                f.Show()
                                f.Activate())
                    )
                    |> ignore
                with _ ->
                    ()

    let stop () =
        shuttingDown <- true
        let f = form

        if not (isNull f) && not f.IsDisposed && f.IsHandleCreated then
            try
                f.BeginInvoke(Action(fun () -> f.Close())) |> ignore
            with _ ->
                ()
