using MultiplayerShooting.Interfaces;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MultiplayerShooting.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class LocalPlayer : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float sendRate = 20f;

        CharacterController cc;
        INetworkService networkService;
        private float pitch, yVel, nextSend;

        public void Init(INetworkService service) => networkService = service;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            // look
            transform.Rotate(0f, Input.GetAxis("Mouse X") * lookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -85f, 85f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // move
            Vector3 dir = transform.right * Input.GetAxisRaw("Horizontal")
                        + transform.forward * Input.GetAxisRaw("Vertical");
            yVel = cc.isGrounded ? -1f : yVel + Physics.gravity.y * Time.deltaTime;
            cc.Move((dir.normalized * moveSpeed + Vector3.up * yVel) * Time.deltaTime);

            //send Data
            if (networkService != null && networkService.IsConnected && Time.time >= nextSend)
            {
                nextSend = Time.time + 1f / sendRate;
                Vector3 p = transform.position;

                networkService.Send(
        new JObject
        {
            ["t"] = "state",
            ["data"] = new JObject
            {
                ["x"] = p.x,
                ["y"] = p.y,
                ["z"] = p.z,
                ["yaw"] = transform.eulerAngles.y
            }
        }
                );
            }
        }
    }
}