using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class Button : MonoBehaviour
{

    [SerializeField]
    private Animator animator;
    
    [SerializeField]
    private float pressDuration = 0.4f;
    
    private float state = 0f;
    
    private HashSet<Collider> inside = new ();
    
    private Coroutine pressCoroutine = null;
    
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Entered by: " + other.name);
        if (other != null)
        {
            inside.Add(other);
            if (pressCoroutine != null)
                StopCoroutine(pressCoroutine);
            pressCoroutine = StartCoroutine(PressState(true));
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger Exited by: " + other.name);
        if (other != null)
        {
            inside.Remove(other);
            if (inside.Count == 0)
            {
                if (pressCoroutine != null)
                    StopCoroutine(pressCoroutine);
                pressCoroutine = StartCoroutine(PressState(false));
            }
        }
    }
    
    private IEnumerator PressState(bool isPressed)
    {
        float startState = state;
        float endState = isPressed ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            state = Mathf.Lerp(startState, endState, elapsed / pressDuration);
            animator.SetFloat("PressState", state);
            yield return null;
        }

        state = endState;
        animator.SetFloat("PressState", state);
        pressCoroutine = null;
    }
    
    
}
