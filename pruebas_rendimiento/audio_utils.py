"""
Utilidades para cargar y gestionar archivos de audio de prueba.
"""

import json
from pathlib import Path


def get_backend_uploads_dir():
    """Retorna el directorio de uploads del backend"""
    return Path(__file__).parent.parent / "backend" / "uploads"


def get_available_audio_files():
    """Obtiene lista de archivos WAV disponibles en uploads"""
    uploads_dir = get_backend_uploads_dir()
    if not uploads_dir.exists():
        return []
    
    wav_files = list(uploads_dir.glob("entrada_*.wav"))
    return sorted(wav_files, key=lambda x: x.stat().st_mtime, reverse=True)


def get_latest_audio_file():
    """Retorna el archivo de audio más reciente"""
    files = get_available_audio_files()
    return files[0] if files else None


def load_transcription_from_user_file():
    """Carga la transcripción real del archivo entrada_user.wav.txt"""
    uploads_dir = get_backend_uploads_dir()
    txt_file = uploads_dir / "entrada_user.wav.txt"
    
    if txt_file.exists():
        return txt_file.read_text(encoding='utf-8').strip()
    
    return None


def load_whisper_metadata():
    """Carga metadata de Whisper si existe"""
    uploads_dir = get_backend_uploads_dir()
    json_file = uploads_dir / "entrada_user.wav.json"
    
    if json_file.exists():
        try:
            with open(json_file, 'r', encoding='utf-8') as f:
                return json.load(f)
        except:
            return None
    
    return None


def get_user_transcription_examples():
    """
    Retorna ejemplos de transcripciones del usuario real.
    Intenta cargar desde entrada_user.wav.txt, sino usa defaults.
    """
    real_transcription = load_transcription_from_user_file()
    
    examples = [
        "Hola, ¿cómo estás?",
        "Cuéntame una historia",
        "¿Qué hora es?",
        "¿Cuáles son los campus de la UCA?",
        "¿Dónde está la biblioteca?",
        "Gracias por tu ayuda",
    ]
    
    if real_transcription:
        examples.insert(0, real_transcription)
    
    return examples


def get_audio_info(audio_path):
    """Retorna información sobre un archivo de audio"""
    if not audio_path or not Path(audio_path).exists():
        return None
    
    path = Path(audio_path)
    return {
        'name': path.name,
        'size_kb': path.stat().st_size / 1024,
        'modified': path.stat().st_mtime,
        'path': str(path)
    }
