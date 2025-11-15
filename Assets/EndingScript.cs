using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingScript : MonoBehaviour
{
    [SerializeField] private AudioClip endingMusic;

    private void Start()
    {
        StartCoroutine(PlayMusicAndEndGame());
    }

    private IEnumerator PlayMusicAndEndGame()
    {
        if (endingMusic != null)
        {
            yield return new WaitForSeconds(endingMusic.length);
        }
        else
        {
            Debug.LogWarning("Ending music clip is not assigned.");
        }
        // End the game or transition to the ending scene
        Debug.Log("Game Over. Thank you for playing!");
        SceneManager.LoadScene("MainMenu"); // Replace with your main menu scene name
    }
}
