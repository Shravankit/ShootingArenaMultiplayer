using System.Collections.Generic;
using MultiplayerShooting.Core.Architecture;
using MultiplayerShooting.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MultiplayerShooting.Gameplay
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] LocalPlayer localPlayerPrefab;
        [SerializeField] RemotePlayer remotePlayerPrefab;
        [SerializeField] Transform[] spawnPoints;

        INetworkService networkService;
        readonly Dictionary<string, RemotePlayer> remotePlayers = new();
        bool spawned;

        void Start()
        {
            networkService = ServiceRegistry.Get<INetworkService>();
            networkService.On("snapshot", OnSnapShot);
            networkService.On("player_left", OnPlayerLeft);
            networkService.Send(new JObject { ["t"] = "request_snapshot" });
        }

        void OnDestroy()
        {
            if (networkService == null) return;
            networkService.Off("snapshot", OnSnapShot);
            networkService.Off("player_left", OnPlayerLeft);
        }

        private void OnSnapShot(JObject m)
        {
            Debug.Log($"[Game] snapshot: {m.ToString(Newtonsoft.Json.Formatting.None)}");

            if (m["players"] is not JArray arr) return;

            if (!spawned)
            {
                bool full = m.Value<bool?>("full") ?? false;
                if (!full) return; // wait for a full one
                SpawnLocal(arr);
            }

            foreach (JToken entry in arr)
            {
                string id = entry.Value<string>("id");
                if (string.IsNullOrEmpty(id) || id == networkService.PlayerId) continue;

                if (!remotePlayers.TryGetValue(id, out RemotePlayer rp))
                {
                    rp = Instantiate(remotePlayerPrefab);
                    remotePlayers[id] = rp;
                    Debug.Log($"[Game] remote player {id} spawned");
                }
                rp.Apply(entry["data"]);
            }
        }

        void SpawnLocal(JArray arr)
        {
            var ids = new List<string> { networkService.PlayerId };
            foreach (JToken e in arr)
            {
                string id = (string)e["id"];
                if (!ids.Contains(id)) ids.Add(id);
            }
            ids.Sort(string.CompareOrdinal);

            Transform sp = spawnPoints[ids.IndexOf(networkService.PlayerId) % spawnPoints.Length];
            LocalPlayer lp = Instantiate(localPlayerPrefab, sp.position, sp.rotation);
            lp.Init(networkService);
            spawned = true;
            Debug.Log($"[Game] local player spawned ({ids.Count} in match)");
        }

        private void OnPlayerLeft(JObject m)
        {
            string id = (string)m["id"];
            if (remotePlayers.Remove(id, out RemotePlayer rp)) Destroy(rp.gameObject);
        }
    }
}