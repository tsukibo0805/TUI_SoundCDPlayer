namespace Sound

open System
open System.IO
open System.Runtime.InteropServices
open System.Text
open NAudio.Wave

module Win32 =
    let genericRead = 0x80000000u
    let fileShareRead = 0x00000001u
    let fileShareWrite = 0x00000002u
    let openExisting = 3u
    let ioctlCdromReadToc = 0x00024000u
    let ioctlCdromRawRead = 0x0002403Eu
    let ioctlStorageEject = 0x002D4808u
    let cddaMode = 2
    let bytesPerRawSector = 2352
    let cookedSector = 2048L

    [<DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)>]
    extern nativeint CreateFileW(
        string lpFileName,
        uint32 dwDesiredAccess,
        uint32 dwShareMode,
        nativeint lpSecurityAttributes,
        uint32 dwCreationDisposition,
        uint32 dwFlagsAndAttributes,
        nativeint hTemplateFile)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool CloseHandle(nativeint hObject)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool DeviceIoControl(
        nativeint hDevice,
        uint32 dwIoControlCode,
        nativeint lpInBuffer,
        uint32 nInBufferSize,
        nativeint lpOutBuffer,
        uint32 nOutBufferSize,
        uint32& lpBytesReturned,
        nativeint lpOverlapped)

    [<DllImport("winmm.dll", CharSet = CharSet.Unicode)>]
    extern int32 mciSendStringW(string command, StringBuilder buffer, int bufferSize, nativeint hwndCallback)

[<Struct; StructLayout(LayoutKind.Sequential)>]
type RawReadInfo =
    val mutable DiskOffset: int64
    val mutable SectorCount: uint32
    val mutable TrackMode: int32

module Mci =
    let aliasName = "soundcd"

    let send (command: string) =
        let buf = StringBuilder(256)
        let code = Win32.mciSendStringW(command, buf, buf.Capacity, 0n)

        if code <> 0 then
            Error $"MCI {code}: {command}"
        else
            Ok(buf.ToString())

    let close () = send $"close {aliasName}" |> ignore

    let openDrive (drive: string) =
        close ()
        send $"open {drive} type cdaudio alias {aliasName} wait"
        |> Result.bind (fun _ -> send $"set {aliasName} time format tmsf wait")

    let status (query: string) = send $"status {aliasName} {query}"

    let parseMmssff (text: string) =
        let parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries)

        match parts with
        | [| m; s; f |] ->
            let mutable mv, sv, fv = 0, 0, 0

            if Int32.TryParse(m, &mv) && Int32.TryParse(s, &sv) && Int32.TryParse(f, &fv) then
                Some(TimeFmt.fromSectors (mv * 60 * 75 + sv * 75 + fv))
            else
                None
        | [| t; m; s; f |] ->
            let mutable mv, sv, fv = 0, 0, 0

            if Int32.TryParse(m, &mv) && Int32.TryParse(s, &sv) && Int32.TryParse(f, &fv) then
                Some(TimeFmt.fromSectors (mv * 60 * 75 + sv * 75 + fv))
            else
                None
        | _ -> None

    let play (track: int) (nextExclusive: int option) =
        match nextExclusive with
        | Some n -> send $"play {aliasName} from {track} to {n}"
        | None -> send $"play {aliasName} from {track}"

    let pause () = send $"pause {aliasName}"
    let resume () = send $"resume {aliasName}"
    let stop () = send $"stop {aliasName}" |> ignore

    let position () =
        match status "position" with
        | Ok text ->
            let parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries)

            match parts with
            | [| t; m; s; f |] ->
                let mutable tv, mv, sv, fv = 0, 0, 0, 0

                if
                    Int32.TryParse(t, &tv)
                    && Int32.TryParse(m, &mv)
                    && Int32.TryParse(s, &sv)
                    && Int32.TryParse(f, &fv)
                then
                    Some(tv, TimeFmt.fromSectors (mv * 60 * 75 + sv * 75 + fv))
                else
                    None
            | _ -> None
        | Error _ -> None

    let mode () =
        match status "mode" with
        | Ok text -> text.Trim().ToLowerInvariant()
        | Error _ -> ""

