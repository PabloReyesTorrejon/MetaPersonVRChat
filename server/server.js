import express from "express";
import fs from "fs";
import { execSync } from "child_process";
import path from "path";

const app = express();
const PORT = 3000;

// === CONFIGURACIÓN GENERAL ===
app.use(express.json({ limit: "1gb" }));
app.use(express.urlencoded({ extended: true, limit: "1gb" }));
app.timeout = 0;

const uploadsDir = "uploads";
if (!fs.existsSync(uploadsDir)) fs.mkdirSync(uploadsDir);

// === HISTORIAL DE CONVERSACIÓN ===
let conversationHistory = [
  {
    role: "system",
    content:
      "Eres un asistente masculino virtual llamado Manuel. Hablas español de forma natural, educada y breve. Ayudas al usuario con tono amigable.",
  },
];

// === FUNCIÓN AUXILIAR: combinar WAVs correctamente ===
function combinarWav(archivos, salidaFinal) {
  const buffers = archivos
    .filter(f => fs.existsSync(f))
    .map(f => fs.readFileSync(f));

  if (buffers.length === 0) throw new Error("No hay archivos WAV válidos");

  // Copiar encabezado del primero
  const header = Buffer.alloc(44);
  buffers[0].copy(header, 0, 0, 44);

  // Concatenar datos (sin encabezados)
  const audioData = Buffer.concat(buffers.map(b => b.slice(44)));

  // Actualizar longitudes
  const fileSize = audioData.length + 36;
  const dataSize = audioData.length;
  header.writeUInt32LE(fileSize, 4);
  header.writeUInt32LE(dataSize, 40);

  const output = Buffer.concat([header, audioData]);
  fs.writeFileSync(salidaFinal, output);
}

// === FUNCIÓN PRINCIPAL: generar audio con Coqui (rápida y robusta) ===
async function generarAudioCoqui(textoCompleto) {
  const archivos = [];

  //  Si el texto es corto, una sola llamada
  if (textoCompleto.length < 400) {
    const salida = path.join(uploadsDir, `salida_${Date.now()}.wav`);
    execSync(
      `tts --text "${textoCompleto.replace(/\r?\n|\*/g, " ")}" --out_path "${salida}" --model_name "tts_models/es/css10/vits"`,
      { stdio: "ignore" }
    );
    return salida;
  }

  //  Dividir en bloques de 2–3 frases
  const frases = textoCompleto.match(/[^.!?]+[.!?]?/g) || [textoCompleto];
  const bloques = [];
  for (let i = 0; i < frases.length; i += 3) {
    bloques.push(frases.slice(i, i + 3).join(" ").trim());
  }

  console.log(`Generando ${bloques.length} bloques de audio...`);

  //  Procesar en paralelo 2 a 2 (más rápido)
  const maxConcurrent = 2;
  let i = 0;
  while (i < bloques.length) {
    const batch = bloques.slice(i, i + maxConcurrent);
    await Promise.all(
      batch.map(async (texto, idx) => {
        const nombre = `frag_${Date.now()}_${i + idx}.wav`;
        const salidaFrag = path.join(uploadsDir, nombre);
        try {
          execSync(
            `tts --text "${texto.replace(/\r?\n|\*/g, " ")}" --out_path "${salidaFrag}" --model_name "tts_models/es/css10/vits"`,
            { stdio: "ignore" }
          );
          archivos.push(salidaFrag);
        } catch (err) {
          console.warn(` Error en bloque ${i + idx + 1}:`, err.message);
        }
      })
    );
    i += maxConcurrent;
  }

  if (archivos.length === 0) throw new Error("No se generó audio válido");

  //  Combinar WAVs correctamente
  const salidaFinal = path.join(uploadsDir, `salida_${Date.now()}.wav`);
  combinarWav(archivos, salidaFinal);

  //  Limpiar temporales
  archivos.forEach(f => fs.existsSync(f) && fs.unlinkSync(f));

  return salidaFinal;
}

// === ENDPOINT PRINCIPAL ===
app.post("/api/audio", async (req, res) => {
  try {
    // Limpieza de audios antiguos (>5 min)
    fs.readdirSync(uploadsDir).forEach(file => {
      const filePath = path.join(uploadsDir, file);
      if (file.endsWith(".wav")) {
        const age = Date.now() - fs.statSync(filePath).mtimeMs;
        if (age > 5 * 60 * 1000) fs.unlinkSync(filePath);
      }
    });

    const base64Audio = req.body.audio;
    if (!base64Audio) {
      return res.status(400).json({ error: "No se recibió ningún archivo" });
    }

    // 1️ Guardar el audio recibido
    const buffer = Buffer.from(base64Audio, "base64");
    const wavPath = path.join(uploadsDir, `entrada_user.wav`);
    fs.writeFileSync(wavPath, buffer);

    // 2️ Transcribir con Whisper
    const txtPath = wavPath + ".txt";
    const whisperExe = path.join("whisper-bin", "whisper-cli.exe");
    const modelPath = path.join("whisper.cpp", "models", "ggml-base.bin");

    console.log("Ejecutando Whisper...");
    execSync(`"${whisperExe}" -m "${modelPath}" -f "${wavPath}" --language es -otxt`, {
      stdio: "ignore",
    });
    if (!fs.existsSync(txtPath)) {
      return res.status(500).json({ error: "Whisper no generó transcripción" });
    }

    const transcription = fs.readFileSync(txtPath, "utf8").trim();
    console.log("Transcripción del usuario:", transcription);

    // 3️ Añadir al historial
    conversationHistory.push({ role: "user", content: transcription });

    // 4️ Generar prompt para Ollama
    const promptFile = "temp_prompt.txt";
    fs.writeFileSync(
      promptFile,
      conversationHistory
        .map(m => `${m.role.toUpperCase()}: ${m.content}`)
        .join("\n") + "\nASSISTANT:"
    );

    // 5️ Obtener respuesta de Ollama
    let ollamaResponse = "";
    try {
      console.log("Generando respuesta con Ollama...");
      ollamaResponse = execSync(`ollama run llama3 < ${promptFile}`, {
        encoding: "utf-8",
        maxBuffer: 1024 * 1024 * 50, // 50MB
      }).trim();
    } catch (err) {
      console.error("Error ejecutando Ollama:", err);
      ollamaResponse = "Lo siento, hubo un error al generar la respuesta.";
    }

    console.log("Respuesta del modelo:", ollamaResponse);
    conversationHistory.push({ role: "assistant", content: ollamaResponse });

    // 6️ Generar voz en español
    let salidaAudio = "";
    try {
      salidaAudio = await generarAudioCoqui(ollamaResponse);
    } catch (err) {
      console.error("Error generando audio con Coqui:", err);
    }

    // 7️ Codificar a base64
    let audioBase64 = "";
    if (fs.existsSync(salidaAudio)) {
      const audioBuffer = fs.readFileSync(salidaAudio);
      audioBase64 = audioBuffer.toString("base64");
      setTimeout(() => fs.unlinkSync(salidaAudio), 20000);
    }

    // 8️ Enviar respuesta a Unity
    res.json({
      text: ollamaResponse,
      audio: audioBase64,
    });
  } catch (err) {
    console.error("Error general:", err);
    res.status(500).json({ error: "Error al procesar el audio o generar respuesta" });
  }
});

app.listen(PORT, () => {
  console.log(`Servidor escuchando en http://localhost:${PORT}`);
});
