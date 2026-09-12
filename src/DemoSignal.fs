namespace Sound

open System
open NAudio.Wave

type DemoWaveStream(kind: DemoKind, duration: TimeSpan) =
    inherit WaveStream()

    let sampleRate = 44100
    let channels = 2
    let bytesPerSample = 2
    let format = WaveFormat(sampleRate, 16, channels)
    let totalFrames = int (duration.TotalSeconds * float sampleRate)
    let length = int64 totalFrames * int64 channels * int64 bytesPerSample
    let mutable position = 0L

    let hash (n: int) =
        let mutable x = uint32 n * 1664525u + 1013904223u
        x <- x ^^^ (x >>> 13)
        x <- x * 2654435761u
        x ^^^ (x >>> 16)

    let sampleAt (frame: int) =
        let t = float frame / float sampleRate
        let env =
            let attack = min 1.0 (t / 0.02)
            let remain = duration.TotalSeconds - t
            let release = min 1.0 (max 0.0 remain / 0.08)
            attack * release

        let raw =
            match kind with
            | Noise ->
                let h = hash frame
                (float (h &&& 0xFFFFu) / 32768.0 - 1.0) * 0.22
            | Bass ->
                let a = Math.Sin(2.0 * Math.PI * 55.0 * t)
                let b = Math.Sin(2.0 * Math.PI * 82.5 * t) * 0.55
                let c = Math.Sin(2.0 * Math.PI * 110.0 * t) * 0.28
                (a + b + c) * 0.38
            | Chord ->
                let freqs = [| 130.81; 164.81; 196.00; 261.63; 329.63 |]
                let mutable sum = 0.0

                for f in freqs do
                    sum <- sum + Math.Sin(2.0 * Math.PI * f * t)
                    sum <- sum + 0.28 * Math.Sin(2.0 * Math.PI * f * 2.0 * t)

                sum / 8.0
            | Sweep ->
                let f0 = 40.0
                let f1 = 16000.0
                let dur = max 0.001 duration.TotalSeconds
                let k = Math.Pow(f1 / f0, t / dur)
                let phase = 2.0 * Math.PI * f0 * dur / Math.Log(f1 / f0) * (k - 1.0)
                Math.Sin(phase) * 0.28

        raw * env

    override _.WaveFormat = format
    override _.Length = length

    override _.Position
        with get () = position
        and set value =
            let aligned = value - (value % int64 (channels * bytesPerSample))
            position <- max 0L (min length aligned)

    override _.Read(buffer, offset, count) =
        let remaining = int (length - position)
        let toRead = min count remaining
        let frames = toRead / (channels * bytesPerSample)
        let startFrame = int (position / int64 (channels * bytesPerSample))

        for i = 0 to frames - 1 do
            let v = sampleAt (startFrame + i)
            let pcm = int16 (Math.Clamp(v, -1.0, 1.0) * 32767.0)
            let p = offset + i * 4
            buffer[p] <- byte (int pcm &&& 0xFF)
            buffer[p + 1] <- byte ((int pcm >>> 8) &&& 0xFF)
            buffer[p + 2] <- buffer[p]
            buffer[p + 3] <- buffer[p + 1]

        let bytes = frames * channels * bytesPerSample
        position <- position + int64 bytes
        bytes

module DemoDisc =
    let create () : Disc =
        let specs =
            [|
                DemoKind.Noise, "ホワイトノイズ", TimeSpan.FromSeconds 40.0
                DemoKind.Bass, "低域テスト 55–110Hz", TimeSpan.FromSeconds 45.0
                DemoKind.Chord, "中域 和音 + 倍音", TimeSpan.FromSeconds 50.0
                DemoKind.Sweep, "40Hz–16kHz スイープ", TimeSpan.FromSeconds 35.0
            |]

        let tracks =
            specs
            |> Array.mapi (fun i (kind, title, dur) -> {
                Index = i
                Number = i + 1
                Title = title
                Duration = dur
                Source = DemoTone kind
            })

        {
            Title = "デモディスク"
            Artist = "SOUND EQ LAB"
            Kind = DemoDisc
            Drive = None
            Tracks = tracks
        }
