"""
Script automatizado para ejecutar todas las pruebas de rendimiento.
Ejecuta múltiples configuraciones y genera reportes completos.

Uso:
    python run_all_tests.py
    python run_all_tests.py --quick
    python run_all_tests.py --iterations 20
"""

import sys
import subprocess
import time
from pathlib import Path
from datetime import datetime


def print_header(text):
    """Imprime encabezado formateado"""
    print(f"\n{'='*70}")
    print(f"  {text}")
    print(f"{'='*70}\n")


def run_command(cmd, description):
    """Ejecuta un comando y muestra el resultado"""
    print(f"▶ {description}")
    print(f"  Comando: {' '.join(cmd)}\n")
    
    try:
        result = subprocess.run(cmd, capture_output=False, text=True)
        if result.returncode == 0:
            print(f"✓ {description} - Completado\n")
            return True
        else:
            print(f"❌ {description} - Falló con código {result.returncode}\n")
            return False
    except Exception as e:
        print(f"❌ Error ejecutando {description}: {e}\n")
        return False


def check_backend_running():
    """Verifica si el backend está ejecutándose"""
    print("Verificando si el backend está ejecutándose...")
    
    try:
        import requests
        response = requests.get("http://localhost:3000", timeout=2)
        print("✓ Backend accesible en http://localhost:3000\n")
        return True
    except:
        print("⚠ Backend no accesible en http://localhost:3000")
        print("  Asegúrate de ejecutar: cd backend && docker-compose up\n")
        
        response = input("¿Continuar de todas formas? (y/N): ")
        return response.lower() == 'y'


def run_performance_tests(iterations=10, backend_url="http://localhost:3000"):
    """Ejecuta pruebas de rendimiento contra el backend"""
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"performance_results_{timestamp}.csv"
    
    print_header(f"PRUEBAS DE RENDIMIENTO BACKEND ({iterations} iteraciones)")
    
    cmd = [
        sys.executable,
        "test_real_backend.py",
        "-n", str(iterations),
        "-b", backend_url,
        "-o", output_file
    ]
    
    success = run_command(cmd, f"Pruebas con {iterations} iteraciones")
    
    if success:
        return output_file
    return None


def analyze_results(csv_file):
    """Analiza resultados de pruebas"""
    print_header("ANÁLISIS DE RESULTADOS")
    
    cmd = [
        sys.executable,
        "analyze_performance.py",
        csv_file
    ]
    
    run_command(cmd, "Análisis estadístico")


def run_full_test_suite(quick_mode=False, iterations=10):
    """Ejecuta suite completa de pruebas"""
    print("\n╔════════════════════════════════════════════════════════════════════╗")
    print("║           SUITE COMPLETA DE PRUEBAS DE RENDIMIENTO                ║")
    print("╚════════════════════════════════════════════════════════════════════╝")
    
    start_time = time.time()
    
    # Verificar backend
    if not check_backend_running():
        return
    
    # Configuraciones de prueba
    if quick_mode:
        configs = [
            {"iterations": 3, "desc": "Prueba rápida"}
        ]
    else:
        configs = [
            {"iterations": iterations, "desc": f"Prueba estándar ({iterations} iteraciones)"}
        ]
    
    results_files = []
    
    for config in configs:
        output_file = run_performance_tests(
            iterations=config["iterations"],
            backend_url="http://localhost:3000"
        )
        
        if output_file and Path(output_file).exists():
            results_files.append(output_file)
            
            # Analizar inmediatamente
            analyze_results(output_file)
    
    # Resumen final
    elapsed = time.time() - start_time
    
    print_header("RESUMEN DE PRUEBAS")
    print(f"Tiempo total: {elapsed:.1f} segundos")
    print(f"Archivos generados: {len(results_files)}\n")
    
    for f in results_files:
        print(f"  - {f}")
    
    print(f"\n{'='*70}\n")
    print("Próximos pasos:")
    print("  1. Revisa los archivos CSV generados")
    print("  2. Compara resultados: python analyze_performance.py file1.csv file2.csv --compare")
    print("  3. Documenta las métricas en tu TFG\n")


def main():
    import argparse
    
    parser = argparse.ArgumentParser(
        description="Suite automatizada de pruebas de rendimiento",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Ejemplos de uso:
  python run_all_tests.py
  python run_all_tests.py --quick
  python run_all_tests.py --iterations 20
  python run_all_tests.py --backend-url https://tu-ngrok.ngrok-free.dev

Requisitos:
  - Backend ejecutándose (cd backend && docker-compose up)
  - Dependencias instaladas (pip install -r requirements.txt)
  - Archivos de audio en backend/uploads/entrada_*.wav
        """
    )
    parser.add_argument(
        "--quick",
        action="store_true",
        help="Modo rápido (solo 3 iteraciones)"
    )
    parser.add_argument(
        "--iterations",
        type=int,
        default=10,
        help="Número de iteraciones por prueba (default: 10)"
    )
    parser.add_argument(
        "--backend-url",
        type=str,
        default="http://localhost:3000",
        help="URL del backend (default: http://localhost:3000)"
    )
    
    args = parser.parse_args()
    
    try:
        run_full_test_suite(
            quick_mode=args.quick,
            iterations=args.iterations
        )
    except KeyboardInterrupt:
        print("\n\n⚠ Pruebas interrumpidas por el usuario\n")
        sys.exit(1)


if __name__ == "__main__":
    main()
