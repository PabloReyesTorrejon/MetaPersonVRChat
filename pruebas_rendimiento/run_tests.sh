#!/bin/bash
# Script automatizado para ejecutar pruebas de rendimiento
# Uso: ./run_tests.sh [numero_iteraciones]

set -e

echo "========================================"
echo " PRUEBAS DE RENDIMIENTO - Backend"
echo "========================================"
echo

# Verificar que estamos en el directorio correcto
if [ ! -f "test_real_backend.py" ]; then
    echo "ERROR: No se encuentra test_real_backend.py"
    echo "Asegúrate de ejecutar este script desde la carpeta pruebas_rendimiento"
    exit 1
fi

# Determinar número de iteraciones
ITERATIONS=${1:-10}

echo "Ejecutando $ITERATIONS iteraciones..."
echo

# Ejecutar pruebas
python test_real_backend.py -n "$ITERATIONS"

if [ $? -ne 0 ]; then
    echo
    echo "ERROR: Las pruebas fallaron"
    echo "Verifica que el backend esté ejecutándose:"
    echo "   cd ../backend"
    echo "   docker-compose up"
    exit 1
fi

echo
echo "========================================"
echo " Pruebas completadas exitosamente"
echo "========================================"
echo

# Preguntar si analizar resultados
read -p "¿Analizar resultados ahora? (s/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Ss]$ ]]; then
    echo
    python analyze_performance.py performance_results_real_backend.csv
fi

echo
