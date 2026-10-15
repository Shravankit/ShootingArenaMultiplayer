using System;

namespace MultiplayerShooting.Network.Data
{
    [Serializable]
    public class RoomInfo
    {
        public string id;
        public string name;
        public int players;
        public int maxPlayers;
        public bool hasPassword;
        public string hostId;
    }

    [Serializable]
    public class LobbyPlayer
    {
        public string id;
        public string name;
        public bool connected;
        public bool ready; // filled from snapshots, not sent by the server in player lists
    }
}