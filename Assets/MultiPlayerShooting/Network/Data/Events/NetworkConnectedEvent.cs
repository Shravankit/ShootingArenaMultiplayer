namespace MultiplayerShooting.Network.Data.Events
{
    public class NetworkConnectedEvent
    {
        public string PlayerId { get; }

        public NetworkConnectedEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }
}