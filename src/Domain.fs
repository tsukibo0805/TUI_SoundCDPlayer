namespace Sound

open System

type RepeatMode =
    | RepeatOff
    | RepeatAll
    | RepeatOne

    member this.Label =
        match this with
        | RepeatOff -> "OFF"
        | RepeatAll -> "ALL"
        | RepeatOne -> "ONE"

    member this.Next() =
        match this with
        | RepeatOff -> RepeatAll
        | RepeatAll -> RepeatOne
        | RepeatOne -> RepeatOff

type PlaybackState =
    | Stopped
    | Playing
    | Paused

    member this.Label =
        match this with
        | Stopped -> "STOP"
        | Playing -> "PLAY"
        | Paused -> "PAUSE"

type FocusPane =
    | TrackList
    | Equalizer

    member this.Next() =
        match this with
        | TrackList -> Equalizer
        | Equalizer -> TrackList

type DemoKind =
    | Noise
    | Bass
    | Chord
    | Sweep

type TrackSource =
    | DigitalCd of handle: nativeint * startLba: int * sectors: int
    | MciCd of drive: string * trackNumber: int
    | AudioFile of path: string
    | DemoTone of kind: DemoKind

type Track = {
    Index: int
    Number: int
    Title: string
    Duration: TimeSpan
    Source: TrackSource
}

type DiscKind =
    | DigitalAudioCd
    | MciAudioCd
    | FolderDisc
    | DemoDisc

    member this.Label =
        match this with
        | DigitalAudioCd -> "CD DIGITAL"
        | MciAudioCd -> "CD MCI"
        | FolderDisc -> "FOLDER"
        | DemoDisc -> "DEMO"

type Disc = {
    Title: string
    Artist: string
    Kind: DiscKind
    Drive: string option
    Tracks: Track array
}

type EqBand = {
    Label: string
    Frequency: float32
    mutable GainDb: float32
}

module EqPreset =
    let minGain = -12.f
    let maxGain = 12.f
    let q = 1.41f

    let createBands () : EqBand array =
        [|
            { Label = "32"; Frequency = 32.f; GainDb = 0.f }
            { Label = "64"; Frequency = 64.f; GainDb = 0.f }
            { Label = "125"; Frequency = 125.f; GainDb = 0.f }
            { Label = "250"; Frequency = 250.f; GainDb = 0.f }
            { Label = "500"; Frequency = 500.f; GainDb = 0.f }
            { Label = "1k"; Frequency = 1000.f; GainDb = 0.f }
            { Label = "2k"; Frequency = 2000.f; GainDb = 0.f }
            { Label = "4k"; Frequency = 4000.f; GainDb = 0.f }
            { Label = "8k"; Frequency = 8000.f; GainDb = 0.f }
            { Label = "16k"; Frequency = 16000.f; GainDb = 0.f }
        |]

    let clampGain (db: float32) =
        Math.Clamp(db, minGain, maxGain)

type PendingAction =
    | Idle
    | OpenFolder
    | Quit

module TimeFmt =
    let mmss (t: TimeSpan) =
        let total = max 0.0 t.TotalSeconds
        let m = int total / 60
        let s = int total % 60
        $"%02d{m}:%02d{s}"

    let fromSectors (sectors: int) =
        TimeSpan.FromSeconds(float sectors / 75.0)

module CdMath =
    let msfToLba (m: int) (s: int) (f: int) = (m * 60 + s) * 75 + f - 150

    let lbaToMsf lba =
        let abs = lba + 150
        let m = abs / (60 * 75)
        let rem = abs % (60 * 75)
        let s = rem / 75
        let f = rem % 75
        m, s, f
