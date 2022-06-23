#define ENABLE_COMPUTE_SHADER
using UnityEngine;

namespace FOW.Core
{
    /// <summary>
    /// Fog of war render
    /// </summary>
    public class FOWRender : MonoBehaviour
    {
        public Color exploredColor = new Color(0f, 0f, 0f, 200f / 255f);
        public Color unexploredColor = new Color(0f, 0f, 0f, 250f / 255f);

        [SerializeField] private Material material;

        private FOWSystem FOWSystem;

        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int BlendFactor = Shader.PropertyToID("_BlendFactor");

        private static readonly int Explored = Shader.PropertyToID("_Explored");
        private static readonly int Unexplored = Shader.PropertyToID("_Unexplored");

        private bool enable;

        public void setActive(bool active) =>
            gameObject.SetActive(active);

        public void setFOWSystem(FOWSystem system)
        {
            FOWSystem = system;
            var meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
                material = meshRenderer.sharedMaterial;

            enable = material != null && system != null;
        }

        private void OnWillRenderObject()
        {
            if (!enable) return;
            if (material != null && FOWSystem.sharedTexture != null)
            {
                material.SetTexture(MainTex, FOWSystem.sharedTexture);
#if !ENABLE_COMPUTE_SHADER
                material.SetFloat(BlendFactor, fowTex.blendFactor);
#endif
                material.SetColor(Explored, exploredColor);
                material.SetColor(Unexplored, FOWSystem.enableFog ? unexploredColor : exploredColor);
            }
        }
    }
}