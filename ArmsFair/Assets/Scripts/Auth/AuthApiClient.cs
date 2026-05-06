using System;
using System.Text;
using System.Threading.Tasks;
using ArmsFair.Shared.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace ArmsFair.Auth
{
    public class AuthResult
    {
        public string Token         { get; }
        public string PlayerId      { get; }
        public string Username      { get; }
        public string HomeNationIso { get; }

        public AuthResult(string token, string playerId, string username, string homeNationIso)
        {
            Token         = token;
            PlayerId      = playerId;
            Username      = username;
            HomeNationIso = homeNationIso;
        }
    }

    [Serializable]
    internal class AuthResponse
    {
        public string token;
        public string playerId;
        public string username;
        public string homeNationIso;
    }

    [Serializable]
    internal class ProfileResponse
    {
        public string id;
        public string username;
        public string homeNation;
        public string companyName;
        public int    capital;
        public int    reputation;
        public int    sharePrice;
        public int    peaceCredits;
        public int    latentRisk;
        public string status;
    }

    public class AuthApiClient
    {
        private readonly string _baseUrl;

        public AuthApiClient(string serverUrl)
        {
            _baseUrl = serverUrl.TrimEnd('/');
        }

        public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password)
        {
            var json = $"{{\"usernameOrEmail\":\"{usernameOrEmail}\",\"password\":\"{password}\"}}";
            var r    = await PostAsync<AuthResponse>("/api/auth/login", json);
            return new AuthResult(r.token, r.playerId, r.username, r.homeNationIso);
        }

        public async Task<AuthResult> RegisterAsync(string username, string email, string password)
        {
            var json = $"{{\"username\":\"{username}\",\"email\":\"{email}\",\"password\":\"{password}\"}}";
            var r    = await PostAsync<AuthResponse>("/api/auth/register", json);
            return new AuthResult(r.token, r.playerId, r.username, r.homeNationIso);
        }

        public async Task<PlayerProfile> GetMeAsync(string token)
        {
            var r = await GetAsync<ProfileResponse>("/api/auth/me", token);
            return new PlayerProfile
            {
                Id           = r.id,
                Username     = r.username,
                HomeNation   = r.homeNation,
                CompanyName  = r.companyName,
                Capital      = r.capital,
                Reputation   = r.reputation,
                SharePrice   = r.sharePrice,
                PeaceCredits = r.peaceCredits,
                LatentRisk   = r.latentRisk,
                Status       = string.IsNullOrEmpty(r.status) ? "active" : r.status
            };
        }

        private static async Task<T> PostAsync<T>(string url, string json)
        {
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"[AuthApiClient] POST {url} failed: {request.error} — {request.downloadHandler.text}");

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        private static async Task<T> GetAsync<T>(string url, string token)
        {
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {token}");
            request.downloadHandler = new DownloadHandlerBuffer();

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"[AuthApiClient] GET {url} failed: {request.error} — {request.downloadHandler.text}");

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }
    }
}
