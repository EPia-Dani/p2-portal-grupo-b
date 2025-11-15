using System.Collections;
using System.Collections.Generic;
using _Scripts.Interfaces;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class Button : MonoBehaviour, IGladosNotifier
{

    [SerializeField]
    private Animator animator;
    
    [SerializeField]
    private float pressDuration = 0.4f;
    
    [SerializeField]
    private GameObject gladosObject;
    private IGlados glados;
    
    private bool notifiedGlados = false;
    
    [SerializeField]
    private MonoBehaviour target;
    private IButtonAction action;
    
    private float state = 0f;
    
    private HashSet<Collider> inside = new ();
    
    private Coroutine pressCoroutine = null;
    
    private void Awake()
    {
        if (target is IButtonAction action)
        {
            this.action = action;
        }
        else
        {
            Debug.LogWarning("Button target does not implement IButtonAction interface.", target);
        }
    }
    
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Entered by: " + other.name);
        if (other != null)
        {
            inside.Add(other);
            if (pressCoroutine != null)
                StopCoroutine(pressCoroutine);
            pressCoroutine = StartCoroutine(PressState(true));
            if (!notifiedGlados && gladosObject != null)
            {
                glados = gladosObject.GetComponent<IGlados>();
                if (glados != null)
                {
                    notifiedGlados = true;
                    NotifyGlados();
                }
            }
        }
        if (inside.Count == 1)
        {
            action?.OnButtonPressed();
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
                action?.OnButtonReleased();
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
            animator?.SetFloat("PressState", state);
            yield return null;
        }

        state = endState;
        animator?.SetFloat("PressState", state);
        pressCoroutine = null;
    }


    public void NotifyGlados()
    {
        glados?.StartNextSequence();
    }
}
