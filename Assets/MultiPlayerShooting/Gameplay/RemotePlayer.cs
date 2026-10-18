using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MultiplayerShooting.Gameplay
{
    public class RemotePlayer : MonoBehaviour
    {
        [SerializeField] private float smoothing = 15f;

        private Vector3 targetPos;
        private float targetYaw;
        private bool hasState;

        public void Apply(JToken token)
        {
            if (token?["x"] == null) return;

            targetPos = new Vector3((float)token["x"], (float)token["y"], (float)token["z"]);
            targetYaw = (float)token["yaw"];

            if (!hasState)
            {
                transform.SetPositionAndRotation(targetPos, Quaternion.Euler(0f, targetYaw, 0f));
                hasState = true;
            }
        }

        private void Update()
        {
            if (!hasState) return;
            float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
            transform.rotation = Quaternion.Euler(0f,
                Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, t), 0f);
        }
    }
}