module CdDrive =
    let private invalidHandle = nativeint -1

    let listDrives () =
        DriveInfo.GetDrives()
        |> Array.choose (fun d ->
            if d.DriveType = DriveType.CDRom then
                Some(d.Name.Substring(0, 1).ToUpperInvariant())
            else
                None)

    let closeHandle (handle: nativeint) =
        if handle <> 0n && handle <> invalidHandle then
            Win32.CloseHandle(handle) |> ignore

    let private ioctl (handle: nativeint) code (inBuf: byte array) (outBuf: byte array) =
        let inPin =
            if inBuf.Length = 0 then
                None
            else
                Some(GCHandle.Alloc(inBuf, GCHandleType.Pinned))

        let outPin = GCHandle.Alloc(outBuf, GCHandleType.Pinned)

        try
            let mutable returned = 0u

            let inPtr, inLen =
                match inPin with
                | Some pin -> pin.AddrOfPinnedObject(), uint32 inBuf.Length
                | None -> 0n, 0u

            let ok =
                Win32.DeviceIoControl(
                    handle,
                    code,
                    inPtr,
                    inLen,
                    outPin.AddrOfPinnedObject(),
                    uint32 outBuf.Length,
                    &returned,
                    0n)

            if ok then
                Ok(int returned)
            else
                Error(Marshal.GetLastWin32Error())
        finally
            outPin.Free()

            match inPin with
            | Some pin -> pin.Free()
            | None -> ()

    let private ioctlStruct (handle: nativeint) code (info: RawReadInfo) (outBuf: byte array) =
        let inArr = [| info |]
        let inPin = GCHandle.Alloc(inArr, GCHandleType.Pinned)
        let outPin = GCHandle.Alloc(outBuf, GCHandleType.Pinned)

        try
            let mutable returned = 0u

            let ok =
                Win32.DeviceIoControl(
                    handle,
                    code,
                    inPin.AddrOfPinnedObject(),
                    uint32 sizeof<RawReadInfo>,
                    outPin.AddrOfPinnedObject(),
                    uint32 outBuf.Length,
                    &returned,
                    0n)

            if ok then
                Ok(int returned)
            else
                Error(Marshal.GetLastWin32Error())
        finally
            inPin.Free()
            outPin.Free()

    let openHandle (letter: string) =
        let path = $@"\\.\{letter}:"

        let handle =
            Win32.CreateFileW(
                path,
                Win32.genericRead,
                Win32.fileShareRead ||| Win32.fileShareWrite,
                0n,
                Win32.openExisting,
                0u,
                0n)

        if handle = invalidHandle then
            Error $"ドライブ {letter}: を開けません (Win32 {Marshal.GetLastWin32Error()})"
        else
            Ok handle

    let readRaw (handle: nativeint) (lba: int) (sectors: int) (dest: byte array) (destOffset: int) =
        let chunk = Array.zeroCreate (sectors * Win32.bytesPerRawSector)
        let mutable info = RawReadInfo()
        info.DiskOffset <- int64 lba * Win32.cookedSector
        info.SectorCount <- uint32 sectors
        info.TrackMode <- Win32.cddaMode

        match ioctlStruct handle Win32.ioctlCdromRawRead info chunk with
        | Error code -> Error code
        | Ok _ ->
            Buffer.BlockCopy(chunk, 0, dest, destOffset, chunk.Length)
            Ok()

    let private parseToc (letter: string) (buffer: byte array) =
        let first = int buffer[2]
        let last = int buffer[3]

        let entries =
            [
                for i = 0 to 99 do
                    let o = 4 + i * 8
                    let track = int buffer[o + 2]
                    let control = int (buffer[o + 1] &&& 0x0Fuy)
                    let m = int buffer[o + 5]
                    let s = int buffer[o + 6]
                    let f = int buffer[o + 7]
                    let lba = CdMath.msfToLba m s f
                    yield track, control, lba
            ]

        let byNumber =
            entries
            |> List.map (fun (t, c, l) -> t, (c, l))
            |> Map.ofList

        let leadOut = entries |> List.tryFind (fun (t, _, _) -> t = 0xAA)

        match leadOut with
        | None -> Error "TOC にリードアウトがありません"
        | Some(_, _, leadLba) ->
            let tracks = ResizeArray<Track>()
            let mutable index = 0

            for n = first to last do
                match Map.tryFind n byNumber with
                | Some(control, startLba) ->
                    let isData = (control &&& 0x04) <> 0

                    if not isData then
                        let nextLba =
                            if n = last then
                                leadLba
                            else
                                match Map.tryFind (n + 1) byNumber with
                                | Some(_, lba) -> lba
                                | None -> leadLba

                        let sectors = max 0 (nextLba - startLba)

                        tracks.Add(
                            {
                                Index = index
                                Number = n
                                Title = sprintf "トラック %02d" n
                                Duration = TimeFmt.fromSectors sectors
                                Source = DigitalCd(0n, startLba, sectors)
                                Rip =
                                    Some {
                                        Drive = letter
                                        StartLba = startLba
                                        Sectors = sectors
                                    }
                            })

                        index <- index + 1
                | None -> ()

            if tracks.Count = 0 then
                Error "オーディオトラックが見つかりません"
            else
                Ok(tracks.ToArray())

    let private loadDigital (letter: string) =
        match openHandle letter with
        | Error e -> Error e
        | Ok handle ->
            let tocBuf = Array.zeroCreate 804

            match ioctl handle Win32.ioctlCdromReadToc [||] tocBuf with
            | Error code ->
                closeHandle handle
                Error $"TOC を読めません (Win32 {code})"
            | Ok _ ->
                match parseToc letter tocBuf with
                | Error e ->
                    closeHandle handle
                    Error e
                | Ok tracks ->
                    match readRaw handle (match tracks[0].Source with | DigitalCd(_, lba, _) -> lba | _ -> 0) 1 (Array.zeroCreate Win32.bytesPerRawSector) 0 with
                    | Error code ->
                        closeHandle handle
                        Error $"デジタル読み取り不可 (Win32 {code})"
                    | Ok() ->
                        let bound =
                            tracks
                            |> Array.map (fun t ->
                                match t.Source with
                                | DigitalCd(_, lba, sectors) -> { t with Source = DigitalCd(handle, lba, sectors) }
                                | other -> { t with Source = other })

                        Ok(
                            handle,
                            {
                                Title = $"Audio CD ({letter}:)"
                                Artist = "CD-DA"
                                Kind = DigitalAudioCd
                                Drive = Some letter
                                Tracks = bound
                            })

    let readTocTracks (letter: string) =
        match openHandle letter with
        | Error e -> Error e
        | Ok handle ->
            let tocBuf = Array.zeroCreate 804

            let result =
                match ioctl handle Win32.ioctlCdromReadToc [||] tocBuf with
                | Error code -> Error $"TOC を読めません (Win32 {code})"
                | Ok _ -> parseToc letter tocBuf

            closeHandle handle
            result

    let private loadMci (letter: string) =
        match Mci.openDrive $"{letter}:" with
        | Error e -> Error e
        | Ok _ ->
            match Mci.status "number of tracks" with
            | Error e -> Error e
            | Ok nText ->
                let mutable n = 0

                if not (Int32.TryParse(nText.Trim(), &n)) || n <= 0 then
                    Error "MCI: トラック数を取得できません"
                else
                    let tracks =
                        [|
                            for i in 1..n do
                                let dur =
                                    match Mci.status $"length track {i}" with
                                    | Ok text -> Mci.parseMmssff (text.Trim()) |> Option.defaultValue (TimeSpan.FromMinutes 3.0)
                                    | Error _ -> TimeSpan.FromMinutes 3.0

                                {
                                    Index = i - 1
                                    Number = i
                                    Title = sprintf "トラック %02d" i
                                    Duration = dur
                                    Source = MciCd(letter, i)
                                    Rip = None
                                }
                        |]

                    Ok
                        {
                            Title = $"Audio CD ({letter}:)"
                            Artist = "CD-DA / MCI"
                            Kind = MciAudioCd
                            Drive = Some letter
                            Tracks = tracks
                        }

    let private withRipFromToc (letter: string) (disc: Disc) =
        match readTocTracks letter with
        | Error _ -> disc
        | Ok toc ->
            let byNumber =
                toc
                |> Array.choose (fun t -> t.Rip |> Option.map (fun r -> t.Number, r))
                |> Map.ofArray

            let tracks =
                disc.Tracks
                |> Array.map (fun t ->
                    match Map.tryFind t.Number byNumber with
                    | Some rip -> { t with Rip = Some rip }
                    | None -> t)

            { disc with Tracks = tracks }

    let load (letter: string) =
        match loadDigital letter with
        | Ok(_, disc) -> Ok disc
        | Error digitalError ->
            match loadMci letter with
            | Ok disc -> Ok(withRipFromToc letter disc)
            | Error mciError -> Error $"{digitalError} / {mciError}"

    let tryLoadFirst () =
        let drives = listDrives ()

        if drives.Length = 0 then
            Error "光学ドライブが見つかりません"
        else
            let rec loop i last =
                if i >= drives.Length then
                    Error last
                else
                    match load drives[i] with
                    | Ok disc -> Ok disc
                    | Error e -> loop (i + 1) e

            loop 0 "ディスクを読めません"

    let eject (letter: string) =
        Mci.close ()

        match openHandle letter with
        | Error e -> Error e
        | Ok handle ->
            let dummy = Array.zeroCreate 1
            let result = ioctl handle Win32.ioctlStorageEject [||] dummy
            closeHandle handle

            match result with
            | Ok _ -> Ok()
            | Error code -> Error $"取り出しに失敗しました (Win32 {code})"

