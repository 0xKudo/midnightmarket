using System.Collections.Generic;
using ArmsFair.Network;
using UnityEngine;

namespace ArmsFair.Map
{
    // Add to the Globe GO in MapGlobe scene.
    // Bridges GameClient WorldUpdate events to GlobeRenderer tension array.
    [RequireComponent(typeof(GlobeRenderer))]
    public class GlobeTensionBridge : MonoBehaviour
    {
        private GlobeRenderer _globe;
        private GameClient    _client;

        private void Start()
        {
            _globe  = GetComponent<GlobeRenderer>();
            _client = FindFirstObjectByType<GameClient>();

            if (_client == null)
            {
                Debug.LogWarning("[GlobeTensionBridge] No GameClient found — tension updates disabled.");
                return;
            }

            _client.OnWorldUpdated.AddListener(OnWorldUpdated);
        }

        private void OnDestroy()
        {
            if (_client != null)
                _client.OnWorldUpdated.RemoveListener(OnWorldUpdated);
        }

        private void OnWorldUpdated(ArmsFair.Shared.Messages.WorldUpdateMessage msg)
        {
            var tensions = new Dictionary<string, float>();
            foreach (var cs in msg.Countries)
                tensions[cs.Iso] = cs.Tension;
            _globe.UpdateCountryTensions(tensions);
        }
    }
}
