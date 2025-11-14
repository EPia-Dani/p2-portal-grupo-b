using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void NewGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("_Scenes/TestChamber1");
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}
