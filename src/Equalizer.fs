namespace Sound

open System
open NAudio.Dsp
open NAudio.Wave

type EqualizerProvider(source: ISampleProvider, bands: EqBand array) =
    let sampleRate = float32 source.WaveFormat.SampleRate
    let channels = source.WaveFormat.Channels
    let nyquist = sampleRate / 2.f
    let gate = obj ()

    let makeFilter (band: EqBand) =
        if band.Frequency >= nyquist - 1.f then
            None
        else
            Some(BiQuadFilter.PeakingEQ(sampleRate, band.Frequency, EqPreset.q, band.GainDb))

    let filters: BiQuadFilter option array array =
        Array.init channels (fun _ -> bands |> Array.map makeFilter)

    let mutable bypass = false

    member _.Bypass
        with get () = bypass
        and set value = bypass <- value

    member _.Update() =
        lock gate (fun () ->
            for ch = 0 to channels - 1 do
                for i = 0 to bands.Length - 1 do
                    match filters[ch][i] with
                    | Some filter ->
                        filter.SetPeakingEq(sampleRate, bands[i].Frequency, EqPreset.q, bands[i].GainDb)
                    | None -> ())

    interface ISampleProvider with
        member _.WaveFormat = source.WaveFormat

        member _.Read(buffer, offset, count) =
            let read = source.Read(buffer, offset, count)

            if not bypass then
                lock gate (fun () ->
                    for n = 0 to read - 1 do
                        let ch = n % channels
                        let mutable sample = buffer[offset + n]

                        for i = 0 to filters[ch].Length - 1 do
                            match filters[ch][i] with
                            | Some filter -> sample <- filter.Transform(sample)
                            | None -> ()

                        buffer[offset + n] <- Math.Clamp(sample, -1.f, 1.f))

            read
