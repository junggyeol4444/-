import tempfile
import unittest
import wave
from pathlib import Path
from myvocal.audio import SAMPLE_RATE, render_demo

class AudioRenderTest(unittest.TestCase):
    def test_renders_valid_stereo_wave(self):
        with tempfile.TemporaryDirectory() as folder:
            path = render_demo(Path(folder) / "demo.wav", bpm=120, bars=1)
            self.assertTrue(path.exists())
            with wave.open(str(path), "rb") as audio:
                self.assertEqual(audio.getnchannels(), 2)
                self.assertEqual(audio.getframerate(), SAMPLE_RATE)
                self.assertGreater(audio.getnframes(), SAMPLE_RATE)

if __name__ == "__main__":
    unittest.main()
