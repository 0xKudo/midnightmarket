using System.Collections.Generic;
using ArmsFair.Network;
using ArmsFair.Shared.Models.Messages;
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

            _client.OnWorldUpdate.AddListener(OnWorldUpdate);
        }

        private void OnDestroy()
        {
            if (_client != null)
                _client.OnWorldUpdate.RemoveListener(OnWorldUpdate);
        }

        private void OnWorldUpdate(WorldUpdateMessage msg)
        {
            var tensions = new Dictionary<string, float>();
            foreach (var cc in msg.CountryChanges)
                tensions[cc.Iso] = cc.NewTension;
            _globe.UpdateCountryTensions(tensions);
        }
    }
}
