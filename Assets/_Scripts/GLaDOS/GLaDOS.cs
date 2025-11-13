using System;
using _Scripts.Player.Runtime;
using UnityEngine;

namespace _Scripts.GLaDOS
{
    /// <summary>
    /// GLaDOS AI Core Script that will work as the Game Manager
    /// </summary>
    public class GLaDOS : MonoBehaviour
    {
        [SerializeField] private GameObject player;
        [SerializeField] private Transform initialPlayerSpawn;
        [SerializeField] private AudioClip[] gladosLines;
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = new AudioSource();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D sound
            _audioSource.volume = 1f;
            _audioSource.loop = false;
            gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            RespawnPlayer();
            Debug.Log("GLaDOS Initialized. Playing Intro Line.");
            PlayGLaDOSLine(0);
        }
        
        public void RespawnPlayer()
        {
            if (player != null && initialPlayerSpawn != null)
            {
                player.GetComponent<PlayerMotor>().TeleportTo(initialPlayerSpawn.position);
                player.transform.rotation = initialPlayerSpawn.rotation;
            }
        }
        
        public void PlayGLaDOSLine(int lineIndex)
        {
            if (gladosLines != null && lineIndex >= 0 && lineIndex < gladosLines.Length)
            {
                _audioSource.clip = gladosLines[lineIndex];
                _audioSource.Play();
            }
        }
        
    }
}
