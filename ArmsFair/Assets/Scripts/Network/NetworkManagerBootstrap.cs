using ArmsFair.UI;
using UnityEngine;

namespace ArmsFair.Network
{
    [DefaultExecutionOrder(100)]
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        private void Start()
        {
            // Always start at Login — stored JWTs from previous sessions are never valid
            // across peer-hosted sessions (different server key each run).
            UIManager.Instance.GoTo("Login");
        }
    }
}
