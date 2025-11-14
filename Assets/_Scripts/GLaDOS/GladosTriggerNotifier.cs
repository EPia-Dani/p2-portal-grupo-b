using _Scripts.Interfaces;
using UnityEngine;

namespace _Scripts.GLaDOS
{
    public class GladosTriggerNotifier : MonoBehaviour, IGladosNotifier
    {
        
        [SerializeField] private GameObject gladosObject;
        private IGlados _glados;
        private bool _notified = false;
        
        private void Awake()
        {
            if (gladosObject != null)
            {
                _glados = gladosObject.GetComponent<IGlados>();
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (_notified) return;
            _notified = true;
            if (other.CompareTag("Player"))
            {
                NotifyGlados();
            }
        }

        public void NotifyGlados()
        {
            _glados?.StartNextSequence();
        }
    }
}