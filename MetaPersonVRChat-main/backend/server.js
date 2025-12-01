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
      "Eres Luca, un asistente virtual oficial de la Universidad de Cádiz, España. Tu función es ayudar a estudiantes, docentes y público general con información institucional, académica y administrativa. Responde siempre de manera clara, breve y directa, evitando explicaciones largas. Reglas: 1) No inventes información. Si no dispones de datos verificados, responde: 'No dispongo de esa información en este momento' y ofrece consultar una fuente oficial o pedir más contexto. 2) Sé conciso: pocas frases, sin adornos ni párrafos extensos. 3) Mantén un tono profesional, amable y neutro. 4) Prioriza lo esencial: fechas, requisitos, procesos, contactos y pasos concretos. 5) No des consejos personales, legales, médicos o financieros; deriva a la oficina correspondiente. 6) No reveles información privada de ningún miembro de la comunidad. 7) Si la pregunta es ambigua o incompleta, pide aclaración. 8) Cuando la información pueda variar (fechas, costos, normativas), advierte que puede cambiar y recomienda revisar fuentes oficiales. Tu objetivo: respuestas breves, útiles y verificables, sin generar datos inciertos."
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

/**
 * Genera audio TTS con gTTS y opcionalmente ajusta la velocidad con ffmpeg.
 * @param {string} texto
 * @param {string} salidaPath - ruta final del mp3
 * @param {number} speed - factor de velocidad (1.0 = normal). Valores >1 aceleran.
 */
function generarAudioGTTS(texto, salidaPath, speed = 1.10) {
  return new Promise((resolve, reject) => {
    // Guardamos primero en un archivo temporal
    const tmpName = `tmp_tts_${Date.now()}.mp3`;
    const tmpPath = path.join(uploadsDir, tmpName);

    const tts = new gTTS(texto, "es");
    tts.save(tmpPath, async err => {
      if (err) {
        return reject(err);
      }

      // Si speed es ~1.0, simplemente renombramos/movemos el archivo temporal
      if (!speed || Math.abs(speed - 1.0) < 0.001) {
        try {
          fs.renameSync(tmpPath, salidaPath);
          return resolve();
        } catch (e) {
          return reject(e);
        }
      }

      // Construir filtro atempo: each atempo supports [0.5,2.0], chain if needed
      // Decompose speed into multipliers between 0.5 and 2.0
      const factors = [];
      let remaining = speed;
      while (remaining > 2.0001) {
        factors.push(2.0);
        remaining /= 2.0;
      }
      while (remaining < 0.4999) {
        factors.push(0.5);
        remaining /= 0.5;
      }
      // push the final remaining (between 0.5 and 2.0)
      factors.push(remaining);

      const filter = factors.map(f => `atempo=${f.toFixed(3)}`).join(',');

      // Ejecutar ffmpeg para ajustar tempo sin cambiar el pitch
      const cmd = `ffmpeg -y -i "${tmpPath}" -filter:a "${filter}" -acodec libmp3lame -q:a 2 "${salidaPath}"`;

      try {
        execSync(cmd, { stdio: 'ignore' });
        // borrar temporal
        try { fs.unlinkSync(tmpPath); } catch (e) { /* ignore */ }
        return resolve();
      } catch (e) {
        // en caso de fallo con ffmpeg, intentar devolver el original
        try {
          fs.renameSync(tmpPath, salidaPath);
          return resolve();
        } catch (e2) {
          return reject(e2);
        }
      }
    });
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
  console.log(`🚀 Servidor escuchando en https://adan-cofferlike-cris.ngrok-free.dev`)
);
