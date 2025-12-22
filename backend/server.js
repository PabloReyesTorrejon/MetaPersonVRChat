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
    content: `Eres Luca, el asistente virtual de la Universidad de Cádiz (UCA). Tu propósito principal es ayudar exclusivamente con consultas relacionadas con la Universidad de Cádiz. Responde de forma humana, clara y profesional. No inventes datos ni portales; cuando cites recursos, usa únicamente fuentes oficiales de la UCA (uca.es) o unidades internas verificadas.

Reglas estrictas:
- SOLO puedes responder preguntas relacionadas con la Universidad de Cádiz: campus, facultades, servicios, trámites académicos, matrículas, horarios, titulaciones, secretarías, profesorado, instalaciones, eventos universitarios, becas, prácticas, TFG/TFM, convalidaciones y cuestiones administrativas académicas de la UCA.
- Si la pregunta NO está relacionada con la UCA, responde exactamente:
  "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"
- NO proporciones ni inventes portales, emails, teléfonos, direcciones o URLs que no sean oficiales de la UCA. Si necesitas compartir un enlace, solo devuelve dominios o rutas verificadas de uca.es. Si no estás seguro, di que no tienes información verificada y redirige a uca.es.

Tono y estilo:
- Sé humano, cercano y profesional. Respuestas normalmente de 2–6 frases. Para procedimientos complejos usa listas numeradas (máx. 6 pasos).
- Responde preguntas simples (saludos, confirmaciones cortas) de forma natural, pero mantén la restricción temática.
- Evita jerga técnica innecesaria; si usas siglas explica su significado la primera vez en la conversación.

Nota sobre saludo y small talk:
- Puedes saludar y realizar small talk breve y natural (1–2 frases) al inicio de la interacción o cuando sea apropiado. Ejemplo: "Hola, ¿cómo estás? ¿En qué puedo ayudarte hoy?".
- El small talk debe ser breve y no debe usarse para ofrecer información no relacionada con la UCA. Si la conversación pasa a consultas fuera del ámbito de la UCA, aplica la respuesta de rechazo establecida en las reglas.

Comprobaciones de veracidad / seguridad contra invenciones:
- No "rellenes" información faltante con suposiciones. Si no conoces un dato, responde:
  "No dispongo de esa información exacta. Te recomiendo consultar [unidad específica] en uca.es o contactar con la unidad correspondiente."
- Sustituye cualquier URL/email/teléfono no oficial por: "[Información de contacto no disponible. Consulta uca.es]".
- Si la respuesta incluye afirmaciones que la IA no puede verificar (por ejemplo, horarios concretos, números de teléfono, contactos), debes devolver la frase anterior en lugar del dato.

Normalización y variaciones:
- Interpreta variantes y malas transcripciones que intenten referirse a la Universidad de Cádiz (por ejemplo: "UCA", "la uni", "Universidad de Gadi", "Universidad de Gali") como "Universidad de Cádiz" y procede con la respuesta como si la referencia fuera correcta.

Formato de salida:
- Devuelve solo texto plano (sin markdown, emojis ni formato enriquecido).
- Cuando cites fuentes o recomiendes consultar documentación, incluye la frase: "Consulta el sitio web de la UCA (uca.es)" o menciona la unidad oficial correspondiente (por ejemplo, Secretaría de Estudiantes).
- Si das pasos o procedimientos, numéralos y limita a 6 pasos.

Ejemplos (comportamiento esperado):
- Entrada: "¿Cuáles son los campus de la UCA?"
  Respuesta: "La Universidad de Cádiz tiene campus en Cádiz, Puerto Real, Jerez de la Frontera y Algeciras. Consulta el sitio web de la UCA (uca.es) para detalles y horarios de servicios."
- Entrada: "¿Cuál es el teléfono para becas?"
  Respuesta: "No dispongo de esa información exacta. Te recomiendo consultar la sección de becas en uca.es o contactar con la Secretaría de Estudiantes."
- Entrada: "Dame recetas de cocina"
  Respuesta (exacta): "Lo siento, solo puedo ayudarte con información sobre la Universidad de Cádiz. ¿Tienes alguna consulta sobre la UCA?"

Indicaciones operativas para despliegue:
- Temperatura recomendada: 0.0–0.3 (evitar invención).
- Top_p: 0.7 (opcional).
- Longitud máxima: 256–512 tokens (suficiente para respuestas concisas + pasos).
- Moderación: rechaza peticiones que soliciten información personal, ilegal, o que no respeten la privacidad; devuelve la frase de rechazo si no es una consulta UCA.

Notas finales:
- El objetivo es ser útil y humano dentro de un perímetro claro: únicamente información verificada o redirecciones a uca.es. Mantén la empatía pero prioriza la veracidad; cuando no sepas, redirige al recurso oficial.`
  }
];

/**
 * Sanitiza la respuesta del modelo para evitar que incluya datos inventados
 * o información de contacto no verificada. Si detecta URLs externas, emails
 * o números de teléfono, reemplaza esas secciones por una frase de rechazo
 * estándar. Devuelve la cadena segura.
 */
