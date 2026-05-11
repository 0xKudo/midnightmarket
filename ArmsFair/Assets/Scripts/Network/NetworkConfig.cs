using UnityEngine;

namespace ArmsFair.Network
{
    /// <summary>
    /// Runtime-mutable server configuration. Set ServerBaseUrl before any auth or lobby call.
    /// </summary>
    public static class NetworkConfig
    {
        // Base URL for auth REST API and lobby REST API (no trailing slash)
        public static string ServerBaseUrl { get; set; } = "http://localhost:5059";

        // Full SignalR hub URL, derived from ServerBaseUrl
        public static string GameHubUrl => ServerBaseUrl.TrimEnd('/') + "/gamehub";

        // Relay API on the Hostinger VPS — maps invite codes to host IP:port
        public const string RelayBaseUrl = "https://armsfair.laynekudo.com/arms-invite";

        // Relay invite code for this session (set when host registers with relay)
        public static string RelayCode { get; set; } = "";

        // Whether this client is the host of the current game
        public static bool IsHost { get; set; }

        // Persistent device credentials — used for auto-registration on peer servers
        public static string DeviceUsername
        {
            get => PlayerPrefs.GetString("device_username", "");
            set { PlayerPrefs.SetString("device_username", value); PlayerPrefs.Save(); }
        }

        public static string DeviceToken
        {
            get
            {
                var t = PlayerPrefs.GetString("device_token", "");
                if (string.IsNullOrEmpty(t))
                {
                    t = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("device_token", t);
                    PlayerPrefs.Save();
                }
                return t;
            }
        }
    }
}
