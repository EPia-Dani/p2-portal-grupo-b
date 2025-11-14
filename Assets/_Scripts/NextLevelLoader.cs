using UnityEngine;

public class NextLevelLoader : MonoBehaviour
{
    [SerializeField] private string nextLevelName;
    private Coroutine loadCoroutine;
    private Animator animator;
    
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
    
    private System.Collections.IEnumerator LoadNextLevel()
    {
        if (animator != null)
        {
            animator.SetTrigger("close");
        }
        // Optional: Add fade-out or loading screen here
        yield return new WaitForSeconds(3f); // Simulate loading delay
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextLevelName);
    }
    
}
