using System.Collections.Generic;
using MultiplayerShooting.Core.Architecture;
using MultiplayerShooting.Gameplay.Combat;
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
        [SerializeField] BulletTracer tracerPrefab;

        INetworkService networkService;
        readonly Dictionary<string, RemotePlayer> remotePlayers = new();
        bool spawned;

        CombatNetworkHandler combat;
        Transform localSpawn;

        void Start()
        {
            combat = GetComponent<CombatNetworkHandler>();
            if (combat == null) combat = gameObject.AddComponent<CombatNetworkHandler>();
            combat.TracerPrefab = tracerPrefab; // ADDED

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
                    var rh = rp.GetComponent<HealthController>();
                    if (rh == null) rh = rp.gameObject.AddComponent<HealthController>();
                    combat.Register(id, rh);
                    RemotePlayer captured = rp;
                    rh.DeadChanged += dead => captured.SetAlive(!dead);
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
            localSpawn = sp;
            WeaponController weapon = lp.GetComponentInChildren<WeaponController>();
            HealthController health = lp.GetComponentInChildren<HealthController>();
            if (weapon == null || health == null)
                Debug.LogError("[Game] LocalPlayer prefab needs WeaponController + HealthController");
            else
            {
                weapon.Init(networkService);
                combat.RegisterLocal(networkService.PlayerId, weapon, health);
                health.DeadChanged += dead => { lp.InputLocked = dead; weapon.Locked = dead; };
            }
            combat.LocalRespawned += () => lp.Teleport(localSpawn.position, localSpawn.rotation);

            spawned = true;
            Debug.Log($"[Game] local player spawned ({ids.Count} in match)");
        }

        private void OnPlayerLeft(JObject m)
        {
            string id = (string)m["id"];
            combat.Unregister(id);
            if (remotePlayers.Remove(id, out RemotePlayer rp)) Destroy(rp.gameObject);
        }
    }
}