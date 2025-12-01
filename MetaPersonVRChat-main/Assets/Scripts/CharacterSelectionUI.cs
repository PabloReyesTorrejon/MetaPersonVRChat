// ...existing code...
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    const string PLAYERPREF_KEY = "SelectedCharacter";

    public Image characterPreview;
    public Sprite[] characterSprites;

    // Métodos para asignar directamente a los dos botones (uno por personaje)
    public void SelectCharacter0() { SelectAndLoad(0); }
    public void SelectCharacter1() { SelectAndLoad(1); }

    // Alternativa genérica (puedes pasar 0 o 1 desde el Inspector)
    public void SelectCharacter(int index) { SelectAndLoad(index); }

    void SelectAndLoad(int index)
    {
        Debug.Log($"CharacterSelectionUI: seleccionando índice {index}");
        PlayerPrefs.SetInt(PLAYERPREF_KEY, index);
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainScene");
    }

    // Opcional: mostrar vista previa si lo necesitas
    public void ShowPreview(int index)
    {
        if (characterPreview == null || characterSprites == null) return;
        if (index < 0 || index >= characterSprites.Length) return;
        characterPreview.sprite = characterSprites[index];
    }
}
