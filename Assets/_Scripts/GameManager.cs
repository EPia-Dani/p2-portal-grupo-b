using System;
using System.Collections;
using _Scripts.Player.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private bool playerIsDead = false;
    
    private int restartCount = 0;
    [SerializeField] private AudioClip giveUpClip;
    
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        if (restartCount >= 3)
        {
            AudioSource.PlayClipAtPoint(giveUpClip, Vector3.zero);
            Debug.Log("Player should take a break! Too many restarts.");
            restartCount = 0; // Reset count after giving up
        }
    }

    public void PlayerDied()
    {
        Debug.Log("Player has died. Restarting level...");
        if (!playerIsDead)
        {
            playerIsDead = true;
            StartCoroutine(LevelRestartCoroutine());
        }
    }
    
    IEnumerator LevelRestartCoroutine()
    {
        restartCount++;
        yield return new WaitForSeconds(2f); // Optional delay before restart
        playerIsDead = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
}
