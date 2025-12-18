import sys
import whisper

if len(sys.argv) < 2:
    print("")
    sys.exit(0)

audio_path = sys.argv[1]

model = whisper.load_model("base")
result = model.transcribe(audio_path, language="es")

print(result["text"])
