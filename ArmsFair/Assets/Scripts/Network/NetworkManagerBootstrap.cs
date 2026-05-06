using System.Threading.Tasks;
using ArmsFair.Auth;
using ArmsFair.UI;
using UnityEngine;

namespace ArmsFair.Network
{
    [DefaultExecutionOrder(100)]
    public class NetworkManagerBootstrap : MonoBehaviour
    {
        private async void Start()
        {
            bool autoLoggedIn = await AccountManager.Instance.TryAutoLoginAsync();

            if (!autoLoggedIn)
                UIManager.Instance.GoTo("Login");
            else
                UIManager.Instance.GoTo("MainMenu");
        }
    }
}
