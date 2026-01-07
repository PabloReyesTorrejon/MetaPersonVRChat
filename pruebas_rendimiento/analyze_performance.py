"""
Analiza resultados de pruebas de rendimiento desde archivos CSV.

Uso:
    python analyze_performance.py performance_results_real_backend.csv
"""

import sys
import csv
import numpy as np
from pathlib import Path


def analyze_csv(filepath):
    """Analiza archivo CSV y muestra estadísticas"""
    data = []
    
    with open(filepath, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            data.append(row)
    
    if not data:
        print("No hay datos para analizar")
        return
    
    print(f"\n{'='*70}")
    print(f"ANÁLISIS: {Path(filepath).name}")
    print(f"Total de pruebas: {len(data)}")
    print(f"{'='*70}\n")
    
    # Analizar cada métrica
    metrics = {
        'total_backend_time_ms': 'Tiempo Total Backend',
        'stt_whisper_time_ms': 'Whisper (STT)',
        'llm_genialle_time_ms': 'Genialle (LLM)',
        'tts_gtts_time_ms': 'TTS (gTTS)'
    }
    
    for key, label in metrics.items():
        if key in data[0]:
            values = [float(row[key]) for row in data if row.get(key)]
            if values:
                print(f"{label}:")
                print(f"  Promedio:  {np.mean(values):.2f} ms")
                print(f"  Mediana:   {np.median(values):.2f} ms")
                print(f"  Mínimo:    {np.min(values):.2f} ms")
                print(f"  Máximo:    {np.max(values):.2f} ms")
                print(f"  Desv.Est:  {np.std(values):.2f} ms\n")


def main():
    if len(sys.argv) < 2:
        print("Uso: python analyze_performance.py <archivo.csv>")
        sys.exit(1)
    
    filepath = sys.argv[1]
    if not Path(filepath).exists():
        print(f"No se encuentra: {filepath}")
        sys.exit(1)
    
    analyze_csv(filepath)


if __name__ == "__main__":
    main()
