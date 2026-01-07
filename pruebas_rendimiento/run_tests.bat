@echo off
REM Script automatizado para ejecutar pruebas de rendimiento en Windows
REM Uso: run_tests.bat [numero_iteraciones]

echo ========================================
echo  PRUEBAS DE RENDIMIENTO - Backend
echo ========================================
echo.

REM Verificar que estamos en el directorio correcto
if not exist "test_real_backend.py" (
    echo ERROR: No se encuentra test_real_backend.py
    echo Asegurate de ejecutar este script desde la carpeta pruebas_rendimiento
    pause
    exit /b 1
)

REM Determinar numero de iteraciones
set ITERATIONS=10
if not "%1"=="" set ITERATIONS=%1

echo Ejecutando %ITERATIONS% iteraciones...
echo.

REM Ejecutar pruebas
python test_real_backend.py -n %ITERATIONS%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Las pruebas fallaron
    echo Verifica que el backend este ejecutandose:
    echo    cd ..\backend
    echo    docker-compose up
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Pruebas completadas exitosamente
echo ========================================
echo.

REM Preguntar si analizar resultados
set /p ANALYZE="Analizar resultados ahora? (S/N): "
if /i "%ANALYZE%"=="S" (
    echo.
    python analyze_performance.py performance_results_real_backend.csv
)

echo.
pause
