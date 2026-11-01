using UnityEngine;

namespace MultiplayerShooting.Gameplay.UI
{
    public class CrossHair : MonoBehaviour
    {
        [SerializeField] private float size = 12f;
        [SerializeField] private float thickness = 2f;
        [SerializeField] private float gap = 4f;
        [SerializeField] private Color color = Color.white;

        private void OnGUI()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.color = color;

            GUI.DrawTexture(new Rect(cx - gap - size, cy - thickness * 0.5f, size, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - thickness * 0.5f, size, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - gap - size, thickness, size), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + gap, thickness, size), Texture2D.whiteTexture);
        }
    }
}