using System;
using System.Collections.Generic;
using MultiplayerShooting.Core.Architecture;
using MultiplayerShooting.Interfaces;
using MultiplayerShooting.Network.Data.Events;
using MultiplayerShooting.Network.Messaging;
using MultiplayerShooting.Network.Serialization;
using NativeWebSocket;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MultiplayerShooting.Network.core
{
    public class NetworkManager : MonoBehaviour, INetworkService
    {
        [SerializeField]
        private string serverUrl = "ws://localhost:8080";

        [SerializeField]
        private string playerName = "Player";

        public bool IsConnected =>
            socket != null &&
            socket.State == WebSocketState.Open;

        public string PlayerId { get; private set; }

        private WebSocket socket;
        private bool shuttingDown;
        private bool initialized;

        private readonly Dictionary<string, Action<JObject>>
            handlers = new();

        public void Initialize()
        {
            if (initialized)
                return;

            shuttingDown = false;
            initialized = true;

            if (!ServiceRegistry.Has<INetworkService>())
                ServiceRegistry.Register<INetworkService>(this);

            Connect();
        }

        public void SetPlayerName(string name) => playerName = name;

        public void Connect()
        {
            if (shuttingDown || IsConnected || socket != null)
                return;

            WebSocket newSocket = new WebSocket(serverUrl);
            socket = newSocket;

            newSocket.OnOpen += () =>
            {
                if (socket != newSocket)
                {
                    Debug.Log("[Network] socket open, sending hello");
                    return;
                }

                newSocket.SendText(
                    new JObject
                    {
                        ["t"] = "hello",
                        ["name"] = playerName,
                        ["v"] = 1
                    }.ToString(Newtonsoft.Json.Formatting.None)
                );
            };

            newSocket.OnMessage += bytes =>
            {
                if (socket != newSocket)
                    return;

                try
                {
                    string json =
                        System.Text.Encoding.UTF8.GetString(bytes);

                    HandleMessage(JObject.Parse(json));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            };

            newSocket.OnError += error =>
            {
                Debug.LogWarning($"[Network] {error}");
            };

            newSocket.OnClose += code =>
            {
                if (socket != newSocket)
                {
                    Debug.Log($"[Network] closed: {code}");
                    return;
                }

                socket = null;
                PlayerId = null;

                EventBus.Publish(new NetworkDisconnectedEvent());
            };

            _ = newSocket.Connect();
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            socket?.DispatchMessageQueue();
#endif
        }

        private void HandleMessage(JObject message)
        {
            Debug.Log($"[Network] recv {(string)message["t"]}");
            string type = (string)message["t"];

            if (type == "welcome")
            {
                PlayerId = (string)message["id"];

                EventBus.Publish(
                    new NetworkConnectedEvent(PlayerId)
                );
            }

            if (type != null &&
                handlers.TryGetValue(type, out var handler))
            {
                handler?.Invoke(message);
            }
        }

        public void On(string type, Action<JObject> handler)
        {
            if (string.IsNullOrEmpty(type) || handler == null)
                return;

            if (handlers.TryGetValue(type, out var existing))
                handlers[type] = existing + handler;
            else
                handlers[type] = handler;
        }

        public void Off(string type, Action<JObject> handler)
        {
            if (!handlers.TryGetValue(type, out var existing))
                return;

            existing -= handler;

            if (existing == null)
                handlers.Remove(type);
            else
                handlers[type] = existing;
        }

        public void SendData<T>(
            string eventName,
            T data,
            string to = null)
        {
            SendJson(eventName, NetworkJson.Serialize(data), to);
        }

        public void SendJson(
            string eventName,
            JObject data,
            string to = null)
        {
            if (!IsConnected)
            {
                Debug.LogWarning(
                    $"Cannot send '{eventName}': not connected.");
                return;
            }

            var envelope = new NetworkEnvelope
            {
                Type = "event",
                Name = eventName,
                Data = data,
                To = to,
                Echo = false
            };

            _ = socket.SendText(
                envelope.ToJson().ToString(
                    Newtonsoft.Json.Formatting.None)
            );
        }

        public Action Subscribe<T>(
            string eventName,
            Action<T> callback)
        {
            Action<JObject> handler = message =>
            {
                if ((string)message["name"] != eventName)
                    return;

                try
                {
                    T data = NetworkJson.Deserialize<T>(
                        message["data"]);

                    callback?.Invoke(data);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Failed to read {eventName}: {exception.Message}");
                }
            };

            On("event", handler);

            return () => Off("event", handler);
        }

        public void Disconnect()
        {
            if (shuttingDown)
                return;

            shuttingDown = true;

            WebSocket oldSocket = socket;
            socket = null;

            PlayerId = null;

            if (oldSocket != null)
                _ = oldSocket.Close();
        }

        public void ShutDown()
        {
            if (ServiceRegistry.Has<INetworkService>() &&
                ReferenceEquals(
                    ServiceRegistry.Get<INetworkService>(), this))
            {
                ServiceRegistry.Unregister<INetworkService>();
            }

            Disconnect();
            handlers.Clear();
            initialized = false;
        }

        private void OnDestroy()
        {
            ShutDown();
        }

        public void Send(JObject message)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Cannot send: not connected.");
                return;
            }
            _ = socket.SendText(message.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}