using ArmsFair.UI;
using UnityEngine;

namespace ArmsFair.Network
{
    [DefaultExecutionOrder(100)]
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Always start at HostOrJoin — each session targets a specific peer server,
            // so stored JWTs from previous sessions are never valid here.
            UIManager.Instance.GoTo("HostOrJoin");
        }
    }
}
