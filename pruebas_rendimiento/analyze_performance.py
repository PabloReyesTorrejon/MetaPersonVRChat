"""
Analiza resultados de pruebas de rendimiento desde archivos CSV.
Genera estadísticas detalladas y comparaciones entre diferentes pruebas.

Uso:
    python analyze_performance.py performance_results_real_backend.csv
    python analyze_performance.py file1.csv file2.csv --compare
    python analyze_performance.py results.csv --export-json stats.json
"""

import sys
import csv
import json
import numpy as np
from pathlib import Path
from datetime import datetime


def load_csv_data(filepath):
    """Carga datos desde archivo CSV"""
    data = []
    
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                # Convertir valores numéricos
                converted_row = {}
                for key, value in row.items():
                    try:
                        # Intentar convertir a float si parece numérico
                        if value and ('_ms' in key or 'time' in key.lower()):
                            converted_row[key] = float(value)
                        else:
                            converted_row[key] = value
                    except ValueError:
                        converted_row[key] = value
                
                data.append(converted_row)
        
        return data
    except FileNotFoundError:
        print(f"❌ No se encuentra el archivo: {filepath}")
        return None
    except Exception as e:
        print(f"❌ Error al leer {filepath}: {e}")
        return None


def calculate_statistics(values):
    """Calcula estadísticas detalladas de una lista de valores"""
    if not values:
        return None
    
    values_array = np.array(values)
    
    return {
        'mean': np.mean(values_array),
        'median': np.median(values_array),
        'min': np.min(values_array),
        'max': np.max(values_array),
        'std': np.std(values_array),
        'q25': np.percentile(values_array, 25),
        'q75': np.percentile(values_array, 75),
        'count': len(values)
    }
    
def analyze_csv(filepath, verbose=True):
    """Analiza archivo CSV y muestra estadísticas detalladas"""
    data = load_csv_data(filepath)
    
    if not data:
        return None
    
    # Filtrar solo resultados exitosos
    successful = [r for r in data if r.get('success', 'True') != 'False']
    
    if verbose:
        print(f"\n{'='*70}")
        print(f"ANÁLISIS: {Path(filepath).name}")
        print(f"Total de pruebas: {len(data)}")
        print(f"Exitosas: {len(successful)} | Fallidas: {len(data) - len(successful)}")
        print(f"{'='*70}\n")
    
    if not successful:
        print("❌ No hay datos exitosos para analizar")
        return None
    
    # Detectar métricas disponibles
    sample_row = successful[0]
    available_metrics = {
        'total_backend_time_ms': 'Tiempo Total Backend',
        'total_client_time_ms': 'Tiempo Total Cliente',
        'stt_whisper_time_ms': 'Whisper (STT)',
        'llm_genialle_time_ms': 'Genialle (LLM)',
        'tts_gtts_time_ms': 'TTS (gTTS)',
        'network_overhead_ms': 'Overhead de Red'
    }
    
    results = {}
    
    for key, label in available_metrics.items():
        if key in sample_row:
            values = [r[key] for r in successful if key in r and r[key] and r[key] != '']
            if values:
                try:
                    values = [float(v) for v in values]
                    stats = calculate_statistics(values)
                    results[key] = stats
                    
                    if verbose:
                        print(f"{label}:")
                        print(f"  Promedio:  {stats['mean']:.2f} ms")
                        print(f"  Mediana:   {stats['median']:.2f} ms")
                        print(f"  Mínimo:    {stats['min']:.2f} ms")
                        print(f"  Máximo:    {stats['max']:.2f} ms")
                        print(f"  Desv.Est:  {stats['std']:.2f} ms")
                        print(f"  Q1-Q3:     {stats['q25']:.2f} - {stats['q75']:.2f} ms\n")
                except (ValueError, TypeError):
                    continue
    
    # Mostrar transcripciones únicas
    if verbose and 'transcription' in sample_row:
        transcriptions = set([r.get('transcription', '') for r in successful if r.get('transcription')])
        if transcriptions:
            print(f"Transcripciones únicas obtenidas:")
            for i, trans in enumerate(transcriptions, 1):
                print(f"  {i}. '{trans}'")
            print()
    
    return results


def compare_csvs(filepaths, labels=None):
    """Compara múltiples archivos CSV"""
    if labels is None:
        labels = [f"Prueba {i+1}" for i in range(len(filepaths))]
    
    print(f"\n{'='*70}")
    print(f"COMPARACIÓN DE RESULTADOS")
    print(f"{'='*70}\n")
    
    all_results = {}
    
    for filepath, label in zip(filepaths, labels):
        print(f"Analizando: {label} ({Path(filepath).name})")
        results = analyze_csv(filepath, verbose=False)
        if results:
            all_results[label] = results
    
    if not all_results:
        print("❌ No hay resultados para comparar")
        return
    
    print(f"\n{'='*70}")
    print("COMPARACIÓN DE MÉTRICAS")
    print(f"{'='*70}\n")
    
    # Obtener todas las métricas disponibles
    all_metrics = set()
    for results in all_results.values():
        all_metrics.update(results.keys())
    
    metric_names = {
        'total_backend_time_ms': 'Tiempo Total Backend',
        'total_client_time_ms': 'Tiempo Total Cliente',
        'stt_whisper_time_ms': 'Whisper (STT)',
        'llm_genialle_time_ms': 'Genialle (LLM)',
        'tts_gtts_time_ms': 'TTS (gTTS)',
        'network_overhead_ms': 'Overhead de Red'
    }
    
    for metric in sorted(all_metrics):
        metric_name = metric_names.get(metric, metric)
        print(f"{metric_name}:")
        
        for label, results in all_results.items():
            if metric in results:
                stats = results[metric]
                print(f"  {label:20s}: {stats['mean']:8.2f} ms  (±{stats['std']:6.2f})")
        print()


def export_to_json(results, output_file):
    """Exporta resultados a JSON"""
    try:
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2, default=str)
        print(f"✓ Exportado a JSON: {output_file}")
    except Exception as e:
        print(f"❌ Error al exportar: {e}")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(
        description="Analiza resultados de pruebas de rendimiento",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Ejemplos de uso:
  python analyze_performance.py results.csv
  python analyze_performance.py file1.csv file2.csv --compare --labels "Antes" "Después"
  python analyze_performance.py results.csv --export-json stats.json
        """
    )
    parser.add_argument(
        "files",
        nargs='+',
        help="Archivos CSV a analizar"
    )
    parser.add_argument(
        "--compare",
        action="store_true",
        help="Comparar múltiples archivos CSV"
    )
    parser.add_argument(
        "--labels",
        nargs='+',
        help="Etiquetas para cada archivo en comparación"
    )
    parser.add_argument(
        "--export-json",
        type=str,
        help="Exportar resultados a archivo JSON"
    )
    
    args = parser.parse_args()
    
    # Verificar que existan todos los archivos
    for filepath in args.files:
        if not Path(filepath).exists():
            print(f"❌ No se encuentra: {filepath}")
            sys.exit(1)
    
    if args.compare and len(args.files) > 1:
        # Modo comparación
        compare_csvs(args.files, args.labels)
    else:
        # Modo análisis individual
        for filepath in args.files:
            results = analyze_csv(filepath)
            
            if results and args.export_json:
                export_to_json(results, args.export_json)


if __name__ == "__main__":
    main()
