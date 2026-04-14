using UnityEngine;
using UnityEngine.Rendering;

namespace Landscape.Atmosphere
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AtmosphereController : MonoBehaviour
    {
        private const string DefaultProfilePath = "Atmosphere/AtmosphereProfile_Earth";
        private const float OverlayPaddingLeft = 8.0f;
        private const float OverlayPaddingRight = 8.0f;
        private const float OverlayPaddingTop = 6.0f;
        private static AtmosphereController instance;

        [SerializeField] private AtmosphereProfile profile;
        [SerializeField] private Light mainLight;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private Vector2 debugOverlaySize = new Vector2(384.0f, 96.0f);

        private AtmosphereLutManager lutManager;
        private bool forceRebuild = true;
        private bool forceMultiScatteringRebuild = true;
        private GUIStyle overlayLabelStyle;
        private GUIStyle overlayTitleStyle;

        public static AtmosphereController Instance => instance;
        public RTHandle TransmittanceHandle => lutManager != null ? lutManager.TransmittanceHandle : null;
        public RTHandle MultiScatteringHandle => lutManager != null ? lutManager.MultiScatteringHandle : null;
        public RenderTexture TransmittanceTexture => lutManager != null ? lutManager.TransmittanceTexture : null;
        public RenderTexture MultiScatteringTexture => lutManager != null ? lutManager.MultiScatteringTexture : null;
        public ComputeShader TransmittanceComputeShader => lutManager != null ? lutManager.TransmittanceComputeShader : null;
        public ComputeShader MultiScatteringComputeShader => lutManager != null ? lutManager.MultiScatteringComputeShader : null;
        public int TransmittanceKernelIndex => lutManager != null ? lutManager.TransmittanceKernelIndex : -1;
        public int MultiScatteringKernelIndex => lutManager != null ? lutManager.MultiScatteringKernelIndex : -1;

        private void Reset()
        {
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Atmosphere: multiple AtmosphereController instances found. Disabling duplicate.", this);
                enabled = false;
                return;
            }

            instance = this;
            lutManager ??= new AtmosphereLutManager();
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
            forceRebuild = true;
            forceMultiScatteringRebuild = true;
        }

        private void OnDisable()
        {
            if (instance == this)
                instance = null;

            if (lutManager != null)
                lutManager.Release();
        }

        private void OnValidate()
        {
            LoadDefaultProfileIfNeeded();
            TryAssignMainLight();
            forceRebuild = true;
            forceMultiScatteringRebuild = true;
        }

        public bool TryPrepareForRender(out AtmosphereParameters parameters)
        {
            parameters = default;

            if (!enabled)
                return false;

            LoadDefaultProfileIfNeeded();
            if (profile == null)
                return false;

            lutManager ??= new AtmosphereLutManager();
            parameters = AtmosphereParameters.FromProfile(profile);
            return lutManager.EnsureTransmittanceResources(parameters);
        }

        public bool TryPrepareForMultiScattering(out AtmosphereParameters parameters)
        {
            parameters = default;
            if (!TryPrepareForRender(out parameters))
                return false;

            return lutManager.EnsureMultiScatteringResources(parameters);
        }

        public bool NeedsTransmittanceRebuild(in AtmosphereParameters parameters)
        {
            return forceRebuild || lutManager == null || lutManager.NeedsTransmittanceRebuild(parameters);
        }

        public bool NeedsMultiScatteringRebuild(in AtmosphereParameters parameters)
        {
            return forceMultiScatteringRebuild || lutManager == null || lutManager.NeedsMultiScatteringRebuild(parameters);
        }

        public void RenderTransmittance(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderTransmittance(cmd, parameters);
            lutManager.BindGlobals(cmd, parameters);
            forceRebuild = false;
            forceMultiScatteringRebuild = true;
        }

        public void RenderMultiScattering(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            lutManager ??= new AtmosphereLutManager();
            lutManager.RenderMultiScattering(cmd, parameters);
            lutManager.BindGlobals(cmd, parameters);
            forceMultiScatteringRebuild = false;
        }

        public void BindGlobals(CommandBuffer cmd, in AtmosphereParameters parameters)
        {
            if (lutManager == null)
                return;

            lutManager.BindGlobals(cmd, parameters);
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            RenderTexture texture = TransmittanceTexture;
            RenderTexture multiScatteringTexture = MultiScatteringTexture;
            if (texture == null && multiScatteringTexture == null)
                return;

            EnsureOverlayStyles();

            float width = Mathf.Max(128.0f, debugOverlaySize.x);
            float height = Mathf.Max(32.0f, debugOverlaySize.y);
            float blockHeight = height + 24.0f;
            int blockCount = (texture != null ? 1 : 0) + (multiScatteringTexture != null ? 1 : 0);
            Rect panelRect = new Rect(16.0f, 16.0f, width + OverlayPaddingLeft + OverlayPaddingRight, blockCount * blockHeight + 20.0f);
            GUI.Box(panelRect, GUIContent.none);

            float cursorY = panelRect.y + 8.0f;
            if (texture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, texture, "Atmosphere Transmittance LUT");
            }

            if (multiScatteringTexture != null)
            {
                DrawOverlayTexture(panelRect.x, ref cursorY, width, height, multiScatteringTexture, "Atmosphere Multi-scattering LUT");
            }
        }

        private void DrawOverlayTexture(float panelX, ref float cursorY, float width, float height, RenderTexture texture, string label)
        {
            Rect textureRect = new Rect(
                panelX + OverlayPaddingLeft,
                cursorY + 20.0f,
                width,
                height);
            GUI.Label(new Rect(panelX + 8.0f, cursorY, width, 20.0f), label, overlayTitleStyle);
            GUI.DrawTexture(textureRect, texture, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(textureRect.x, textureRect.yMax + 4.0f, textureRect.width, 18.0f), $"{texture.width}x{texture.height} ARGBHalf", overlayLabelStyle);
            cursorY = textureRect.yMax + 24.0f;
        }

        private void EnsureOverlayStyles()
        {
            if (overlayTitleStyle != null && overlayLabelStyle != null)
                return;

            overlayTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            overlayLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f, 1.0f) }
            };
        }

        private void LoadDefaultProfileIfNeeded()
        {
            if (profile != null)
                return;

            profile = Resources.Load<AtmosphereProfile>(DefaultProfilePath);
        }

        private void TryAssignMainLight()
        {
            if (mainLight != null && mainLight.type == LightType.Directional)
                return;

            if (RenderSettings.sun != null)
            {
                mainLight = RenderSettings.sun;
                return;
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    mainLight = lights[i];
                    return;
                }
            }
        }
    }
}
