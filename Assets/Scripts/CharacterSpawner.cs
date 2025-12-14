using UnityEngine;
using System.Collections;

public class CharacterSpawner : MonoBehaviour
{
    public GameObject[] characters;
    public Transform spawnPoint;
    const string PLAYERPREF_KEY = "SelectedCharacter";

    [Header("Saludo inicial")]
    public VoiceChatManager voiceChatManager;
    public float greetingDelay = 1.0f;

    void Start()
    {
        // Depuración: cuántas instancias hay
        int instances = FindObjectsOfType<CharacterSpawner>().Length;
        if (instances > 1) Debug.LogWarning($"CharacterSpawner: hay {instances} instancias en la escena.");

        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning("CharacterSpawner: no hay characters asignados en el array. Asigna los GameObjects en el Inspector.");
            return;
        }

        bool hasKey = PlayerPrefs.HasKey(PLAYERPREF_KEY);
        int index = hasKey ? PlayerPrefs.GetInt(PLAYERPREF_KEY, 0) : 0;
        Debug.Log($"CharacterSpawner: HasKey={hasKey} -> índice leído = {index}");

        if (index < 0 || index >= characters.Length)
        {
            Debug.LogWarning($"CharacterSpawner: índice {index} fuera de rango. Se usará 0.");
            index = 0;
        }

        for (int i = 0; i < characters.Length; i++)
        {
            GameObject go = characters[i];
            if (go == null)
            {
                Debug.LogWarning($"CharacterSpawner: characters[{i}] es null.");
                continue;
            }

            bool shouldBeActive = (i == index);
            go.SetActive(shouldBeActive);
            Debug.Log($"CharacterSpawner: '{go.name}' active = {shouldBeActive}");

            if (shouldBeActive && spawnPoint != null)
            {
                go.transform.position = spawnPoint.position;
                go.transform.rotation = spawnPoint.rotation;
            }

            var animators = go.GetComponentsInChildren<Animator>(true);
            foreach (var a in animators) a.enabled = shouldBeActive;
        }

        // Reproducir saludo inicial después de un pequeño delay
        StartCoroutine(PlayGreetingAfterDelay());
    }

    IEnumerator PlayGreetingAfterDelay()
    {
        yield return new WaitForSeconds(greetingDelay);

        if (voiceChatManager != null)
        {
            voiceChatManager.PlayGreeting("en qué te puedo ayudar?");
        }
        else
        {
            Debug.LogWarning("CharacterSpawner: VoiceChatManager no asignado. No se puede reproducir el saludo.");
        }
    }
}
