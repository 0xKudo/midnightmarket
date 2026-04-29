using ArmsFair.Network;
using UnityEngine;

namespace ArmsFair.Network
{
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "http://localhost:5000";

        private static bool _created;

        private void Awake()
        {
            if (_created)
            {
                Destroy(gameObject);
                return;
            }

            _created = true;
            DontDestroyOnLoad(gameObject);

            if (GetComponent<UnityMainThreadDispatcher>() == null)
                gameObject.AddComponent<UnityMainThreadDispatcher>();

            var client = GetComponent<GameClient>();
            if (client != null)
                client.ServerUrl = serverUrl;
        }

        private void OnDestroy()
        {
            if (gameObject == null)
                _created = false;
        }
    }
}
