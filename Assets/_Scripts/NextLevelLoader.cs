using System.Collections;
using UnityEngine;

public class NextLevelLoader : MonoBehaviour
{
    [SerializeField] private string nextLevelName;
    private Coroutine loadCoroutine;
    private Animator animator;
    [SerializeField]
    private AudioClip doorCloseSfx;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (loadCoroutine == null)
            {
                loadCoroutine = StartCoroutine(LoadNextLevel());
            }
        }
    }
    
    private IEnumerator LoadNextLevel()
    {
        if (animator != null)
        {
            animator.SetTrigger("close");
        }
        if (doorCloseSfx != null)
        {
            AudioSource.PlayClipAtPoint(doorCloseSfx, transform.position);
        }
        // Optional: Add fade-out or loading screen here
        yield return new WaitForSeconds(3f); // Simulate loading delay
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextLevelName);
    }
    
}
