using MultiplayerShooting.Core.Architecture;
using MultiplayerShooting.Interfaces;
using MultiplayerShooting.Network.core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerShooting.Core.Bootstrap
{
    public class Bootstrapper : MonoBehaviour
    {
        [SerializeField] NetworkManager networkManagerPrefab;
        [SerializeField] string firstScene = "Lobby";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatistics()
        {
            ServiceRegistry.Clear();
            EventBus.Clear();
        }

        void Awake()
        {
            if (ServiceRegistry.Has<INetworkService>())
            {
                SceneManager.LoadScene(firstScene, LoadSceneMode.Additive);
                return;
            }

            NetworkManager nm = Instantiate(networkManagerPrefab);
            DontDestroyOnLoad(nm.gameObject);

            ServiceRegistry.Register<INetworkService>(nm);

            SceneManager.LoadScene(firstScene, LoadSceneMode.Additive);
        }
    }
}