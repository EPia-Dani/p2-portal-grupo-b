using _Scripts.Interfaces;
using UnityEngine;

public class DoorBase : MonoBehaviour, IDoor
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private AudioSource doorAudioSource;
    [SerializeField] private AudioClip audioClip;
    [SerializeField] private bool needsKey;

    public bool NeedsKey => needsKey;
    public void Open()
    {
        if (doorAnimator != null && !needsKey)
        {
            doorAnimator.SetTrigger("Open");
            doorAudioSource?.PlayOneShot(audioClip);
        }
    }

    public void Close()
    {
        if (doorAnimator != null && !needsKey)
        {
            doorAnimator.SetTrigger("Close");
            doorAudioSource?.PlayOneShot(audioClip);
        }
    }

    public void Unlock()
    {
        needsKey = false;
    }
}
