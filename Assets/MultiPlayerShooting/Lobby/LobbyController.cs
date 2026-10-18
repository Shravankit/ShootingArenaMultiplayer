using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerShooting.Core.Architecture;
using MultiplayerShooting.Interfaces;
using MultiplayerShooting.Network.Data;
using MultiplayerShooting.Network.Data.Events;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerShooting.Lobby
{
    public enum LobbyState { Disconnected, Searching, InRoom }

    public class LobbyController : MonoBehaviour
    {
        [SerializeField] private string gameScene = "Game";

        public event Action Changed;
        public event Action<string> ErrorRaised;

        public LobbyState State { get; private set; } = LobbyState.Disconnected;
        public IReadOnlyList<LobbyPlayer> Players => players;
        public string SelfId => net?.PlayerId;

        private INetworkService net;
        private Dictionary<string, Action<JObject>> map;
        private readonly List<LobbyPlayer> players = new();
        private bool wantReady;
        private int stateSeq;

        private void Start()
        {
            Debug.Log("[Lobby] Start ran");
            net = ServiceRegistry.Get<INetworkService>();

            map = new Dictionary<string, Action<JObject>>
            {
                ["room_joined"] = OnRoomJoined,
                ["room_left"] = _ => LeaveLocal(),
                ["kicked"] = _ => LeaveLocal(),
                ["player_joined"] = OnPlayerJoined,
                ["player_left"] = m => { players.RemoveAll(p => p.id == (string)m["id"]); Changed?.Invoke(); },
                ["player_disconnected"] = m => SetConnected(m, false),
                ["player_reconnected"] = m => SetConnected(m, true),
                ["snapshot"] = OnSnapshot,
                ["room_state"] = m => LoadGameIfPlaying(m["patch"]),
                ["error"] = m => ErrorRaised?.Invoke((string)m["message"]),
            };
            foreach (var kv in map) net.On(kv.Key, kv.Value);

            EventBus.Subscribe<NetworkConnectedEvent>(OnConnected);
            EventBus.Subscribe<NetworkDisconnectedEvent>(OnDisconnected);
        }

        private void OnDestroy()
        {
            EventBus.UnSubscribe<NetworkConnectedEvent>(OnConnected);
            EventBus.UnSubscribe<NetworkDisconnectedEvent>(OnDisconnected);
            if (net != null && map != null)
                foreach (var kv in map) net.Off(kv.Key, kv.Value);
        }

        // ---------- UI action ----------

        public void SetReady(bool ready, string playerName)
        {
            Debug.Log($"[Lobby] SetReady {ready}, state={State}, connected={net.IsConnected}");
            wantReady = ready;

            switch (State)
            {
                case LobbyState.Disconnected:
                    if (!ready) return;
                    State = LobbyState.Searching;
                    Changed?.Invoke();
                    if (net.IsConnected)
                    {
                        net.Send(new JObject { ["t"] = "quick_join" });
                    }
                    else
                    {
                        net.SetPlayerName(string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim());
                        net.Connect();
                    }
                    break;

                case LobbyState.InRoom:
                    SendReady();
                    break;
            }
        }

        public void LeaveRoom() => net.Send(new JObject { ["t"] = "leave_room" });

        // ---------- Connection ----------

        private void OnConnected(NetworkConnectedEvent e) =>
            net.Send(new JObject { ["t"] = "quick_join" });

        private void OnDisconnected(NetworkDisconnectedEvent e)
        {
            wantReady = false;
            players.Clear();
            State = LobbyState.Disconnected;
            Changed?.Invoke();
        }

        private void LeaveLocal()
        {
            wantReady = false;
            players.Clear();
            State = LobbyState.Disconnected;
            Changed?.Invoke();
        }

        // ---------- Server messages ----------

        private void OnRoomJoined(JObject m)
        {
            players.Clear();
            foreach (JToken p in (JArray)m["players"])
                players.Add(p.ToObject<LobbyPlayer>());
            ApplySnapshot(m["snapshot"]);

            stateSeq = 0; // server resets seq on join
            State = LobbyState.InRoom;
            Changed?.Invoke();

            if (wantReady) SendReady();
            LoadGameIfPlaying(m["roomState"]);
        }

        private void OnPlayerJoined(JObject m)
        {
            LobbyPlayer p = m["player"].ToObject<LobbyPlayer>();
            players.RemoveAll(x => x.id == p.id);
            players.Add(p);
            Changed?.Invoke();
        }

        private void SetConnected(JObject m, bool connected)
        {
            LobbyPlayer p = players.FirstOrDefault(x => x.id == (string)m["id"]);
            if (p == null) return;
            p.connected = connected;
            Changed?.Invoke();
        }

        private void OnSnapshot(JObject m)
        {
            if (State != LobbyState.InRoom) return;
            ApplySnapshot(m);
            Changed?.Invoke();
        }

        private void ApplySnapshot(JToken snap)
        {
            if (snap?["players"] is not JArray arr) return;

            foreach (JToken entry in arr)
            {
                LobbyPlayer p = players.FirstOrDefault(x => x.id == (string)entry["id"]);
                if (p == null) continue;

                if (entry["connected"] != null) p.connected = (bool)entry["connected"];
                if (entry["data"]?["ready"] != null) p.ready = (bool)entry["data"]["ready"];
            }
        }

        private void SendReady() =>
            net.Send(new JObject
            {
                ["t"] = "state",
                ["seq"] = ++stateSeq,
                ["data"] = new JObject { ["ready"] = wantReady }
            });

        private void LoadGameIfPlaying(JToken state)
        {
            if (state?["phase"]?.ToString() != "playing") return;
            string scene = state["scene"]?.ToString();
            SceneManager.LoadScene(string.IsNullOrEmpty(scene) ? gameScene : scene);
        }
    }
}