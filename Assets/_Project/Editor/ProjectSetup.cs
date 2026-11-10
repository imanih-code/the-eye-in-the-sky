using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EyeSky.EditorTools
{
    /// <summary>Deja el pipeline listo: sin suavizado, sin HDR, con sombras duras y el pase de contorno.</summary>
    public static class ProjectSetup
    {
        const string SettingsFolder = "Assets/_Project/Settings";
        const string RendererPath = SettingsFolder + "/EyeRenderer.asset";
        const string PipelinePath = SettingsFolder + "/EyeUrp.asset";
        const string OutlinePath = SettingsFolder + "/M_FeedOutline.mat";

        public static void Run()
        {
            Directory.CreateDirectory(SettingsFolder);

            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (outline == null)
            {
                outline = new Material(Shader.Find("EyeSky/FeedOutline"));
                AssetDatabase.CreateAsset(outline, OutlinePath);
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                AddOutlineFeature(renderer, outline);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 90f;
            pipeline.shadowCascadeCount = 1;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            var currentQuality = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentQuality, false);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.productName = "The Eye In The Sky";
            PlayerSettings.companyName = "Bliss Bit Games";
            AssetDatabase.SaveAssets();
        }

        static void AddOutlineFeature(UniversalRendererData renderer, Material outline)
        {
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "FeedOutline";
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.passMaterial = outline;
            feature.passIndex = 0;

            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

            var serialized = new SerializedObject(renderer);
            var features = serialized.FindProperty("m_RendererFeatures");
            var map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }
    }
}
