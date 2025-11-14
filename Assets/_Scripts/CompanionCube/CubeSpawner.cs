// CubeSpawner.cs

using System;
using System.Collections;
using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.CompanionCube
{
    [DisallowMultipleComponent]
    public class CubeSpawner : MonoBehaviour, IButtonAction
    {
        [Header("Animation & SFX")]
        [SerializeField] private Animator animator;
        [SerializeField] private AudioClip audioClip;
        [SerializeField] private float transitionDuration = 0.5f;

        [Header("Spawn")]
        [SerializeField] private Transform spawnPoint;        // where the cube appears
        [SerializeField] private GameObject cubePrefab;       // the cube to spawn
        [SerializeField] private Collider spawnVolume;        // area that must be empty to allow spawn
        [SerializeField] private LayerMask blockMask = ~0;    // layers considered "occupied"
        [SerializeField] private bool alignToSpawnRotation = true;

        private float state = 0f;       // 0 = closed, 1 = open
        private Coroutine tween;
    
        public void OnButtonPressed()
        {
            if (tween != null) StopCoroutine(tween);
            tween = StartCoroutine(TweenState(1f));
            if (audioClip) AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }

        public void OnButtonReleased()
        {
            if (tween != null) StopCoroutine(tween);
            tween = StartCoroutine(TweenState(0f)); // spawn check happens when we fully close
            if (audioClip) AudioSource.PlayClipAtPoint(audioClip, transform.position);
        }

        private void Start()
        {
            TrySpawnCube();
        }

        IEnumerator TweenState(float target)
        {
            float start = state;
            float t = 0f;

            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                state = Mathf.Lerp(start, target, t / transitionDuration);
                if (animator) animator.SetFloat("OpenState", state);
                yield return null;
            }

            state = target;
            if (animator) animator.SetFloat("OpenState", state);
            tween = null;

            // Spawn only when fully closed
            if (Mathf.Approximately(state, 0f))
                TrySpawnCube();
        }

        void TrySpawnCube()
        {
            if (!cubePrefab || !spawnPoint || !spawnVolume) return;
            Quaternion rotOut = alignToSpawnRotation ? spawnPoint.rotation : Quaternion.identity;
            Instantiate(cubePrefab, spawnPoint.position, rotOut);
        }

        // Gizmos for spawn volume
        void OnDrawGizmosSelected()
        {
            if (!spawnVolume) return;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.identity;

            if (spawnVolume is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
            }
            else if (spawnVolume is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.bounds.center,
                    sphere.radius * Mathf.Max(sphere.transform.lossyScale.x, sphere.transform.lossyScale.y, sphere.transform.lossyScale.z));
            }
            else if (spawnVolume is CapsuleCollider capsule)
            {
                // simple approximation
                Gizmos.DrawWireSphere(capsule.bounds.center, Mathf.Max(capsule.bounds.extents.x, capsule.bounds.extents.y, capsule.bounds.extents.z));
            }
        }

        public void SpawnCube()
        {
            StartCoroutine(OpenAndClose());
        }

        IEnumerator OpenAndClose()
        {
            OnButtonPressed();
            yield return new WaitForSeconds(transitionDuration + 0.2f);
            OnButtonReleased();
        }
    }
}
