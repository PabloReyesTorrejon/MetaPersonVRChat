using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisemePlayer : MonoBehaviour
{
    public SpriteRenderer mouthRenderer;

    public List<string> visemeKeys;
    public List<Sprite> visemeSprites;

    public float visemeSpeed = 0.06f;

    private Dictionary<string, Sprite> dict;
    private Coroutine routine;

    void Start()
    {
        dict = new Dictionary<string, Sprite>();
        for (int i = 0; i < visemeKeys.Count; i++)
        {
            if (i < visemeSprites.Count)
                dict[visemeKeys[i]] = visemeSprites[i];
        }
    }

    public void PlayText(string text)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(Lipsync(text));
    }

    IEnumerator Lipsync(string text)
    {
        text = text.ToLower();

        foreach (char c in text)
        {
            string v = CharToViseme(c);

            if (dict.ContainsKey(v))
                mouthRenderer.sprite = dict[v];

            yield return new WaitForSeconds(visemeSpeed);
        }

        // Boca en reposo
        if (dict.ContainsKey("rest"))
            mouthRenderer.sprite = dict["rest"];
    }

    private string CharToViseme(char c)
    {
        if ("a".Contains(c)) return "a";
        if ("e".Contains(c)) return "e";
        if ("i".Contains(c)) return "i";
        if ("o".Contains(c)) return "o";
        if ("u".Contains(c)) return "u";

        return "rest";
    }
}