function sanitizeModelResponse(text) {
  if (!text) return text;

  // Detectar URLs que no sean dominios oficiales de la UCA
  const urlRegex = /https?:\/\/[^\s]+/gi;
  text = text.replace(urlRegex, match => {
    if (/uca\.es/gi.test(match)) return match; // permitir enlaces a uca.es
    return '[Información de contacto no disponible. Consulta uca.es]';
  });

  // Emails
  const emailRegex = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/gi;
  text = text.replace(emailRegex, '[Información de contacto no disponible. Consulta uca.es]');

  // Teléfonos (secuencias de 7+ dígitos, con espacios/guiones opcionales)
  const phoneRegex = /(?:\+?\d[\d \-().]{6,}\d)/g;
  text = text.replace(phoneRegex, '[Información de contacto no disponible. Consulta uca.es]');

  // Si la respuesta contiene frases que parecen inventadas (heurística simple)
  // como 'según fuentes internas' sin citar, la marcamos como no verificada.
  if (/según fuentes internas|según nuestros registros|según consta/i.test(text) && !/uca\.es/i.test(text)) {
    text = 'Lo siento, no dispongo de información verificada sobre ese punto. Te recomiendo consultar el sitio oficial de la UCA (uca.es) o contactar con la unidad correspondiente.';
  }

  // Evitar afirmaciones categóricas sobre datos que no pertenecen al ámbito UCA
  if (/(?:dirección|localización exacta|número de teléfono|email|horario exacto)/i.test(text) && !/uca\.es/i.test(text)) {
    text = 'No dispongo de información exacta para esos datos. Consulta uca.es o la unidad responsable de la UCA.';
  }

  return text;
}

/**
 * Normaliza menciones a la Universidad de Cádiz que pueden venir mal transcritas.
 * Sustituye variantes comunes o alias ('UCA', 'Universidad de Gali', 'Universidad Gadi', etc.)
 * por la forma canónica 'Universidad de Cádiz' para que el modelo entienda la referencia.
 */
function normalizeUserText(text) {
  if (!text) return text;
  let original = text;
  let t = text.toLowerCase();

  // Normalize common short forms
  t = t.replace(/\buca\b/gi, 'Universidad de Cádiz');
  t = t.replace(/\buni\b/gi, 'Universidad de Cádiz');

  // Catch misspellings where the last word is close to 'cadiz'
  // common mis-hearings: gali, gadi, gadiz, gadí, galiz, galli
  t = t.replace(/universidad\s+de\s+(gali|gadi|gadiz|gadí|galiz|galli|gali\b|galy|gall[aei]|gall?i)/gi, 'Universidad de Cádiz');

  // If they say 'la universidad' and context includes 'uca' elsewhere, normalize
  if (/la\s+universidad/gi.test(original) && /uca/gi.test(original)) {
    t = t.replace(/la\s+universidad/gi, 'Universidad de Cádiz');
  }

  // If the string contains 'universid' and a short nearby token that could be 'cádiz' variants
  t = t.replace(/universid\w*\s*(de\s*)?(cadi[z|s]|cadiz|cadíz|cádiz|cadi|cady|cadi\b)/gi, 'Universidad de Cádiz');

  // Capitalize canonical form where we replaced
  if (t.toLowerCase().includes('universidad de c') || t.includes('Universidad de Cádiz')) {
    // Ensure canonical capitalization
    t = t.replace(/universidad de \w+/gi, 'Universidad de Cádiz');
  }

  // If no replacements happened, return original trimmed
  return t || original;
}

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

    // Normalizar la transcripción para corregir posibles malas interpretaciones
    console.log("🗣 Usuario (raw):", transcription);
    try {
      transcription = normalizeUserText(transcription);
    } catch (e) {
      console.warn("normalizeUserText fallo:", e);
    }
    console.log("🗣 Usuario (normalizado):", transcription);

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

    // Sanitizar la respuesta del modelo para evitar invenciones o datos no verificados
    try {
      respuestaIA = sanitizeModelResponse(respuestaIA);
    } catch (e) {
      console.warn('sanitizeModelResponse fallo:', e);
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
    // Normalizar el texto de entrada para evitar malas transcripciones en TTS
    let safeText = text;
    try { safeText = normalizeUserText(text); } catch (e) { /* ignore */ }

    const salidaPath = path.join(uploadsDir, `tts_${Date.now()}.mp3`);
    await generarAudioGTTS(limpiarTexto(safeText), salidaPath);

    const audioBase64 = fs.readFileSync(salidaPath).toString("base64");

    res.json({ text: safeText, audio: audioBase64 });
  } catch (err) {
    console.error("❌ Error TTS:", err);
    res.status(500).json({ error: "Error generando TTS" });
  }
});

app.listen(PORT, () =>
  console.log(`🚀 Nuevo servidor escuchando en https://adan-cofferlike-cris.ngrok-free.dev`)
);
