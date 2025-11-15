using System;
using UnityEngine;

public class MainMenu : MonoBehaviour
{
    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void NewGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("_Scenes/TestChamber1");
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}
