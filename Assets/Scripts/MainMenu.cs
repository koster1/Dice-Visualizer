using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
    public void StartApplication()
    {
        SceneManager.LoadScene("Game");
    }

    public void QuitApplication()
    {
        Application.Quit();

        // toimii Editorissa
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

    