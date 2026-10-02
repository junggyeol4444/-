"""Small dependency-free demo renderer used by the desktop prototype."""
from __future__ import annotations
import math
import struct
import wave
from pathlib import Path

SAMPLE_RATE = 44_100


def render_demo(path: str | Path, bpm: int = 128, bars: int = 8) -> Path:
    """Render an original chord-loop demo to a stereo 16-bit WAV file."""
    path = Path(path)
    beat = 60 / bpm
    duration = bars * 4 * beat
    chords = ((220.00, 261.63, 329.63), (174.61, 220.00, 261.63),
              (130.81, 164.81, 196.00), (196.00, 246.94, 293.66))
    frames = bytearray()
    for index in range(int(duration * SAMPLE_RATE)):
        t = index / SAMPLE_RATE
        chord = chords[int(t / (beat * 2)) % len(chords)]
        envelope = min(1.0, (t % beat) * 20) * math.exp(-(t % beat) * 1.2)
        melodic = sum(math.sin(2 * math.pi * hz * t) for hz in chord) / 3
        bass = math.sin(2 * math.pi * chord[0] / 2 * t)
        kick_phase = t % beat
        kick = math.sin(2 * math.pi * (75 - 35 * kick_phase) * t) * math.exp(-kick_phase * 18)
        sample = max(-1, min(1, (melodic * .25 + bass * .18) * envelope + kick * .28))
        value = int(sample * 22_000)
        frames.extend(struct.pack("<hh", value, value))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2); output.setsampwidth(2); output.setframerate(SAMPLE_RATE)
        output.writeframes(frames)
    return path