type CdWaveStream(handle: nativeint, startLba: int, sectorCount: int) =
    inherit WaveStream()

    let format = WaveFormat(44100, 16, 2)
    let length = int64 sectorCount * int64 Win32.bytesPerRawSector
    let mutable position = 0L
    let maxChunk = 26

    override _.WaveFormat = format
    override _.Length = length

    override _.Position
        with get () = position
        and set value =
            let aligned = value - (value % int64 Win32.bytesPerRawSector)
            position <- max 0L (min length aligned)

    override _.Read(buffer, offset, count) =
        let remaining = int (length - position)
        let toRead = min count remaining

        if toRead <= 0 then
            0
        else
            let mutable written = 0
            let mutable ok = true
            let tmp = Array.zeroCreate (maxChunk * Win32.bytesPerRawSector)

            while written < toRead && ok do
                let absPos = position + int64 written
                let sectorIndex = int (absPos / int64 Win32.bytesPerRawSector)
                let sectorOff = int (absPos % int64 Win32.bytesPerRawSector)
                let sectorsLeft = sectorCount - sectorIndex
                let n = min maxChunk sectorsLeft

                if n <= 0 then
                    ok <- false
                else
                    match CdDrive.readRaw handle (startLba + sectorIndex) n tmp 0 with
                    | Error _ -> ok <- false
                    | Ok() ->
                        let available = n * Win32.bytesPerRawSector - sectorOff
                        let take = min (toRead - written) available
                        Buffer.BlockCopy(tmp, sectorOff, buffer, offset + written, take)
                        written <- written + take

            position <- position + int64 written
            written
