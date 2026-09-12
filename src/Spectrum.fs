namespace Sound

open System
open NAudio.Dsp
open NAudio.Wave

type SpectrumAnalyzer(fftSize: int, sampleRate: int, barCount: int) =
    do
        if fftSize <= 0 || (fftSize &&& (fftSize - 1)) <> 0 then
            invalidArg (nameof fftSize) "FFT size must be a power of two."

    let m = int (Math.Log(float fftSize, 2.0))
    let window =
        Array.init fftSize (fun i -> float32 (FastFourierTransform.HannWindow(i, fftSize)))

    let acc = Array.zeroCreate fftSize
    let complex = Array.init fftSize (fun _ -> Complex())
    let bars = Array.zeroCreate barCount
    let peaks = Array.zeroCreate barCount
    let gate = obj ()
    let mutable write = 0
    let mutable currentRate = sampleRate

    let binFreq () = float currentRate / float fftSize

    let remap () =
        let nyquist = float currentRate / 2.0
        let lo = 40.0
        let hi = min 16000.0 (nyquist * 0.95)

        for bar = 0 to barCount - 1 do
            let t0 = float bar / float barCount
            let t1 = float (bar + 1) / float barCount
            let f0 = lo * Math.Pow(hi / lo, t0)
            let f1 = lo * Math.Pow(hi / lo, t1)
            let i0 = max 1 (int (f0 / binFreq ()))
            let i1 = min (fftSize / 2 - 1) (max (i0 + 1) (int (f1 / binFreq ())))
            let mutable peak = 0.f

            for i = i0 to i1 do
                let c = complex[i]
                let mag = MathF.Sqrt(c.X * c.X + c.Y * c.Y)
                if mag > peak then peak <- mag

            let db = 20.f * MathF.Log10(peak + 1e-8f)
            let norm = Math.Clamp((db + 72.f) / 72.f, 0.f, 1.f)
            let decayed = bars[bar] * 0.72f
            bars[bar] <- max decayed norm

            if bars[bar] >= peaks[bar] then
                peaks[bar] <- bars[bar]
            else
                peaks[bar] <- max 0.f (peaks[bar] - 0.018f)

    member _.SampleRate
        with get () = currentRate
        and set value = currentRate <- value

    member _.Add(buffer: float32 array, offset: int, count: int, channels: int) =
        if count <= 0 || channels <= 0 then
            ()
        else
            lock gate (fun () ->
                let frames = count / channels
                let mutable i = 0

                while i < frames do
                    let mutable mix = 0.f

                    for ch = 0 to channels - 1 do
                        mix <- mix + buffer[offset + i * channels + ch]

                    acc[write] <- mix / float32 channels
                    write <- write + 1

                    if write >= fftSize then
                        for n = 0 to fftSize - 1 do
                            complex[n].X <- acc[n] * window[n]
                            complex[n].Y <- 0.f

                        FastFourierTransform.FFT(true, m, complex)
                        remap ()
                        write <- 0

                    i <- i + 1)

    member _.Decay() =
        lock gate (fun () ->
            for i = 0 to barCount - 1 do
                bars[i] <- bars[i] * 0.86f
                peaks[i] <- max 0.f (peaks[i] - 0.02f))

    member _.Snapshot() =
        lock gate (fun () -> Array.copy bars, Array.copy peaks)

    member _.Reset() =
        lock gate (fun () ->
            Array.Clear(bars)
            Array.Clear(peaks)
            write <- 0)

type SpectrumTapProvider(source: ISampleProvider, analyzer: SpectrumAnalyzer) =
    do analyzer.SampleRate <- source.WaveFormat.SampleRate

    interface ISampleProvider with
        member _.WaveFormat = source.WaveFormat

        member _.Read(buffer, offset, count) =
            let read = source.Read(buffer, offset, count)
            analyzer.Add(buffer, offset, read, source.WaveFormat.Channels)
            read
