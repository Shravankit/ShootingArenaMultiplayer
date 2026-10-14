using System;
using Newtonsoft.Json.Linq;

namespace MultiplayerShooting.Interfaces
{
    public interface INetworkService : IService
    {
        bool IsConnected { get; }
        string PlayerId { get; }

        void Connect();
        void Disconnect();

        void SendData<T>(
            string eventName,
            T data,
            string to = null
        );

        Action Subscribe<T>(
            string eventName,
            Action<T> callback
        );

        void SendJson(
            string eventName,
            JObject data,
            string to = null
        );
    }
}