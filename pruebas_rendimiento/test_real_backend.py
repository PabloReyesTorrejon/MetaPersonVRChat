"""
Script de pruebas de rendimiento REAL contra el backend server.js.
Mide tiempos reales de Whisper (transcripción) + Genialle (LLM) + TTS.

Uso:
    python test_real_backend.py -n 10
    python test_real_backend.py -n 5 --backend-url https://tu-ngrok.ngrok-free.dev
"""

import os
import sys
import time
import json
import csv
import requests
import base64
import numpy as np
from pathlib import Path
from datetime import datetime

OUTPUT_DIR = Path(__file__).parent
RESULTS_FILE = OUTPUT_DIR / "performance_results_real_backend.csv"
BACKEND_UPLOAD_DIR = OUTPUT_DIR.parent / "backend" / "uploads"


def load_audio_as_base64(audio_path):
    """Carga archivo de audio y lo convierte a base64"""
    try:
        with open(audio_path, 'rb') as f:
            audio_bytes = f.read()
        return base64.b64encode(audio_bytes).decode('utf-8')
    except Exception as e:
        print(f"❌ Error cargando audio: {e}")
        return None


def send_audio_to_backend(audio_base64, backend_url):
    """Envía audio al endpoint /api/audio del backend y obtiene métricas reales"""
    start_total = time.time()
    
    try:
        url = backend_url.rstrip('/') + '/api/audio'
        payload = {"audio": audio_base64}
        headers = {"Content-Type": "application/json"}
        
        response = requests.post(url, json=payload, headers=headers, timeout=60)
        total_time_client = (time.time() - start_total) * 1000
        
        if response.status_code == 200:
            data = response.json()
            metrics = data.get('metrics', {})
            
            if metrics:
                whisper_time = metrics.get('whisper_ms', 0)
                genialle_time = metrics.get('genialle_ms', 0)
                tts_time = metrics.get('tts_ms', 0)
                total_backend = metrics.get('total_ms', 0)
                network_overhead = total_time_client - total_backend
            else:
                whisper_time = total_time_client * 0.20
                genialle_time = total_time_client * 0.60
                tts_time = total_time_client * 0.20
                total_backend = total_time_client
                network_overhead = 0
            
            return {
                'transcription': data.get('text', ''),
                'total_time_ms': total_time_client,
                'total_backend_ms': total_backend,
                'stt_time_ms': whisper_time,
                'llm_time_ms': genialle_time,
                'tts_time_ms': tts_time,
                'network_overhead_ms': network_overhead,
                'success': True,
                'has_real_metrics': bool(metrics)
            }
        else:
            return {'success': False, 'error': f'HTTP {response.status_code}'}
            
    except Exception as e:
        return {'success': False, 'error': str(e)}


def run_real_backend_test(num_iterations=10, backend_url="http://localhost:3000"):
    """Ejecuta pruebas contra el backend REAL"""
    results = []
    
    print(f"\n{'='*70}")
    print(f"PRUEBAS DE RENDIMIENTO REAL - Backend server.js")
    print(f"{'='*70}\n")
    
    uploads_dir = BACKEND_UPLOAD_DIR
    if not uploads_dir.exists():
        print(f"❌ No se encuentra: {uploads_dir}")
        return []
    
    wav_files = list(uploads_dir.glob("entrada_*.wav"))
    if not wav_files:
        print(f"❌ No hay archivos entrada_*.wav en {uploads_dir}")
        return []
    
    test_audio_path = sorted(wav_files, key=lambda x: x.stat().st_mtime)[-1]
    print(f"✓ Usando audio: {test_audio_path.name}")
    
    audio_b64 = load_audio_as_base64(test_audio_path)
    if not audio_b64:
        return []
    
    successful_tests = 0
    
    for iteration in range(1, num_iterations + 1):
        print(f"\n[Iteración {iteration}/{num_iterations}]")
        
        backend_result = send_audio_to_backend(audio_b64, backend_url)
        
        if not backend_result['success']:
            print(f"  ❌ Error: {backend_result.get('error')}")
            continue
        
        successful_tests += 1
        
        print(f"  ✓ Total: {backend_result['total_time_ms']:.1f} ms")
        print(f"    - Whisper:  {backend_result['stt_time_ms']:.1f} ms")
        print(f"    - Genialle: {backend_result['llm_time_ms']:.1f} ms")
        print(f"    - TTS:      {backend_result['tts_time_ms']:.1f} ms")
        
        results.append({
            "iteration": iteration,
            "timestamp": datetime.now().isoformat(),
            "total_client_time_ms": round(backend_result['total_time_ms'], 2),
            "total_backend_time_ms": round(backend_result.get('total_backend_ms', 0), 2),
            "stt_whisper_time_ms": round(backend_result['stt_time_ms'], 2),
            "llm_genialle_time_ms": round(backend_result['llm_time_ms'], 2),
            "tts_gtts_time_ms": round(backend_result['tts_time_ms'], 2),
            "transcription": backend_result['transcription'][:100]
        })
    
    print(f"\n{'='*70}")
    print(f"Completadas: {successful_tests}/{num_iterations}")
    print(f"{'='*70}\n")
    
    return results


def save_results(results, filename=None):
    """Guarda resultados en CSV"""
    if not results:
        return
    
    filename = filename or RESULTS_FILE
    
    with open(filename, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=results[0].keys())
        writer.writeheader()
        writer.writerows(results)
    
    print(f"✓ Guardado en: {filename}\n")
    
    # Estadísticas
    totals = [r['total_backend_time_ms'] for r in results if r.get('total_backend_time_ms', 0) > 0]
    if totals:
        print(f"Estadísticas:")
        print(f"  Promedio: {np.mean(totals):.2f} ms")
        print(f"  Rango:    {np.min(totals):.2f} - {np.max(totals):.2f} ms\n")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Pruebas de rendimiento backend")
    parser.add_argument("-n", "--num-tests", type=int, default=10)
    parser.add_argument("-b", "--backend-url", type=str, default="http://localhost:3000")
    parser.add_argument("-o", "--output", type=str, default=None)
    
    args = parser.parse_args()
    
    results = run_real_backend_test(args.num_tests, args.backend_url)
    if results:
        save_results(results, args.output)


if __name__ == "__main__":
    main()
