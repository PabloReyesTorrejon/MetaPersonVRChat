"""
INICIO RÁPIDO - Pruebas de Rendimiento
Ejecuta este script para comenzar rápidamente con las pruebas.
"""

import sys
import subprocess
from pathlib import Path


def print_banner():
    print("""
╔════════════════════════════════════════════════════════════════════╗
║                   PRUEBAS DE RENDIMIENTO                           ║
║                  MetaPersonVRChat Backend                          ║
╚════════════════════════════════════════════════════════════════════╝
""")


def check_dependencies():
    """Verifica que las dependencias estén instaladas"""
    print("Verificando dependencias...")
    
    try:
        import numpy
        import requests
        print("✓ Dependencias instaladas\n")
        return True
    except ImportError as e:
        print(f"❌ Falta dependencia: {e}")
        print("\nInstala las dependencias con:")
        print("  pip install -r requirements.txt\n")
        return False


def check_backend():
    """Verifica si el backend está accesible"""
    print("Verificando backend...")
    
    try:
        import requests
        response = requests.get("http://localhost:3000", timeout=2)
        print("✓ Backend accesible en http://localhost:3000\n")
        return True
    except:
        print("❌ Backend no accesible\n")
        print("Inicia el backend con:")
        print("  cd ../backend")
        print("  docker-compose up -d\n")
        return False


def check_audio_files():
    """Verifica que haya archivos de audio disponibles"""
    print("Verificando archivos de audio...")
    
    uploads_dir = Path(__file__).parent.parent / "backend" / "uploads"
    wav_files = list(uploads_dir.glob("entrada_*.wav")) if uploads_dir.exists() else []
    
    if wav_files:
        print(f"✓ {len(wav_files)} archivo(s) WAV encontrado(s)\n")
        return True
    else:
        print("⚠ No hay archivos WAV en backend/uploads/\n")
        print("Graba audio desde la aplicación Unity o copia archivos WAV manualmente.\n")
        return False


def run_quick_test():
    """Ejecuta prueba rápida"""
    print("═" * 70)
    print("EJECUTANDO PRUEBA RÁPIDA (3 iteraciones)")
    print("═" * 70)
    print()
    
    cmd = [sys.executable, "test_real_backend.py", "-n", "3"]
    
    try:
        subprocess.run(cmd)
        return True
    except Exception as e:
        print(f"❌ Error: {e}")
        return False


def show_menu():
    """Muestra menú interactivo"""
    print("═" * 70)
    print("MENÚ PRINCIPAL")
    print("═" * 70)
    print()
    print("1. Prueba rápida (3 iteraciones)")
    print("2. Prueba estándar (10 iteraciones)")
    print("3. Prueba completa (20 iteraciones)")
    print("4. Ejecutar suite completa (run_all_tests.py)")
    print("5. Analizar resultados existentes")
    print("6. Ver ayuda completa")
    print("7. Salir")
    print()
    
    choice = input("Selecciona una opción (1-7): ")
    return choice


def main():
    print_banner()
    
    # Verificaciones previas
    if not check_dependencies():
        sys.exit(1)
    
    backend_ok = check_backend()
    audio_ok = check_audio_files()
    
    if not backend_ok:
        response = input("¿Continuar de todas formas? (y/N): ")
        if response.lower() != 'y':
            sys.exit(1)
    
    if not audio_ok:
        response = input("¿Continuar de todas formas? (y/N): ")
        if response.lower() != 'y':
            sys.exit(1)
    
    print()
    
    # Menú interactivo
    while True:
        choice = show_menu()
        
        if choice == "1":
            subprocess.run([sys.executable, "test_real_backend.py", "-n", "3"])
        elif choice == "2":
            subprocess.run([sys.executable, "test_real_backend.py", "-n", "10"])
        elif choice == "3":
            subprocess.run([sys.executable, "test_real_backend.py", "-n", "20"])
        elif choice == "4":
            subprocess.run([sys.executable, "run_all_tests.py"])
        elif choice == "5":
            csv_file = input("Archivo CSV a analizar (default: performance_results_real_backend.csv): ")
            if not csv_file:
                csv_file = "performance_results_real_backend.csv"
            subprocess.run([sys.executable, "analyze_performance.py", csv_file])
        elif choice == "6":
            print("\n" + "═" * 70)
            print("AYUDA COMPLETA")
            print("═" * 70 + "\n")
            print("Consulta README.md para documentación completa")
            print()
            print("Comandos útiles:")
            print("  python test_real_backend.py -n 10")
            print("  python analyze_performance.py results.csv")
            print("  python run_all_tests.py --quick")
            print()
            input("Presiona Enter para continuar...")
        elif choice == "7":
            print("\n¡Hasta luego!\n")
            break
        else:
            print("\n❌ Opción inválida. Intenta de nuevo.\n")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n⚠ Interrumpido por el usuario\n")
        sys.exit(0)
