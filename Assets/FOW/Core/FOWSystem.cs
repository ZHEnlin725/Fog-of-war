#define ENABLE_COMPUTE_SHADER

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FOW.Core
{
    /// <summary>
    /// Fog of war texture
    /// </summary>
    public class FOWSystem : MonoSingletonTemplate<FOWSystem>
    {
        public Vector3 worldSize;
        public Vector3 worldOrigin;

        public float radiusOffset;

        public int textureWidth = 512, textureHeight = 512;

        public float textureBlendTime = 0.5f;

#if !ENABLE_COMPUTE_SHADER
        [Range(0f, 1f)] public float bufferUpdateInterval = 0.1f;
        public float blendFactor;
#endif

        [Range(1, 10)] public uint blurIterations = 1;

        public bool enableFog = true;

        private Texture texture;

        public Texture sharedTexture
        {
            get
            {
                if (texture == null)
                    UpdateTexture();
                return texture;
            }
        }

#if ENABLE_COMPUTE_SHADER
        private struct FOV
        {
            public int radius;
            public int x;
            public int y;
        }

        private FOV[] FOVCaches;
        [SerializeField] private ComputeShader computeShader;
#else
        private enum State
        {
            Applying,
            UpdateBuffer,
            UpdateTexture,
        }

        private State state;

        private Color32[] tempBuffer;
        private Color32[] renderBuffer;

        private bool threadRunning;
        private Thread updateBufferThread;
        
        private readonly List<IFOV> added = new List<IFOV>();
        private readonly List<IFOV> removed = new List<IFOV>();
#endif

        private readonly List<IFOV> FOVList = new List<IFOV>();

        public override void Init()
        {
#if ENABLE_COMPUTE_SHADER

#if UNITY_EDITOR
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/FOW/Shaders/FOVCompute.compute");
            computeShader = Instantiate(shader);
#else
            //todo 根据实际情况加载computeShader资源
#endif

#else
            if (updateBufferThread != null)
                updateBufferThread.Abort();

            var length = textureWidth * textureHeight;

            tempBuffer = new Color32[length];
            renderBuffer = new Color32[length];

            threadRunning = true;
            updateBufferThread = new Thread(ThreadUpdate);
            updateBufferThread.Start();
#endif
        }

        public void setWorld(Vector3 worldSize, Vector3 worldOrigin)
        {
            this.worldSize = worldSize;
            this.worldOrigin = worldOrigin;
        }

        public void setTexture(int width, int height)
        {
            textureWidth = width;
            textureHeight = height;
        }

        public void Dispose()
        {
#if ENABLE_COMPUTE_SHADER
            if (computeShader != null)
                Destroy(computeShader);
            computeShader = null;
            FOVCaches = null;
#else
            threadRunning = false;

            if (updateBufferThread != null)
            {
                updateBufferThread.Abort();
                updateBufferThread = null;
            }

            tempBuffer = null;
            renderBuffer = null;
#endif


            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }

            FOVList.Clear();
        }

        public void Add(IFOV fov)
        {
#if ENABLE_COMPUTE_SHADER
            FOVList.Add(fov);
            if (FOVCaches == null || FOVList.Count != FOVCaches.Length)
                FOVCaches = new FOV[FOVList.Count];
#else
            added.Add(fov);
#endif
        }

        public void Remove(IFOV fov)
        {
#if ENABLE_COMPUTE_SHADER
            FOVList.Remove(fov);
            if (FOVCaches == null || FOVList.Count != FOVCaches.Length)
                FOVCaches = new FOV[FOVList.Count];
#else
            removed.Add(fov);
#endif
        }

        public void RenderImmediate()
        {
#if !ENABLE_COMPUTE_SHADER
            UpdateBuffer();

            state = State.UpdateTexture;
#endif
            UpdateTexture();

#if !ENABLE_COMPUTE_SHADER
            blendFactor = 1f;
#endif
        }

        private void Update()
        {
#if ENABLE_COMPUTE_SHADER
            UpdateTexture();
#else
            blendFactor = textureBlendTime > 0f ? Mathf.Clamp01(blendFactor + Time.deltaTime / textureBlendTime) : 1f;

            if (state == State.Applying)
            {
                var time = Time.time;
                if (nextUpdate < time)
                {
                    nextUpdate = time + bufferUpdateInterval;
                    state = State.UpdateBuffer;
                }
            }
            else if (state != State.UpdateBuffer)
            {
                UpdateTexture();
            }
#endif
        }

        private void UpdateTexture()
        {
            if (texture == null)
            {
                texture = CreateTexture();
#if !ENABLE_COMPUTE_SHADER
                ((Texture2D) texture).SetPixels32(renderBuffer);
                ((Texture2D) texture).Apply();
                state = State.Applying;
#endif
            }
#if ENABLE_COMPUTE_SHADER
            Compute();
#else
            if (state == State.UpdateTexture)
            {
                ((Texture2D) texture).SetPixels32(renderBuffer);
                ((Texture2D) texture).Apply();
                blendFactor = 0f;
                state = State.Applying;
            }
#endif
        }

        private Texture CreateTexture()
        {
            Texture result = null;
#if ENABLE_COMPUTE_SHADER
            var renderTexture = new RenderTexture(textureWidth, textureHeight, 0)
            {
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                format = RenderTextureFormat.ARGB32,
            };
            renderTexture.Create();
            result = renderTexture;
#else
            var texture2D = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
                {wrapMode = TextureWrapMode.Clamp};
            result = texture2D;
#endif
            return result;
        }

        #region UpdateBuffer Or Compute

#if ENABLE_COMPUTE_SHADER
        private void Compute()
        {
            if (texture == null)
            {
                var renderTexture = new RenderTexture(textureWidth, textureHeight, 0)
                {
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp,
                    format = RenderTextureFormat.ARGB32,
                };
                renderTexture.Create();

                texture = renderTexture;
            }

            if (FOVCaches == null) return;

            // var dateTime = DateTime.Now;
            var updateKernel = computeShader.FindKernel("Update");

            var length = FOVCaches.Length;
            var scaleX = textureWidth / worldSize.x;
            var scaleY = textureHeight / worldSize.z;

            for (int i = 0; i < length; i++)
            {
                var fov = FOVList[i];
                FOVCaches[i] = new FOV
                {
                    x = Mathf.RoundToInt((fov.getPosition().x - worldOrigin.x) * scaleX),
                    y = Mathf.RoundToInt((fov.getPosition().z - worldOrigin.z) * scaleY),
                    radius = Mathf.RoundToInt((fov.getRadius() + radiusOffset) * Mathf.Max(scaleX, scaleY)),
                };
            }

            var FOVBuffer = new ComputeBuffer(length, sizeof(int) * 3);
            FOVBuffer.SetData(FOVCaches);

            computeShader.SetInt("FOVBufferLength", length);
            computeShader.SetInt("TexWidth", textureWidth);
            computeShader.SetInt("TexHeight", textureHeight);
            computeShader.SetBuffer(updateKernel, "FOVBuffer", FOVBuffer);
            computeShader.SetTexture(updateKernel, "Result", texture);

            computeShader.Dispatch(updateKernel, textureWidth / 32, textureHeight / 32, 1);

            var blurKernel = computeShader.FindKernel("Blur");
            computeShader.SetTexture(blurKernel, "Result", texture);
            for (int i = 0; i < blurIterations; i++)
                computeShader.Dispatch(blurKernel, textureWidth / 32, textureHeight / 32, 1);

            FOVBuffer.Release();
            // Debug.Log($"Compute Consume {(DateTime.Now - dateTime).Milliseconds}ms");
        }

#else
        private float elapsed;
        private float nextUpdate;

        private void ThreadUpdate()
        {
            var stopwatch = new Stopwatch();
            while (threadRunning)
            {
                if (state == State.UpdateBuffer)
                {
                    stopwatch.Restart();
                    UpdateBuffer();
                    elapsed = stopwatch.ElapsedMilliseconds * 0.001f;
                    state = State.UpdateTexture;
                    stopwatch.Stop();
                }

                Thread.Sleep(1);
            }

            Debug.LogError("Exit buffer update thread !!!");
        }

        private void UpdateBuffer()
        {
            #region Add Or Remove

            for (int i = added.Count - 1; i >= 0; i--)
            {
                var add = added[i];
                FOVList.Add(add);
                added.RemoveAt(i);
            }

            for (int i = removed.Count - 1; i >= 0; i--)
            {
                var remove = removed[i];
                FOVList.Remove(remove);
                removed.RemoveAt(i);
            }

            #endregion

            var factor = textureBlendTime > 0f
                ? Mathf.Clamp01(blendFactor + elapsed / textureBlendTime)
                : 1f;

            for (int i = 0; i < tempBuffer.Length; i++)
            {
                Color32 lerp;
                if (renderBuffer[i].g < tempBuffer[i].g)
                {
                    lerp = Color32.Lerp(renderBuffer[i], tempBuffer[i], factor);
                    renderBuffer[i].g = lerp.g;
                }

                lerp = Color32.Lerp(tempBuffer[i], new Color32(0, 0, 0, 0), 2 * factor);
                tempBuffer[i].r = lerp.r;
            }

            foreach (var fov in FOVList)
                if (fov.Visible())
                    UpdateVisibleBuffer(fov);

            for (var i = 0; i < blurIterations; i++)
                BlurMarginOfVisible();

            MergeBuffer();
        }

        private void UpdateVisibleBuffer(IFOV fov)
        {
            var radius = fov.getRadius() + radiusOffset;
            var position = fov.getPosition() - worldOrigin;
            
            var scaleX = textureWidth / worldSize.x;
            var scaleY = textureHeight / worldSize.z;
            
            var texRadius = new Vector2(radius * scaleX, radius * scaleY);
            var texPosition = new Vector2(position.x * scaleX, position.z * scaleY);

            var max = new Vector2(texPosition.x + texRadius.x, texPosition.y + texRadius.y);
            var min = new Vector2(texPosition.x - texRadius.x, texPosition.y - texRadius.y);

            for (int y = Mathf.RoundToInt(min.y); y < Mathf.RoundToInt(max.y); y++)
            {
                if (y < 0 || y >= textureHeight) continue;
                var yx = textureWidth * y;
                for (int x = Mathf.RoundToInt(min.x); x < Mathf.RoundToInt(max.x); x++)
                {
                    if (x < 0 || x >= textureWidth) continue;
                    var dx = x - texPosition.x;
                    var dy = y - texPosition.y;
                    if (dx * dx + dy * dy < texRadius.x * texRadius.y)
                        tempBuffer[x + yx].r = 255;
                }
            }
        }

        private void BlurMarginOfVisible()
        {
            for (int y = 0; y < textureHeight; ++y)
            {
                var yx = y * textureWidth;
                var yx0 = Mathf.Max(0, y - 1) * textureWidth;
                var yx1 = Mathf.Min(textureHeight - 1, y + 1) * textureWidth;
                for (int x = 0; x < textureWidth; ++x)
                {
                    var x0 = Mathf.Max(0, x - 1);
                    var x1 = Mathf.Min(textureWidth - 1, x + 1);

                    var index = x + yx;
                    int val = tempBuffer[index].r;

                    val += tempBuffer[x0 + yx].r;
                    val += tempBuffer[x1 + yx].r;
                    val += tempBuffer[x + yx0].r;
                    val += tempBuffer[x + yx1].r;

                    val += tempBuffer[x0 + yx0].r;
                    val += tempBuffer[x1 + yx0].r;
                    val += tempBuffer[x0 + yx1].r;
                    val += tempBuffer[x1 + yx1].r;

                    var color = tempBuffer[index];
                    color.r = (byte) (val / 9);
                    tempBuffer[index] = color;
                }
            }
        }

        private void MergeBuffer()
        {
            for (int i = 0; i < tempBuffer.Length; i++)
            {
                // green通道表示探索过的区域
                if (tempBuffer[i].g < tempBuffer[i].r)
                    tempBuffer[i].g = tempBuffer[i].r;

                // red通道表示当前所在的区域
                renderBuffer[i].r = tempBuffer[i].r;
            }
        }
#endif

        #endregion

        private void OnApplicationQuit()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}