# Pruebas de Rendimiento - MetaPersonVRChat Backend

Suite completa de scripts para medir el rendimiento real del backend (Whisper + Genialle + TTS).

## 📋 Tabla de Contenidos

- [Instalación](#instalación)
- [Uso Rápido](#uso-rápido)
- [Scripts Disponibles](#scripts-disponibles)
- [Requisitos Previos](#requisitos-previos)
- [Ejemplos de Uso](#ejemplos-de-uso)
- [Interpretación de Resultados](#interpretación-de-resultados)

## 🚀 Instalación

```bash
cd pruebas_rendimiento
pip install -r requirements.txt
```

## ⚡ Uso Rápido

### Windows (PowerShell)

```powershell
# Ejecutar backend
cd ..\backend
docker-compose up -d

# Ejecutar pruebas (desde pruebas_rendimiento)
cd ..\pruebas_rendimiento
python test_real_backend.py -n 10

# O usando el script automatizado
.\run_tests.bat 10
```

### Linux/macOS

```bash
# Ejecutar backend
cd ../backend
docker-compose up -d

# Ejecutar pruebas
cd ../pruebas_rendimiento
python test_real_backend.py -n 10

# O usando el script automatizado
chmod +x run_tests.sh
./run_tests.sh 10
```

## 📁 Scripts Disponibles

### test_real_backend.py

Script principal para medir rendimiento del backend real.

**Características:**
- Mide tiempos REALES de Whisper, Genialle y TTS desde server.js
- Usa archivos de audio reales del usuario (backend/uploads/entrada_*.wav)
- Calcula overhead de red
- Guarda resultados en CSV
- Muestra estadísticas detalladas

**Uso:**
```bash
# Básico
python test_real_backend.py -n 10

# Con backend ngrok
python test_real_backend.py -n 5 --backend-url https://tu-ngrok.ngrok-free.dev

# Guardar en archivo específico
python test_real_backend.py -n 20 --output results_20250107.csv

# Modo silencioso
python test_real_backend.py -n 10 --quiet
```

### analyze_performance.py

Analiza resultados de pruebas desde archivos CSV.

**Características:**
- Estadísticas detalladas (media, mediana, desviación, percentiles)
- Comparación entre múltiples pruebas
- Exportación a JSON

**Uso:**
```bash
# Analizar un archivo
python analyze_performance.py performance_results_real_backend.csv

# Comparar dos pruebas
python analyze_performance.py before.csv after.csv --compare --labels "Antes" "Después"

# Exportar a JSON
python analyze_performance.py results.csv --export-json stats.json
```

### run_all_tests.py

Ejecuta suite completa de pruebas automáticamente.

**Características:**
- Verifica que el backend esté ejecutándose
- Ejecuta múltiples configuraciones
- Analiza resultados automáticamente
- Genera reportes completos

**Uso:**
```bash
# Suite completa (10 iteraciones)
python run_all_tests.py

# Modo rápido (3 iteraciones)
python run_all_tests.py --quick

# Personalizado
python run_all_tests.py --iterations 20
```

### audio_utils.py

Utilidades para gestionar archivos de audio de prueba.

**Funciones:**
- `get_latest_audio_file()`: Obtiene el WAV más reciente
- `get_available_audio_files()`: Lista todos los WAV disponibles
- `load_transcription_from_user_file()`: Carga transcripción real
- `get_user_transcription_examples()`: Ejemplos de transcripciones

### Scripts de Automatización

#### run_tests.bat (Windows)

```cmd
run_tests.bat 10
```

#### run_tests.sh (Linux/macOS)

```bash
chmod +x run_tests.sh
./run_tests.sh 10
```

## ✅ Requisitos Previos

### 1. Backend Ejecutándose

El backend debe estar corriendo:

**Con Docker:**
```bash
cd backend
docker-compose up -d

# Ver logs
docker logs -f luca
```

**Sin Docker:**
```bash
cd backend
node server.js
```

### 2. Métricas Habilitadas en server.js

El archivo `server.js` debe incluir métricas en la respuesta:

```javascript
res.json({ 
  text: respuestaIA, 
  audio: audioBase64,
  metrics: {
    whisper_ms: 250,
    genialle_ms: 1500,
    tts_ms: 800,
    total_ms: 2550
  }
});
```

Si ves este mensaje en las pruebas:
```
✓ Usando MÉTRICAS REALES desde server.js
```
Significa que las métricas están funcionando correctamente.

### 3. Archivos de Audio

Se necesita al menos un archivo WAV en `backend/uploads/`:

```
backend/uploads/
  ├── entrada_1764113408403.wav
  ├── entrada_1763576766660.wav
  └── ...
```

Estos archivos se generan automáticamente cuando usas la aplicación Unity.

## 📊 Ejemplos de Uso

### Caso 1: Prueba Básica

```bash
cd pruebas_rendimiento
python test_real_backend.py -n 10
```

**Salida esperada:**
```
======================================================================
PRUEBAS DE RENDIMIENTO REAL - Backend server.js
======================================================================

[1/3] Archivo de audio REAL del usuario
      ✓ Archivo: entrada_1764113408403.wav
      ✓ Tamaño: 312.5 KB

[Iteración 1/10]
  ✓ Respuesta recibida en 2598.0 ms

  📊 Métricas REALES desde backend:
     1. Transcripción (Whisper):  258.0 ms
     2. Respuesta LLM (Genialle): 1520.0 ms
     3. Síntesis TTS (gTTS):      820.0 ms
  ────────────────────────────────
  ⏱  TIEMPO TOTAL: 2598.0 ms (2.60 s)
```

### Caso 2: Análisis de Resultados

```bash
python analyze_performance.py performance_results_real_backend.csv
```

**Salida esperada:**
```
======================================================================
ANÁLISIS: performance_results_real_backend.csv
Total de pruebas: 10
Exitosas: 10 | Fallidas: 0
======================================================================

Tiempo Total Backend:
  Promedio:  2550.45 ms
  Mediana:   2540.00 ms
  Mínimo:    2320.12 ms
  Máximo:    2890.78 ms
  Desv.Est:  145.23 ms
```

### Caso 3: Comparación de Pruebas

```bash
# Ejecutar prueba "antes"
python test_real_backend.py -n 10 -o before.csv

# (Hacer optimizaciones en el backend)

# Ejecutar prueba "después"
python test_real_backend.py -n 10 -o after.csv

# Comparar
python analyze_performance.py before.csv after.csv --compare --labels "Antes" "Después"
```

### Caso 4: Suite Completa

```bash
python run_all_tests.py
```

Ejecuta automáticamente:
1. Verificación del backend
2. Pruebas de rendimiento
3. Análisis de resultados
4. Generación de reportes

## 📈 Interpretación de Resultados

### Métricas Clave

| Componente | Tiempo Típico | Porcentaje | Optimización |
|-----------|---------------|------------|--------------|
| **Whisper (STT)** | 200-350ms | ~10-15% | Usar modelo más pequeño |
| **Genialle (LLM)** | 800-2500ms | ~50-70% | Limitar longitud respuesta |
| **gTTS (TTS)** | 500-1200ms | ~20-30% | Ajustar velocidad síntesis |
| **Overhead Red** | 100-500ms | adicional | Servidor local |

### Tiempos Objetivo

Para una experiencia fluida en VR:

- ✅ **< 2000ms (2s)**: Excelente
- ⚠️ **2000-3000ms**: Aceptable
- ❌ **> 3000ms (3s)**: Necesita optimización

### Identificar Cuellos de Botella

**Si Genialle es muy lento (>3s):**
- Servidor sobrecargado
- Modelo muy grande
- Pregunta muy compleja

**Si Whisper es muy lento (>500ms):**
- Audio muy largo
- Modelo muy grande
- CPU lenta

**Si TTS es muy lento (>1500ms):**
- Texto muy largo
- Velocidad de síntesis muy alta
- Conexión a Google TTS lenta

**Si Overhead de red es muy alto (>1s):**
- Conexión lenta
- Servidor muy lejano
- Payloads muy grandes

## 🔧 Solución de Problemas

### Error: "No se puede conectar al backend"

```
❌ No se puede conectar a http://localhost:3000
```

**Solución:**
1. Verifica que el backend esté ejecutándose
2. Verifica el puerto (3000 por defecto)
3. Si usas Docker, verifica que el puerto esté mapeado

### Error: "No hay archivos entrada_*.wav"

```
❌ No hay archivos entrada_*.wav en backend/uploads
```

**Solución:**
1. Graba audio desde la aplicación Unity
2. O copia un archivo WAV manualmente a `backend/uploads/`

### Métricas Estimadas en Lugar de Reales

```
⚠ Usando estimaciones (actualiza server.js)
```

**Solución:**
1. Asegúrate de tener la versión actualizada de `server.js`
2. El campo `metrics` debe estar en la respuesta JSON

## 📝 Notas

- Los archivos CSV se guardan en `pruebas_rendimiento/`
- Cada ejecución genera un timestamp único
- Las métricas son exactas cuando el backend las incluye
- El overhead de red varía según la conexión

## 🎯 Próximos Pasos

1. **Ejecutar pruebas**: `python test_real_backend.py -n 20`
2. **Documentar resultados**: Incluir en TFG (capítulo Evaluación)
3. **Optimizar componentes lentos**: Basándote en métricas
4. **Crear gráficos**: Visualizar distribución de tiempos
5. **Comparar configuraciones**: Diferentes modelos/parámetros
