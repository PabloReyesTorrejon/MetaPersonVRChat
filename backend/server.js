// backend/server.js
import express from "express";
import fs from "fs";
import path from "path";
import { execSync } from "child_process";
import fetch from "node-fetch";
import gTTS from "gtts";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const app = express();
const PORT = 3000;
const GENIALLE_API = process.env.GENIALLE_API || "";

app.use(express.json({ limit: "1gb" }));

const uploadsDir = path.join(__dirname, "uploads");
if (!fs.existsSync(uploadsDir)) fs.mkdirSync(uploadsDir);

// Conversación
let conversationHistory = [
  {
    role: "system",
    content:
      "Eres Luca, un asistente oficial, informativo y cordial de la Universidad de Cádiz (España). Tu función es ayudar a estudiantes, personal y público general con información clara, útil, verificada y contextualizada."
  }
];

function limpiarTexto(texto) {
  if (!texto) return "";

  return texto
    .normalize("NFKD")                       // Normaliza caracteres especiales
    //.replace(/[^\w\sáéíóúüñ¡!¿?.,;:()]/gi, "") // Elimina caracteres no permitidos
    .replace(/[_*~`>#\[\]\{\}\(\)-]/g, "")     // Quita markdown y símbolos
    .replace(/\s{2,}/g, " ")                   // Reemplaza múltiples espacios
    .trim();
}


async function generarRespuestaGenialle(history) {
  const resp = await fetch(GENIALLE_API, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ model: "llama3.1:8b", messages: history })
  });

  const streamText = await resp.text();
  let final = "";

  streamText.split("\n").filter(Boolean).forEach(line => {
    try {
      const obj = JSON.parse(line);
      if (obj?.message?.content) final += obj.message.content;
    } catch {}
  });

  return final.trim();
}

function generarAudioGTTS(texto, salidaPath) {
  return new Promise((resolve, reject) => {
    const tts = new gTTS(texto, "es");
    tts.save(salidaPath, err => (err ? reject(err) : resolve()));
  });
}

// --------------------------------------
//  ENDPOINT PRINCIPAL
// --------------------------------------
app.post("/api/audio", async (req, res) => {
  try {
    const base64Audio = req.body.audio;
    if (!base64Audio)
      return res.status(400).json({ error: "No se recibió audio" });

    const wavPath = path.join(uploadsDir, `entrada_${Date.now()}.wav`);
    fs.writeFileSync(wavPath, Buffer.from(base64Audio, "base64"));

    // -------------------------
    // 🎙  WHISPER (PYTHON)
    // -------------------------
    console.log("🎙 Ejecutando Whisper...");

    let transcription = "";
    try {
      const output = execSync(
        `/venv/bin/python /app/transcribe.py "${wavPath}"`,
        { encoding: "utf-8" }
      );
      transcription = output.trim();
    } catch (err) {
      console.error("❌ Error ejecutando Whisper:", err);
      return res.status(500).json({ error: "Error en transcripción" });
    }

    console.log("🗣 Usuario:", transcription);
    conversationHistory.push({ role: "user", content: transcription });

    // -------------------------
    // 🧠 GENIALLE
    // -------------------------
    console.log("🧠 Solicitando a Genialle...");
    let respuestaIA = "";
    try {
      respuestaIA = await generarRespuestaGenialle(conversationHistory);
    } catch (err) {
      console.error("❌ Error Genialle:", err);
      respuestaIA = "Lo siento, hubo un error generando la respuesta.";
    }

    conversationHistory.push({ role: "assistant", content: respuestaIA });

    // -------------------------
    // 🔊 TTS
    // -------------------------
    const salidaPath = path.join(uploadsDir, `respuesta_${Date.now()}.mp3`);
    await generarAudioGTTS(limpiarTexto(respuestaIA), salidaPath);

    const audioBase64 = fs.readFileSync(salidaPath).toString("base64");

    res.json({ text: respuestaIA, audio: audioBase64 });
  } catch (err) {
    console.error("❌ Error general:", err);
    res.status(500).json({ error: "Error interno del servidor" });
  }
});

app.listen(PORT, () =>
  console.log(`🚀 Nuevo servidor escuchando en https://adan-cofferlike-cris.ngrok-free.dev`)
);
