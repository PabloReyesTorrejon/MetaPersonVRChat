using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Escucha cargas de escena y reproduce un saludo TTS cuando se carga "MainScene".
/// No requiere configuración en el Inspector: busca un componente VoiceChatManager en la escena.
/// </summary>
public class SceneGreetingListener : MonoBehaviour
{
    [Tooltip("Segundos de espera antes de reproducir el saludo (por si la escena aún inicializa objetos)")]
    public float greetingDelay = 1.5f;

    [Tooltip("Texto de saludo que se enviará al TTS")] 
    public string greetingText = "¿En qué te puedo ayudar?";

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Si ya estamos en MainScene (ej. la escena se abre directamente en el editor), lanzamos igualmente
        var active = SceneManager.GetActiveScene();
        if (active.name == "MainScene")
            StartCoroutine(DelayedGreeting());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainScene")
        {
            StartCoroutine(DelayedGreeting());
        }
    }

    // Asegura que exista una instancia del listener incluso si no se añadió manualmente a la escena.
    // Ejecutamos *después* de cargar la escena para evitar crear una instancia antes de que
    // los objetos de la escena estén inicializados (lo que causaba saludos duplicados).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        // Si ya hay un SceneGreetingListener en la escena, no hacemos nada
        var existing = Object.FindObjectsOfType<SceneGreetingListener>();
        if (existing != null && existing.Length > 0)
            return;

        var go = new GameObject("SceneGreetingListener_Auto");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<SceneGreetingListener>();
    }

    IEnumerator DelayedGreeting()
    {
        yield return new WaitForSeconds(greetingDelay);

        // Intentar encontrar VoiceChatManager varias veces por si aún no se ha inicializado
        VoiceChatManager vcm = null;
        int attempts = 6;
        float waitBetween = 0.5f;
        for (int i = 0; i < attempts; i++)
        {
            vcm = FindObjectOfType<VoiceChatManager>();
            if (vcm != null) break;
            yield return new WaitForSeconds(waitBetween);
        }

        if (vcm != null)
        {
            Debug.Log($"SceneGreetingListener: reproduciendo saludo en escena 'MainScene': {greetingText}");
            vcm.PlayGreeting(greetingText);
        }
        else
        {
            Debug.LogWarning("SceneGreetingListener: no se encontró VoiceChatManager en la escena después de varios intentos. Asigna uno o añade un GameObject con VoiceChatManager.");
        }
    }
}
