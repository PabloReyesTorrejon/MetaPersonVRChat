FROM node:18-bullseye

# ----- Instalar dependencias de sistema -----
RUN apt-get update && apt-get install -y \
    python3 python3-pip python3-venv ffmpeg curl && \
    rm -rf /var/lib/apt/lists/*

# ----- Crear entorno virtual para Whisper -----
RUN python3 -m venv /venv
ENV PATH="/venv/bin:$PATH"

# ----- Instalar Whisper (openai-whisper) -----
RUN pip install --no-cache-dir openai-whisper

# ----- Crear directorio app -----
WORKDIR /app

# ----- Copiar package.json + instalar dependencias -----
COPY package.json package-lock.json* ./
RUN npm install

# ----- Copiar código del backend -----
COPY . .

# ----- Crear carpeta de uploads -----
RUN mkdir -p /app/uploads

# ----- Exponer puerto -----
EXPOSE 3000

# ----- Comando por defecto -----
CMD ["node", "server.js"]
