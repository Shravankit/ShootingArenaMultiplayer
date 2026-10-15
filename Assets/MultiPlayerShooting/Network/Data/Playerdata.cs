using System;
using UnityEngine;

namespace MultiplayerShooting.Network.Data
{
    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public Vector3 position;
        public Vector3 rotation;
        public float health;
        public string weapon;
    }
}