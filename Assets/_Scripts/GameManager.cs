using _Scripts.Player.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private bool playerIsDead = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    public void PlayerDied()
    {
        Debug.Log("Player has died. Restarting level...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
