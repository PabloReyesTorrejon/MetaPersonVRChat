"""
Script de pruebas de rendimiento REAL contra el backend server.js.
Mide tiempos reales de Whisper (transcripción) + Genialle (LLM) + TTS.

Requisitos:
    1. Backend ejecutándose: cd backend && docker-compose up
    2. Archivos de audio en backend/uploads/entrada_*.wav
    3. Dependencies: pip install -r requirements.txt

Uso:
    python test_real_backend.py -n 10
    python test_real_backend.py -n 5 --backend-url https://tu-ngrok.ngrok-free.dev
    python test_real_backend.py -n 20 --output results_20250107.csv
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
from audio_utils import get_latest_audio_file, get_audio_info, get_user_transcription_examples

OUTPUT_DIR = Path(__file__).parent
RESULTS_FILE = OUTPUT_DIR / "performance_results_real_backend.csv"
BACKEND_UPLOAD_DIR = OUTPUT_DIR.parent / "backend" / "uploads"

# Colores para terminal (opcional)
try:
    from colorama import init, Fore, Style
    init()
    COLOR_SUCCESS = Fore.GREEN
    COLOR_ERROR = Fore.RED
    COLOR_INFO = Fore.CYAN
    COLOR_RESET = Style.RESET_ALL
except ImportError:
    COLOR_SUCCESS = COLOR_ERROR = COLOR_INFO = COLOR_RESET = ""


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


def run_real_backend_test(num_iterations=10, backend_url="http://localhost:3000", verbose=True):
    """Ejecuta pruebas contra el backend REAL"""
    results = []
    
    if verbose:
        print(f"\n{'='*70}")
        print(f"PRUEBAS DE RENDIMIENTO REAL - Backend server.js")
        print(f"{'='*70}\n")
    
    # Usar audio_utils para obtener archivo
    test_audio_path = get_latest_audio_file()
    
    if not test_audio_path:
        print(f"{COLOR_ERROR}❌ No hay archivos entrada_*.wav en backend/uploads{COLOR_RESET}")
        return []
    
    audio_info = get_audio_info(test_audio_path)
    if verbose:
        print(f"[1/3] Archivo de audio REAL del usuario")
        print(f"      ✓ Archivo: {audio_info['name']}")
        print(f"      ✓ Tamaño: {audio_info['size_kb']:.1f} KB")
        print(f"      ✓ Ruta: {audio_info['path']}\n")
    
    audio_b64 = load_audio_as_base64(test_audio_path)
    if not audio_b64:
        return []
    
    if verbose:
        print(f"[2/3] Audio cargado en memoria ({len(audio_b64)} caracteres base64)\n")
        print(f"[3/3] Ejecutando {num_iterations} iteraciones contra {backend_url}\n")
        print(f"{'='*70}\n")
    
    successful_tests = 0
    failed_tests = 0
    
    for iteration in range(1, num_iterations + 1):
        if verbose:
            print(f"[Iteración {iteration}/{num_iterations}]")
        
        backend_result = send_audio_to_backend(audio_b64, backend_url)
        
        if not backend_result['success']:
            failed_tests += 1
            if verbose:
                print(f"  {COLOR_ERROR}❌ Error: {backend_result.get('error')}{COLOR_RESET}\n")
            
            results.append({
                "iteration": iteration,
                "timestamp": datetime.now().isoformat(),
                "success": False,
                "error": backend_result.get('error', 'Unknown')
            })
            continue
        
        successful_tests += 1
        
        if verbose:
            status_icon = "✓" if backend_result.get('has_real_metrics') else "~"
            print(f"  {COLOR_SUCCESS}{status_icon} Respuesta recibida en {backend_result['total_time_ms']:.1f} ms{COLOR_RESET}\n")
            
            if backend_result.get('has_real_metrics'):
                print(f"  📊 Métricas REALES desde backend:")
            else:
                print(f"  📊 Métricas estimadas:")
            
            print(f"     1. Transcripción (Whisper):  {backend_result['stt_time_ms']:.1f} ms")
            print(f"        → '{backend_result['transcription'][:60]}'")
            print(f"     2. Respuesta LLM (Genialle): {backend_result['llm_time_ms']:.1f} ms")
            print(f"     3. Síntesis TTS (gTTS):      {backend_result['tts_time_ms']:.1f} ms")
            
            if backend_result.get('network_overhead_ms', 0) > 0:
                print(f"     4. Overhead de red:          {backend_result['network_overhead_ms']:.1f} ms")
            
            print(f"  ────────────────────────────────")
            print(f"  ⏱  TIEMPO TOTAL: {backend_result['total_time_ms']:.1f} ms ({backend_result['total_time_ms']/1000:.2f} s)\n")
        
        results.append({
            "iteration": iteration,
            "timestamp": datetime.now().isoformat(),
            "success": True,
            "total_client_time_ms": round(backend_result['total_time_ms'], 2),
            "total_backend_time_ms": round(backend_result.get('total_backend_ms', 0), 2),
            "stt_whisper_time_ms": round(backend_result['stt_time_ms'], 2),
            "llm_genialle_time_ms": round(backend_result['llm_time_ms'], 2),
            "tts_gtts_time_ms": round(backend_result['tts_time_ms'], 2),
            "network_overhead_ms": round(backend_result.get('network_overhead_ms', 0), 2),
            "transcription": backend_result['transcription'][:100],
            "audio_file": audio_info['name'],
            "has_real_metrics": backend_result.get('has_real_metrics', False)
        })
    
    if verbose:
        print(f"{'='*70}")
        print(f"Pruebas completadas: {COLOR_SUCCESS}{successful_tests} exitosas{COLOR_RESET}, {COLOR_ERROR}{failed_tests} fallidas{COLOR_RESET}")
        print(f"{'='*70}\n")
    
    return results


def save_results(results, filename=None):
    """Guarda resultados en CSV y muestra estadísticas"""
    if not results:
        print(f"{COLOR_ERROR}❌ No hay resultados para guardar{COLOR_RESET}")
        return
    
    filename = filename or RESULTS_FILE
    
    # Filtrar resultados exitosos
    successful = [r for r in results if r.get('success', True) and 'total_backend_time_ms' in r]
    
    try:
        with open(filename, 'w', newline='', encoding='utf-8') as f:
            if successful:
                writer = csv.DictWriter(f, fieldnames=successful[0].keys())
            else:
                writer = csv.DictWriter(f, fieldnames=results[0].keys())
            writer.writeheader()
            writer.writerows(results)
        
        print(f"{COLOR_SUCCESS}✓ Resultados guardados en: {filename.resolve()}{COLOR_RESET}\n")
    except Exception as e:
        print(f"{COLOR_ERROR}❌ Error al guardar CSV: {e}{COLOR_RESET}\n")
        return
    
    if not successful:
        print(f"{COLOR_ERROR}❌ No hay resultados exitosos para analizar{COLOR_RESET}")
        return
    
    # Estadísticas detalladas
    print(f"{'='*70}")
    print(f"RESUMEN DE ESTADÍSTICAS ({len(successful)} pruebas exitosas)")
    
    has_real_metrics = any(r.get('has_real_metrics', False) for r in successful)
    if has_real_metrics:
        print(f"{COLOR_SUCCESS}✓ Usando MÉTRICAS REALES desde server.js{COLOR_RESET}")
    else:
        print(f"{COLOR_INFO}⚠ Usando estimaciones (actualiza server.js para métricas reales){COLOR_RESET}")
    
    print(f"{'='*70}\n")
    
    # Calcular estadísticas
    metrics = {
        'total_backend_time_ms': 'TIEMPO TOTAL BACKEND (Whisper + Genialle + TTS)',
        'total_client_time_ms': 'TIEMPO TOTAL CLIENTE (incluyendo red)',
        'stt_whisper_time_ms': 'TRANSCRIPCIÓN (Whisper)',
        'llm_genialle_time_ms': 'RESPUESTA LLM (Genialle)',
        'tts_gtts_time_ms': 'SÍNTESIS TTS (gTTS)',
        'network_overhead_ms': 'OVERHEAD DE RED'
    }
    
    for key, label in metrics.items():
        values = [r[key] for r in successful if key in r and r[key] > 0]
        if values:
            print(f"{label}:")
            print(f"  Promedio:  {np.mean(values):.2f} ms")
            print(f"  Mediana:   {np.median(values):.2f} ms")
            print(f"  Mínimo:    {np.min(values):.2f} ms")
            print(f"  Máximo:    {np.max(values):.2f} ms")
            print(f"  Desv. Est: {np.std(values):.2f} ms\n")
    
    # Mostrar transcripciones únicas
    transcriptions = set([r['transcription'] for r in successful if r.get('transcription')])
    if transcriptions:
        print(f"Transcripciones obtenidas:")
        for i, trans in enumerate(transcriptions, 1):
            print(f"  {i}. '{trans}'")
        print()
    
    print(f"{'='*70}\n")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(
        description="Pruebas de rendimiento REAL contra backend server.js (Whisper + Genialle + TTS)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Ejemplos de uso:
  python test_real_backend.py -n 10
  python test_real_backend.py -n 5 --backend-url https://tu-ngrok.ngrok-free.dev
  python test_real_backend.py -n 20 --output results_20250107.csv --quiet

Requisitos previos:
  1. Backend ejecutándose: cd backend && docker-compose up
  2. Archivos de audio en backend/uploads/entrada_*.wav
  3. Dependencias instaladas: pip install -r requirements.txt
        """
    )
    parser.add_argument(
        "-n", "--num-tests",
        type=int,
        default=10,
        help="Número de iteraciones de prueba (default: 10)"
    )
    parser.add_argument(
        "-b", "--backend-url",
        type=str,
        default="http://localhost:3000",
        help="URL del backend (default: http://localhost:3000)"
    )
    parser.add_argument(
        "-o", "--output",
        type=str,
        default=None,
        help="Archivo de salida CSV (default: performance_results_real_backend.csv)"
    )
    parser.add_argument(
        "-q", "--quiet",
        action="store_true",
        help="Modo silencioso (sin output detallado)"
    )
    
    args = parser.parse_args()
    
    if not args.quiet:
        print("\n╔════════════════════════════════════════════════════════════════════╗")
        print("║  PRUEBAS DE RENDIMIENTO REAL - Backend server.js                  ║")
        print("║  Whisper (STT) + Genialle (LLM) + gTTS (TTS)                      ║")
        print("╚════════════════════════════════════════════════════════════════════╝")
    
    results = run_real_backend_test(args.num_tests, args.backend_url, verbose=not args.quiet)
    
    if results:
        save_results(results, args.output)
    else:
        print(f"\n{COLOR_ERROR}❌ No se pudieron ejecutar las pruebas. Verifica:{COLOR_RESET}")
        print("   1. Backend ejecutándose: cd backend && docker-compose up")
        print("   2. URL correcta del backend")
        print("   3. Archivos de audio en backend/uploads/entrada_*.wav\n")
        sys.exit(1)


if __name__ == "__main__":
    main()
