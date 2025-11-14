using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.RefractionCube
{
    public class LaserButton : MonoBehaviour, ILaserReceiver
    {
        [SerializeField] private MonoBehaviour target; // must implement IButtonAction
        private IButtonAction action;
        
        private int lastHitFrame = -1;
        private bool isPressed = false;
        
        private void Awake()
        {
            if (target is IButtonAction action)
            {
                this.action = action;
            }
            else
            {
                Debug.LogWarning("LaserButton target does not implement IButtonAction interface.", target);
            }
        }

        void LateUpdate()
        {
            bool shouldBePressed = (lastHitFrame == Time.frameCount);

            if (!isPressed && shouldBePressed)
            {
                action?.OnButtonPressed();
                isPressed = true;
            }
            else if (isPressed && !shouldBePressed)
            {
                action?.OnButtonReleased();
                isPressed = false;
            }
            else
            {
                isPressed = shouldBePressed;
                if (!isPressed)
                    action?.OnButtonReleased();
                else
                {
                    action?.OnButtonPressed();
                }
            }
        }

        public void LaserHit(Vector3 point, Vector3 normal, int frame)
        {
            if (frame != lastHitFrame)
            {
                lastHitFrame = frame;
                if (!isPressed)
                {
                    isPressed = true;
                    action?.OnButtonPressed();
                }
            }
        }
    }
}