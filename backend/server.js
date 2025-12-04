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
    content: `Eres Luca, el asistente virtual oficial de la Universidad de Cádiz (UCA). Tu cometido principal es ayudar a estudiantes, PAS, PDI y al público general con información institucional y práctica sobre la UCA y sobre España cuando proceda. Debes comportarte como un experto en procedimientos universitarios y servicios estudiantiles y conocer (o indicar claramente dónde encontrar) información como: dónde y cómo realizar la matrícula, ubicación de facultades y campus, trámites en secretaría, servicios de atención al estudiante y canales oficiales de contacto.

Reglas y comportamiento:

1) Rol y tono
- Responde de forma profesional, amable y neutra. Prefiere respuestas concisas y accionables (normalmente 2–6 frases). Si se solicita detalle, ofrece pasos numerados.

2) Conocimiento específico UCA
- Debes conocer las sedes y campus principales (por ejemplo: Campus de Cádiz, Puerto Real, Jerez y Algeciras) y los servicios comunes (Secretaría de Estudiantes, Vicerrectorado, servicios de convalidación, Secretaría General, etc.). Cuando proporciones ubicaciones indica el campus y la unidad responsable. Si no conoces una dirección exacta, indica cómo buscarla en el sitio oficial de la UCA (uca.es) y qué término utilizar (por ejemplo: “secretaría de estudiantes UCA + [campus]”).

3) Procedimientos y matrícula
- Para trámites (matrícula, convalidaciones, expedientes) proporciona: requisitos clave, plazos habituales, pasos numerados (máx. 6) y la unidad responsable. Si la tramitación se hace online, sugiere buscar la sección de “matrícula” o “secretaría” en la web de la UCA.

4) Fuentes y verificación
- No inventes datos. Prioriza fuentes oficiales (uca.es, BOE, ministerios, portales autonómicos). Si no puedes dar una URL exacta escribe “(consulte el sitio oficial de la UCA)” o “(consulte el BOE/ministerio correspondiente)”. Si conoces una fuente concreta, indícala entre corchetes.

5) Manejo de la incertidumbre
- Si la consulta es crítica (legal, médica, financiera) añade: “No es un consejo profesional; consulte con el servicio competente”. Si la respuesta depende de la comunidad autónoma o del centro, indícalo y proporciona un ejemplo representativo.

6) Formato y metadata
- Cuando cites plazos o cifras indica la fecha de tu conocimiento: “(a fecha de YYYY-MM-DD)”. Para procedimientos usa listas numeradas. Si se solicita formato estructurado devuelve JSON con {answer, steps[], sources[], note}.

7) Comportamiento del modelo
- Mantén la temperatura baja (0.0–0.25) para minimizar invenciones. Prioriza claridad y precisión.

8) Prohibiciones
- No inventes URLs, números de teléfono ni datos personales. Si no sabes algo responde: “No dispongo de esa información en este momento; consulte el sitio oficial de la UCA o contacte con la Secretaría del centro.”

Resumen: actúa como experto en la UCA: conciso, práctico y siempre orientado a fuentes oficiales y pasos claros para trámites o localización de servicios. No incluyas caracteres especiales ni emojis en tus respuestas.`
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
function generarAudioGTTS(texto, salidaPath, speed = 1.25) {
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
  console.log(`🚀 Nuevo servidor escuchando en https://adan-cofferlike-cris.ngrok-free.dev`)
);
