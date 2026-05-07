using System;
using System.Threading.Tasks;
using ArmsFair.Network;
using ArmsFair.Shared.Models;
using UnityEngine;
using UnityEngine.Events;

namespace ArmsFair.Auth
{
    public class AccountManager : MonoBehaviour
    {
        public static AccountManager Instance { get; private set; }

        [Header("Server")]
        [SerializeField] private string serverUrl = "http://localhost:5059";

        public bool          IsLoggedIn  { get; private set; }
        public PlayerProfile LocalPlayer { get; private set; }
        public string        Token       { get; private set; }

        public UnityEvent OnLoggedIn  = new();
        public UnityEvent OnLoggedOut = new();

        private AuthApiClient _api;

        private const string TokenKey = "auth_token";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _api = new AuthApiClient(serverUrl);
        }

        public async Task<bool> TryAutoLoginAsync()
        {
            var stored = PlayerPrefs.GetString(TokenKey, null);
            if (string.IsNullOrEmpty(stored)) return false;

            try
            {
                LocalPlayer = await _api.GetMeAsync(stored);
                Token       = stored;
                IsLoggedIn  = true;
                await GameClient.Instance.ConnectAsync(Token);
                OnLoggedIn.Invoke();
                return true;
            }
            catch
            {
                PlayerPrefs.DeleteKey(TokenKey);
                return false;
            }
        }

        public async Task LoginAsync(string usernameOrEmail, string password)
        {
            var result  = await _api.LoginAsync(usernameOrEmail, password);
            Token       = result.Token;
            LocalPlayer = new PlayerProfile
            {
                Id          = result.PlayerId,
                Username    = result.Username,
                HomeNation  = result.HomeNationIso,
                CompanyName = result.CompanyName,
            };
            IsLoggedIn = true;
            PlayerPrefs.SetString(TokenKey, Token);
            PlayerPrefs.Save();
            await GameClient.Instance.ConnectAsync(Token);
            OnLoggedIn.Invoke();
        }

        public async Task RegisterAsync(string username, string email, string password)
        {
            var result  = await _api.RegisterAsync(username, email, password);
            Token       = result.Token;
            LocalPlayer = new PlayerProfile
            {
                Id          = result.PlayerId,
                Username    = result.Username,
                HomeNation  = result.HomeNationIso,
                CompanyName = result.CompanyName,
            };
            IsLoggedIn = true;
            PlayerPrefs.SetString(TokenKey, Token);
            PlayerPrefs.Save();
            await GameClient.Instance.ConnectAsync(Token);
            OnLoggedIn.Invoke();
        }

        public async Task SaveProfileAsync(string homeNationIso, string companyName)
        {
            LocalPlayer = await _api.PatchProfileAsync(Token, homeNationIso, companyName);
        }

        public async Task LogOutAsync()
        {
            IsLoggedIn  = false;
            Token       = null;
            LocalPlayer = null;
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.Save();
            await GameClient.Instance.DisconnectAsync();
            OnLoggedOut.Invoke();
        }
    }
}
