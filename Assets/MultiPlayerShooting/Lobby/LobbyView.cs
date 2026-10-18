using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiplayerShooting.Lobby
{
    [RequireComponent(typeof(UIDocument))]
    public class LobbyView : MonoBehaviour
    {
        private const int MatchSize = 4; // keep in sync with server MATCH_SIZE

        [SerializeField] private LobbyController controller;

        private TextField nameField;
        private Toggle readyToggle;
        private Label statusLabel, playersLabel;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            nameField = root.Q<TextField>("name-field");
            readyToggle = root.Q<Toggle>("ready-toggle");
            statusLabel = root.Q<Label>("status-label");
            playersLabel = root.Q<Label>("players-label");

            Debug.Log($"[LobbyView] toggle={(readyToggle != null)} name={(nameField != null)} controller={(controller != null)}");

            readyToggle.RegisterValueChangedCallback(e =>
            {
                Debug.Log($"[LobbyView] toggle changed -> {e.newValue}");
                controller.SetReady(e.newValue, nameField.value);
            });

            // readyToggle.RegisterValueChangedCallback(
            //     e => controller.SetReady(e.newValue, nameField.value));

            controller.Changed += Refresh;
            controller.ErrorRaised += ShowError;

            Refresh();
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.Changed -= Refresh;
            controller.ErrorRaised -= ShowError;
        }

        private void ShowError(string message) => statusLabel.text = message;

        private void Refresh()
        {
            bool disconnected = controller.State == LobbyState.Disconnected;

            nameField.SetEnabled(disconnected);
            if (disconnected) readyToggle.SetValueWithoutNotify(false);

            switch (controller.State)
            {
                case LobbyState.Disconnected:
                    statusLabel.text = "Enter your name and tick Ready";
                    playersLabel.text = "";
                    break;

                case LobbyState.Searching:
                    statusLabel.text = "Finding a match...";
                    playersLabel.text = "";
                    break;

                case LobbyState.InRoom:
                    int total = controller.Players.Count;
                    int ready = controller.Players.Count(p => p.connected && p.ready);

                    statusLabel.text = total < MatchSize
                        ? $"Waiting for players {total}/{MatchSize}"
                        : $"Ready {ready}/{MatchSize} - starting when all are ready";

                    playersLabel.text = string.Join("\n", controller.Players.Select(p =>
                        $"{p.name}{(p.id == controller.SelfId ? " (you)" : "")}  -  " +
                        (!p.connected ? "offline" : p.ready ? "READY" : "waiting")));
                    break;
            }
        }
    }
}