namespace Sound

open System
open System.Drawing
open System.Drawing.Imaging
open System.Runtime.InteropServices
open System.Text
open System.Threading
open System.Windows.Forms

module DiscordShare =
    let private pwRenderFullContent = 2u
    let private gaRoot = 2u
    let private srcCopy = 0x00CC0020

    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type RECT =
        val mutable Left: int
        val mutable Top: int
        val mutable Right: int
        val mutable Bottom: int

    type EnumWindowsProc = delegate of nativeint * nativeint -> bool

    [<DllImport("kernel32.dll")>]
    extern nativeint GetConsoleWindow()

    [<DllImport("user32.dll")>]
    extern bool GetClientRect(nativeint hWnd, RECT& lpRect)

    [<DllImport("user32.dll")>]
    extern bool GetWindowRect(nativeint hWnd, RECT& lpRect)

    [<DllImport("user32.dll")>]
    extern bool PrintWindow(nativeint hwnd, nativeint hdcBlt, uint32 nFlags)

    [<DllImport("user32.dll")>]
    extern nativeint GetDC(nativeint hWnd)

    [<DllImport("user32.dll")>]
    extern int ReleaseDC(nativeint hWnd, nativeint hDC)

    [<DllImport("gdi32.dll")>]
    extern bool BitBlt(nativeint hdcDest, int x, int y, int cx, int cy, nativeint hdcSrc, int x1, int y1, int rop)

    [<DllImport("user32.dll")>]
    extern nativeint GetAncestor(nativeint hwnd, uint32 gaFlags)

    [<DllImport("user32.dll")>]
    extern bool IsWindowVisible(nativeint hWnd)

    [<DllImport("user32.dll", CharSet = CharSet.Unicode)>]
    extern int GetClassNameW(nativeint hWnd, StringBuilder lpClassName, int nMaxCount)

    [<DllImport("user32.dll")>]
    extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nativeint lParam)

    [<DllImport("user32.dll")>]
    extern uint32 GetWindowThreadProcessId(nativeint hWnd, uint32& lpdwProcessId)

    [<DllImport("shell32.dll", CharSet = CharSet.Unicode)>]
    extern int SetCurrentProcessExplicitAppUserModelID(string appID)

    let mutable private form: Form = null
    let mutable private shuttingDown = false
    let mutable private started = false
    let mutable private renderer: (unit -> string) = fun () -> ""
    let private renderLock = obj ()

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

    let private clientSize hwnd =
        let mutable rect = RECT()
        if GetClientRect(hwnd, &rect) then
            rect.Right - rect.Left, rect.Bottom - rect.Top
        else
            0, 0

    let private isTerminalClass name =
        name = "ConsoleWindowClass"
        || name.StartsWith("CASCADIA", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Console", StringComparison.OrdinalIgnoreCase)

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

    let private findTerminalHwnd () =
        let cons = GetConsoleWindow()
        let root = if cons = 0n then 0n else GetAncestor(cons, gaRoot)

        if isUsable root then
            root
        elif isUsable cons then
            cons
        else
            let mutable consolePid = 0u

            if cons <> 0n then
                GetWindowThreadProcessId(cons, &consolePid) |> ignore

            let mutable found = 0n
            let proc = EnumWindowsProc(fun hwnd _ ->
                if found = 0n && isUsable hwnd then
                    let cls = className hwnd
                    let mutable pid = 0u
                    GetWindowThreadProcessId(hwnd, &pid) |> ignore

                    if isTerminalClass cls || (consolePid <> 0u && pid = consolePid) then
                        found <- hwnd

                found = 0n)

            EnumWindows(proc, 0n) |> ignore
            GC.KeepAlive(proc)
            found

    let private isMostlyBlack (bmp: Bitmap) =
        let w = bmp.Width
        let h = bmp.Height
        let mutable lit = 0
        let mutable n = 0
        let stepX = max 1 (w / 12)
        let stepY = max 1 (h / 8)

        for y in 2 .. stepY .. h - 3 do
            for x in 2 .. stepX .. w - 3 do
                let c = bmp.GetPixel(x, y)
                n <- n + 1
                if int c.R + int c.G + int c.B > 36 then
                    lit <- lit + 1

        n > 0 && lit * 20 < n

    let private captureWindow hwnd =
        let w, h = clientSize hwnd

        if w < 8 || h < 8 then
            None
        else
            let bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb)
            use g = Graphics.FromImage(bmp)
            let hdc = g.GetHdc()
            let printed = PrintWindow(hwnd, hdc, pwRenderFullContent)

            if not printed then
                let srcDc = GetDC(hwnd)
                if srcDc <> 0n then
                    BitBlt(hdc, 0, 0, w, h, srcDc, 0, 0, srcCopy) |> ignore
                    ReleaseDC(hwnd, srcDc) |> ignore

            g.ReleaseHdc(hdc)

            if printed && not (isMostlyBlack bmp) then
                Some bmp
            else
                let mutable wr = RECT()

                if GetWindowRect(hwnd, &wr) then
                    let sw = wr.Right - wr.Left
                    let sh = wr.Bottom - wr.Top

                    if sw > 8 && sh > 8 then
                        try
                            use screen = new Bitmap(sw, sh, PixelFormat.Format32bppArgb)
                            use sg = Graphics.FromImage(screen)
                            sg.CopyFromScreen(wr.Left, wr.Top, 0, 0, Size(sw, sh))

                            if isMostlyBlack screen then
                                bmp.Dispose()
                                None
                            else
                                bmp.Dispose()
                                Some(new Bitmap(screen))
                        with _ ->
                            if isMostlyBlack bmp then
                                bmp.Dispose()
                                None
                            else
                                Some bmp
                    elif isMostlyBlack bmp then
                        bmp.Dispose()
                        None
                    else
                        Some bmp
                elif isMostlyBlack bmp then
                    bmp.Dispose()
                    None
                else
                    Some bmp

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

        g.DrawString("Discord ではこのウィンドウを共有してください", font, muted, 18.0f, float32 (h - 28))
        bmp

    let private nextFrame (targetSize: Size) =
        let hwnd = findTerminalHwnd ()

        if hwnd <> 0n then
            match captureWindow hwnd with
            | Some bmp -> bmp
            | None -> drawFallback targetSize.Width targetSize.Height
        else
            drawFallback targetSize.Width targetSize.Height

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
                Text = "TUI をこの窓に映しています。Discord ではこの『SOUND CD Player』を共有してください。",
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
                let bmp = nextFrame picture.ClientSize
                let old = picture.Image
                picture.Image <- bmp

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
            thread.Name <- "SOUND Discord Share"
            thread.Start()

    let toggle () =
        let f = form

        if isNull f || f.IsDisposed || not f.IsHandleCreated then
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
