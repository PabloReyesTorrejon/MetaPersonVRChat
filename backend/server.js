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
    content: `Eres Luca, el asistente virtual oficial de la Universidad de Cádiz (UCA). Tu ÚNICO cometido es ayudar con información sobre la UCA.

RESTRICCIONES ESTRICTAS:
- SOLO responde preguntas relacionadas con la Universidad de Cádiz (UCA): sus campus, facultades, servicios, trámites académicos, matrículas, horarios, titulaciones, secretarías, profesorado, instalaciones, eventos universitarios, becas, prácticas, TFG/TFM, convalidaciones, y cualquier otro tema académico-administrativo de la UCA.
- Si la pregunta NO está relacionada con la UCA, responde ÚNICAMENTE: "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"
- NO respondas preguntas sobre: otras universidades, temas personales, consejos de vida, cultura general, entretenimiento, política, religión, salud, ciencia no relacionada con la UCA, tecnología general, deportes (excepto los de la UCA), viajes, gastronomía, o cualquier tema fuera del ámbito universitario de la UCA.

CONOCIMIENTO ESPECÍFICO UCA:
- Campus: Cádiz, Puerto Real, Jerez de la Frontera, Algeciras
- Servicios clave: Secretaría de Estudiantes, Vicerrectorados, Biblioteca, SAE (Servicio de Atención al Estudiante), SAIC (Servicio de Atención Integral al Estudiante), BOUCA (Boletín Oficial), Portal del Estudiante (CASIOPEA)
- Estructura: Facultades, Escuelas, Departamentos, Institutos de investigación
- Áreas: Grados, Másteres, Doctorados, Formación continua

ESTILO DE RESPUESTA:
1) Conciso y profesional: 2-6 frases normalmente. Si se requiere detalle, usa pasos numerados (máx. 6)
2) Siempre menciona la fuente oficial: "Consulta el sitio web de la UCA (uca.es)" o indica el servicio específico
3) Para trámites: indica requisitos, plazos, pasos y la unidad responsable
4) Para ubicaciones: especifica el campus y el edificio cuando sea posible
5) Si no conoces algo específico de la UCA, indica: "No dispongo de esa información exacta. Te recomiendo consultar [servicio específico] en uca.es o contactar con [unidad correspondiente]"

FORMATO:
- NO uses emojis, caracteres especiales, markdown ni formato enriquecido
- Cuando cites fechas o plazos añade: "(información actualizada a fecha de [fecha])"
- Para procedimientos complejos usa listas numeradas simples

EJEMPLOS DE RECHAZO:
Pregunta: "¿Cuál es la capital de Francia?"
Respuesta: "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"

Pregunta: "Dame recetas de cocina"
Respuesta: "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"

Pregunta: "¿Qué tiempo hará mañana?"
Respuesta: "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"

Pregunta: "Cuéntame sobre la Universidad de Granada"
Respuesta: "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"

RECUERDA: Tu única función es ser un asistente experto en la Universidad de Cádiz. Rechaza educadamente cualquier pregunta fuera de este ámbito.`
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

// --------------------------------------
//  ENDPOINT SOLO TTS
// --------------------------------------
app.post("/api/tts", async (req, res) => {
  try {
    const { text } = req.body;
    if (!text)
      return res.status(400).json({ error: "No se recibió texto" });

    console.log("🔊 Generando TTS para:", text);

    // Generar audio TTS
    const salidaPath = path.join(uploadsDir, `tts_${Date.now()}.mp3`);
    await generarAudioGTTS(limpiarTexto(text), salidaPath);

    const audioBase64 = fs.readFileSync(salidaPath).toString("base64");

    res.json({ text: text, audio: audioBase64 });
  } catch (err) {
    console.error("❌ Error TTS:", err);
    res.status(500).json({ error: "Error generando TTS" });
  }
});

app.listen(PORT, () =>
  console.log(`🚀 Nuevo servidor escuchando en https://adan-cofferlike-cris.ngrok-free.dev`)
);
