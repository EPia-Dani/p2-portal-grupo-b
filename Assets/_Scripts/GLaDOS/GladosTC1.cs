using System;
using System.Collections;
using _Scripts.CompanionCube;
using _Scripts.Interfaces;
using _Scripts.Player.Runtime;
using UnityEngine;

namespace _Scripts.GLaDOS
{
    /// <summary>
    /// GLaDOS AI Core Script that will work as the Game Manager
    /// </summary>
    public class GladosTC1 : MonoBehaviour, IGlados
    {
        [SerializeField] private GameObject player;
        [SerializeField] private Transform initialPlayerSpawn;
        [SerializeField] private AudioClip[] gladosIntroLines;
        [SerializeField] private AudioClip[] gladosCubeSpawnLines;
        [SerializeField] private AudioClip[] gladosCompletionLines;
        [SerializeField] private CubeSpawner cubeSpawner;
        [SerializeField] private DoorBase firstDoor;
        [SerializeField] private Portal[] startingPortals;
        private AudioSource _audioSource;
        private int _currentSequence = 0;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            RespawnPlayer();
            foreach (var portal in startingPortals)
            {
                portal.gameObject.SetActive(false);
            }
            Debug.Log("GLaDOS Initialized. Playing Intro Line.");
            StartCoroutine(StartingDialogue());
        }

        private IEnumerator StartingDialogue()
        {
            yield return new WaitForSeconds(2f);
            for (int i = 0; i < gladosIntroLines.Length; i++)
            {
                PlayGLaDOSIntroLine(i);
                yield return new WaitForSeconds(gladosIntroLines[i].length + 1f);
            }

            foreach (var portal in startingPortals)
            {
                portal.gameObject.SetActive(true);
                portal.GladosOnlyOpenPortal();
            }
            firstDoor.OnButtonPressed();
        }

        public void RespawnPlayer()
        {
            if (player != null && initialPlayerSpawn != null)
            {
                player.GetComponent<PlayerMotor>().TeleportTo(initialPlayerSpawn.position);
                player.transform.rotation = initialPlayerSpawn.rotation;
            }
        }
        
        public void PlayGLaDOSIntroLine(int lineIndex)
        {
            if (gladosIntroLines != null && lineIndex >= 0 && lineIndex < gladosIntroLines.Length)
            {
                _audioSource.clip = gladosIntroLines[lineIndex];
                _audioSource.Play();
            }
        }
        
        public void PlayGLaDOSCubeSpawnLine(int lineIndex)
        {
            if (gladosCubeSpawnLines != null && lineIndex >= 0 && lineIndex < gladosCubeSpawnLines.Length)
            {
                _audioSource.clip = gladosCubeSpawnLines[lineIndex];
                _audioSource.Play();
            }
        }

        public void StartNextSequence()
        {
            _currentSequence++;
            PlaySequence();
        }

        private void PlaySequence()
        {
            switch (_currentSequence)
            {
                case 1:
                    StartCoroutine(CubeSpawnSequence());
                    break;
                case 2:
                    StartCoroutine(CompleteionSequence());
                    break;
                default:
                    Debug.Log("No more sequences.");
                    break;
            }
        }

        private IEnumerator CompleteionSequence()
        {
            for (int i = 0; i < gladosCompletionLines.Length; i++)
            {
                if (gladosCompletionLines[i] != null)
                {
                    _audioSource.clip = gladosCompletionLines[i];
                    _audioSource.Play();
                    yield return new WaitForSeconds(gladosCompletionLines[i].length + 1f);
                }
            }
            Debug.Log("GLaDOS Completion Sequence Finished.");
        }

        private IEnumerator CubeSpawnSequence()
        {
            for (int i = 0; i < gladosIntroLines.Length; i++)
            {
                PlayGLaDOSCubeSpawnLine(i);
                yield return new WaitForSeconds(gladosIntroLines[i].length + 1f);
            }
            cubeSpawner.SpawnCube();
        }
    }
}
