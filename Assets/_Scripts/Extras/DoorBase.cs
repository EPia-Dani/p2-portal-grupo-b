using System.Collections;
using _Scripts.Interfaces;
using UnityEngine;

public class DoorBase : MonoBehaviour, IButtonAction
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private AudioClip audioClip;
    
    [SerializeField]
    private float openDuration = 0.5f;
    
    private float _state = 0f;
    
    private Coroutine _openCoroutine = null;
    
    public void OnButtonPressed()
    {
        if (doorAnimator != null)
        {
            if (_openCoroutine != null)
                StopCoroutine(_openCoroutine);
            _openCoroutine = StartCoroutine(OpenState(true));
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }
    }

    public void OnButtonReleased()
    {
        if (doorAnimator != null)
        {
            if (_openCoroutine != null)
                StopCoroutine(_openCoroutine);
            _openCoroutine = StartCoroutine(OpenState(false));
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }
    }
    
    private IEnumerator OpenState(bool isPressed)
    {
        float startState = _state;
        float endState = isPressed ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            _state = Mathf.Lerp(startState, endState, elapsed / openDuration);
            doorAnimator.SetFloat("OpenState", _state);
            yield return null;
        }

        _state = endState;
        doorAnimator.SetFloat("OpenState", _state);
        _openCoroutine = null;
    }
}
