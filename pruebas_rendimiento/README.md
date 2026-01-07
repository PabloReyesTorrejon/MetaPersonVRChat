# Pruebas de Rendimiento - MetaPersonVRChat

Scripts para medir el rendimiento real del backend (Whisper + Genialle + TTS).

## Instalación

```bash
pip install -r requirements.txt
```

## Uso

### Pruebas con Backend Real

```bash
# Asegúrate de que el backend esté corriendo
cd ../backend
docker-compose up -d

# Ejecuta las pruebas
cd ../pruebas_rendimiento
python test_real_backend.py -n 10
```

## Scripts Principales

- **test_real_backend.py**: Mide tiempos reales de Whisper, Genialle y TTS
- **analyze_performance.py**: Analiza resultados de pruebas

## Resultados

Los resultados se guardan en archivos CSV con métricas detalladas de cada componente.
