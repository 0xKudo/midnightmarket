using System;
using System.Text;
using System.Threading.Tasks;
using ArmsFair.Auth;
using UnityEngine;
using UnityEngine.Networking;

namespace ArmsFair.Network
{
    [Serializable]
    public class RoomInfo
    {
        public string   roomId;
        public string   roomName;
        public string   hostPlayerId;
        public string   hostUsername;
        public string   timerPreset;
        public string   inviteCode;
        public string   gameMode;
        public int      playerSlots;
        public bool     isPrivate;
        public bool     aiFillIn;
        public bool     isStarted;
        public string[] playerIds;
    }

    [Serializable]
    public class RoomSummary
    {
        public string roomId;
        public string roomName;
        public string hostUsername;
        public string gameMode;
        public string timerPreset;
        public int    playerSlots;
        public int    playerCount;
        public bool   isPrivate;
        public bool   isStarted;
        public string inviteCode;
    }

    [Serializable]
    public class CreateRoomPayload
    {
        public string roomName;
        public int    playerSlots;
        public string timerPreset;
        public bool   voiceEnabled;
        public bool   aiFillIn;
        public bool   isPrivate;
        public int    gameMode; // server enum: Realistic=1, EqualWorld=2, BlankSlate=3, HotWorld=4, Custom=5
    }

    [Serializable]
    internal class RoomSummaryList
    {
        public RoomSummary[] items;
    }

    public class LobbyApiClient
    {
        private readonly string _baseUrl;

        public LobbyApiClient(string serverUrl)
        {
            _baseUrl = serverUrl.TrimEnd('/');
        }

        public async Task<RoomInfo> CreateRoomAsync(CreateRoomPayload payload)
        {
            var json = JsonUtility.ToJson(payload);
            return await PostAsync<RoomInfo>("/api/rooms", json);
        }

        public async Task<RoomSummary[]> ListRoomsAsync()
        {
            var raw = await GetRawAsync("/api/rooms");
            var wrapped = JsonUtility.FromJson<RoomSummaryList>($"{{\"items\":{raw}}}");
            return wrapped?.items ?? Array.Empty<RoomSummary>();
        }

        public async Task<RoomInfo> GetRoomAsync(string id)
        {
            return await GetAsync<RoomInfo>($"/api/rooms/{id}");
        }

        public async Task<RoomInfo> JoinRoomAsync(string id)
        {
            return await PostAsync<RoomInfo>($"/api/rooms/{id}/join", "{}");
        }

        private async Task<T> PostAsync<T>(string path, string json)
        {
            var url   = _baseUrl + path;
            var token = AccountManager.Instance.Token;
            Debug.Log($"[LobbyApiClient] POST {path} token={(string.IsNullOrEmpty(token) ? "NULL" : token[..Math.Min(20, token.Length)] + "...")}");
            var request = new UnityWebRequest(url, "POST");
            request.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type",  "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {token}");

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LobbyApiClient] POST {path} → {request.responseCode}: {request.error} | body: {request.downloadHandler.text}");
                throw new Exception($"{request.responseCode}: {request.error} — {request.downloadHandler.text}");
            }

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        private async Task<T> GetAsync<T>(string path)
        {
            var raw = await GetRawAsync(path);
            return JsonUtility.FromJson<T>(raw);
        }

        private async Task<string> GetRawAsync(string path)
        {
            var url     = _baseUrl + path;
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", $"Bearer {AccountManager.Instance.Token}");
            request.downloadHandler = new DownloadHandlerBuffer();

            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"{request.responseCode}: {request.error} — {request.downloadHandler.text}");

            return request.downloadHandler.text;
        }
    }
}
