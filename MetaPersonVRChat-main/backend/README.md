# Backend Docker para MetaPerson / Luca

Requisitos locales:
 - Docker & docker-compose
 - Modelo whisper.cpp (colocar en ./whisper/models/ggml-base.bin o montar volumen)

1) Preparar variables:
   - Crear un fichero .env en backend/ con:
     GENIALLE_API=http://genialle.uca.es:11434/api/chat
     WHISPER_BIN=/app/whisper/main
     WHISPER_MODEL_PATH=/app/whisper/models/ggml-base.bin

2) Coloca whisper compilado localmente (opcional):
   Si ya tienes whisper compilado, monta la carpeta como volumen:
     - ./whisper (contiene ejecutar 'main' y carpeta models)

3) Build & run:
   docker compose build
   docker compose up

4) Probar:
   - Health: http://MI_HOST:3000/health
   - Endpoint: POST http://MI_HOST:3000/api/audio
     body JSON: { "audio": "<base64 wav 16k/16bit>" }

5) Unity:
   - En tu APK usa la IP del host donde corre Docker.
   - Endpoint: http://<IP_HOST>:3000/api/audio
