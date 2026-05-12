using ArmsFair.UI;
using UnityEngine;

namespace ArmsFair.Network
{
    [DefaultExecutionOrder(100)]
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        private void Start()
        {
            UIManager.Instance.GoTo("Splash");
        }
    }
}
