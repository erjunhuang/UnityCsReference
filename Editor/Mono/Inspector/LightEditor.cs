// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using Object = UnityEngine.Object;

using UnityEditor.AnimatedValues;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Linq;
using System.Collections.Generic;

namespace UnityEditor
{
    [CustomEditor(typeof(Light))]
    [CanEditMultipleObjects]
    public class LightEditor : Editor
    {
        public sealed class Settings
        {
            private SerializedObject m_SerializedObject;

            public SerializedProperty lightType { get; private set; }
            public SerializedProperty lightShape { get; private set; }
            public SerializedProperty range { get; private set; }
            public SerializedProperty spotAngle { get; private set; }
            public SerializedProperty innerSpotAngle { get; private set; }
            public SerializedProperty cookieSize { get; private set; }
            public SerializedProperty color { get; private set; }
            public SerializedProperty intensity { get; private set; }
            public SerializedProperty bounceIntensity { get; private set; }
            public SerializedProperty colorTemperature { get; private set; }
            public SerializedProperty useColorTemperature { get; private set; }
            public SerializedProperty cookieProp { get; private set; }
            public SerializedProperty shadowsType { get; private set; }
            public SerializedProperty shadowsStrength { get; private set; }
            public SerializedProperty shadowsResolution { get; private set; }
            public SerializedProperty shadowsBias { get; private set; }
            public SerializedProperty shadowsNormalBias { get; private set; }
            public SerializedProperty shadowsNearPlane { get; private set; }
            public SerializedProperty halo { get; private set; }
            public SerializedProperty flare { get; private set; }
            public SerializedProperty renderMode { get; private set; }
            public SerializedProperty cullingMask { get; private set; }
            public SerializedProperty renderingLayerMask { get; private set; }
            public SerializedProperty lightmapping { get; private set; }
            public SerializedProperty areaSizeX { get; private set; }
            public SerializedProperty areaSizeY { get; private set; }
            public SerializedProperty bakedShadowRadiusProp { get; private set; }
            public SerializedProperty bakedShadowAngleProp { get; private set; }

            Texture2D m_KelvinGradientTexture;
            const float kMinKelvin = 1000f;
            const float kMaxKelvin = 20000f;
            const float kSliderPower = 2f;

            // should have the same int values as corresponding shape in LightType
            private enum AreaLightShape
            {
                None = 0,
                Rectangle = 3,
                Disc = 4
            }

            public Settings(SerializedObject so)
            {
                m_SerializedObject = so;
            }

            private static class Styles
            {
                public static readonly GUIContent Type = EditorGUIUtility.TrTextContent("Type", "Specifies the current type of light. Possible types are Directional, Spot, Point, and Area lights.");
                public static readonly GUIContent Shape = EditorGUIUtility.TrTextContent("Shape", "Specifies the shape of the Area light. Possible types are Rectangle and Disc.");
                public static readonly GUIContent Range = EditorGUIUtility.TrTextContent("Range", "Controls how far the light is emitted from the center of the object.");
                public static readonly GUIContent SpotAngle = EditorGUIUtility.TrTextContent("Spot Angle", "Controls the angle in degrees at the base of a Spot light's cone.");
                public static readonly GUIContent InnerOuterSpotAngle = EditorGUIUtility.TrTextContent("Inner / Outer Spot Angle", "Controls the inner and outer angles in degrees, at the base of a Spot light's cone.");
                public static readonly GUIContent Color = EditorGUIUtility.TrTextContent("Color", "Controls the color being emitted by the light.");
                public static readonly GUIContent UseColorTemperature = EditorGUIUtility.TrTextContent("Use color temperature mode", "Choose between RGB and temperature mode for light's color.");
                public static readonly GUIContent ColorFilter = EditorGUIUtility.TrTextContent("Filter", "A colored gel can be put in front of the light source to tint the light.");
                public static readonly GUIContent ColorTemperature = EditorGUIUtility.TrTextContent("Temperature", "Also known as CCT (Correlated color temperature). The color temperature of the electromagnetic radiation emitted from an ideal black body is defined as its surface temperature in Kelvin. White is 6500K");
                public static readonly GUIContent Intensity = EditorGUIUtility.TrTextContent("Intensity", "Controls the brightness of the light. Light color is multiplied by this value.");
                public static readonly GUIContent LightmappingMode = EditorGUIUtility.TrTextContent("Mode", "Specifies the light mode used to determine if and how a light will be baked. Possible modes are Baked, Mixed, and Realtime.");
                public static readonly GUIContent LightBounceIntensityRealtimeGISupport = EditorGUIUtility.TrTextContent("Indirect Multiplier", "Determines the intensity of indirect light being contributed to the scene. Has no effect when both Realtime and Baked Global Illumination are disabled. If this value is 0, Realtime lights to be removed from realtime global illumination and Baked and Mixed lights to no longer emit indirect lighting.");
                public static readonly GUIContent LightBounceIntensity = EditorGUIUtility.TrTextContent("Indirect Multiplier", "Determines the intensity of indirect light being contributed to to the scene. Has no effect when Baked Global Illumination is disabled. If this value is 0, Baked and Mixed lights no longer emit indirect lighting.");
                public static readonly GUIContent ShadowType = EditorGUIUtility.TrTextContent("Shadow Type", "Specifies whether Hard Shadows, Soft Shadows, or No Shadows will be cast by the light.");
                public static readonly GUIContent CastShadows = EditorGUIUtility.TrTextContent("Cast Shadows", "Specifies whether Soft Shadows or No Shadows will be cast by the light.");
                //realtime
                public static readonly GUIContent ShadowRealtimeSettings = EditorGUIUtility.TrTextContent("Realtime Shadows", "Settings for realtime direct shadows.");
                public static readonly GUIContent ShadowStrength = EditorGUIUtility.TrTextContent("Strength", "Controls how dark the shadows cast by the light will be.");
                public static readonly GUIContent ShadowResolution = EditorGUIUtility.TrTextContent("Resolution", "Controls the rendered resolution of the shadow maps. A higher resolution will increase the fidelity of shadows at the cost of GPU performance and memory usage.");
                public static readonly GUIContent ShadowBias = EditorGUIUtility.TrTextContent("Bias", "Controls the distance at which the shadows will be pushed away from the light. Useful for avoiding false self-shadowing artifacts.");
                public static readonly GUIContent ShadowNormalBias = EditorGUIUtility.TrTextContent("Normal Bias", "Controls distance at which the shadow casting surfaces will be shrunk along the surface normal. Useful for avoiding false self-shadowing artifacts.");
                public static readonly GUIContent ShadowNearPlane = EditorGUIUtility.TrTextContent("Near Plane", "Controls the value for the near clip plane when rendering shadows. Currently clamped to 0.1 units or 1% of the lights range property, whichever is lower.");
                //baked
                public static readonly GUIContent BakedShadowRadius = EditorGUIUtility.TrTextContent("Baked Shadow Radius", "Controls the amount of artificial softening applied to the edges of shadows cast by the Point or Spot light.");
                public static readonly GUIContent BakedShadowAngle = EditorGUIUtility.TrTextContent("Baked Shadow Angle", "Controls the amount of artificial softening applied to the edges of shadows cast by directional lights.");

                public static readonly GUIContent Cookie = EditorGUIUtility.TrTextContent("Cookie", "Specifies the Texture mask to cast shadows, create silhouettes, or patterned illumination for the light.");
                public static readonly GUIContent CookieSize = EditorGUIUtility.TrTextContent("Size", "Controls the size of the cookie mask currently assigned to the light.");
                public static readonly GUIContent CookieTexture = EditorGUIUtility.TrTextContent("Cookie", "Texture to use for the light cookie.");

                public static readonly GUIContent DrawHalo = EditorGUIUtility.TrTextContent("Draw Halo", "When enabled, draws a spherical halo of light with a radius equal to the lights range value.");
                public static readonly GUIContent Flare = EditorGUIUtility.TrTextContent("Flare", "Specifies the flare object to be used by the light to render lens flares in the scene.");
                public static readonly GUIContent RenderMode = EditorGUIUtility.TrTextContent("Render Mode", "Specifies the importance of the light which impacts lighting fidelity and performance. Options are Auto, Important, and Not Important. This only affects Forward Rendering.");
                public static readonly GUIContent CullingMask = EditorGUIUtility.TrTextContent("Culling Mask", "Specifies which layers will be affected or excluded from the light's effect on objects in the scene.");
                public static readonly GUIContent RenderingLayerMask = EditorGUIUtility.TrTextContent("Rendering Layer Mask", "Mask that can be used with SRP when drawing shadows to filter renderers outside of the normal layering system.");

                public static readonly GUIContent AreaWidth = EditorGUIUtility.TrTextContent("Width", "Controls the width in units of the area light.");
                public static readonly GUIContent AreaHeight = EditorGUIUtility.TrTextContent("Height", "Controls the height in units of the area light.");
                public static readonly GUIContent AreaRadius = EditorGUIUtility.TrTextContent("Radius", "Controls the radius in units of the disc area light.");

                public static readonly GUIContent BakingWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. Enable Baked Global Illumination to use Mixed or Baked light modes.");
                public static readonly GUIContent IndirectBounceShadowWarning = EditorGUIUtility.TrTextContent("Realtime indirect bounce shadowing is only supported for Directional lights.");
                public static readonly GUIContent CookieSpotRepeatWarning = EditorGUIUtility.TrTextContent("Cookie textures for spot lights should be set to clamp, not repeat, to avoid artifacts.");
                public static readonly GUIContent CookieNotEnabledWarning = EditorGUIUtility.TrTextContent("Cookie support for baked lights is not enabled. Please enable it in Project Settings > Editor > Enable baked cookies support");
                public static readonly GUIContent CookieNotEnabledInfo = EditorGUIUtility.TrTextContent("Cookie support for mixed lights is not enabled for indirect lighting. You can enable it in Project Settings > Editor > Enable baked cookies support");
                public static readonly GUIContent CookieSpotDirectionalTextureWarning = EditorGUIUtility.TrTextContent("Spot and directional light cookie textures must be 2D.");
                public static readonly GUIContent CookiePointCubemapTextureWarning = EditorGUIUtility.TrTextContent("Cookie support for baked lights is not enabled. Please enable it in Project Settings > Editor > Enable baked cookies support");
                public static readonly GUIContent MixedUnsupportedWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. The current render pipeline doesn't support Mixed mode and/or any of the lighting modes.");
                public static readonly GUIContent BakedUnsupportedWarning = EditorGUIUtility.TrTextContent("Light mode is currently overridden to Realtime mode. The current render pipeline doesn't support Baked mode.");

                public static readonly GUIContent[] LightmapBakeTypeTitles = { EditorGUIUtility.TrTextContent("Realtime"), EditorGUIUtility.TrTextContent("Mixed"), EditorGUIUtility.TrTextContent("Baked") };
                public static readonly int[] LightmapBakeTypeValues = { (int)LightmapBakeType.Realtime, (int)LightmapBakeType.Mixed, (int)LightmapBakeType.Baked };

                public static readonly GUIContent[] LightTypeTitles = { EditorGUIUtility.TrTextContent("Spot"), EditorGUIUtility.TrTextContent("Directional"), EditorGUIUtility.TrTextContent("Point"), EditorGUIUtility.TrTextContent("Area (baked only)") };
                public static readonly int[] LightTypeValues = { (int)LightType.Spot, (int)LightType.Directional, (int)LightType.Point, (int)LightType.Rectangle };

                public static readonly GUIContent[] AreaLightShapeTitles = { EditorGUIUtility.TrTextContent("Rectangle"), EditorGUIUtility.TrTextContent("Disc") };
                public static readonly int[] AreaLightShapeValues = { (int)AreaLightShape.Rectangle, (int)AreaLightShape.Disc };
            }

            public bool isRealtime { get { return lightmapping.intValue == (int)LightmapBakeType.Realtime; } }
            public bool isMixed { get { return lightmapping.intValue == (int)LightmapBakeType.Mixed; } }
            public bool isCompletelyBaked { get { return lightmapping.intValue == (int)LightmapBakeType.Baked; } }
            public bool isBakedOrMixed { get { return !isRealtime; } }
            public bool isAreaLightType { get { return lightType.intValue == (int)LightType.Rectangle || lightType.intValue == (int)LightType.Disc; } }

            internal bool typeIsSame { get { return !lightType.hasMultipleDifferentValues; } }
            internal bool shadowTypeIsSame { get { return !shadowsType.hasMultipleDifferentValues; } }
            internal bool lightmappingTypeIsSame { get { return !lightmapping.hasMultipleDifferentValues; } }

            internal bool isPrefabAsset
            {
                get
                {
                    if (m_SerializedObject == null || m_SerializedObject.targetObject == null)
                        return false;

                    return PrefabUtility.IsPartOfPrefabAsset(m_SerializedObject.targetObject);
                }
            }

            public Light light { get { return m_SerializedObject.targetObject as Light; } }
            public Texture cookie { get { return cookieProp.objectReferenceValue as Texture; } }

            internal bool showMixedModeUnsupportedWarning { get { return !isPrefabAsset && isMixed && lightmappingTypeIsSame && !SupportedRenderingFeatures.IsLightmapBakeTypeSupported(LightmapBakeType.Mixed); } }
            internal bool showBakedModeUnsupportedWarning { get { return !isPrefabAsset && isCompletelyBaked && lightmappingTypeIsSame && !SupportedRenderingFeatures.IsLightmapBakeTypeSupported(LightmapBakeType.Baked); } }

            internal bool showBounceWarning
            {
                get
                {
                    return typeIsSame && (light.type == LightType.Point || light.type == LightType.Spot) &&
                        lightmappingTypeIsSame && isRealtime && !bounceIntensity.hasMultipleDifferentValues && bounceIntensity.floatValue > 0.0F;
                }
            }
            internal bool showBakingWarning { get { return !isPrefabAsset && !Lightmapping.GetLightingSettingsOrDefaultsFallback().bakedGI && lightmappingTypeIsSame && isBakedOrMixed; } }

            internal bool showCookieSpotRepeatWarning
            {
                get
                {
                    RenderPipelineAsset srpAsset = GraphicsSettings.currentRenderPipeline;
                    bool usingSRP = srpAsset != null;

                    return typeIsSame && light.type == LightType.Spot &&
                        !cookieProp.hasMultipleDifferentValues && cookie && cookie.wrapMode != TextureWrapMode.Clamp
                        && !usingSRP;
                }
            }

            internal bool showCookieNotEnabledWarning
            {
                get
                {
                    return typeIsSame && isCompletelyBaked && !cookieProp.hasMultipleDifferentValues && cookie && !EditorSettings.enableCookiesInLightmapper;
                }
            }

            internal bool showCookieNotEnabledInfo
            {
                get
                {
                    return typeIsSame && isMixed && !cookieProp.hasMultipleDifferentValues && cookie && !EditorSettings.enableCookiesInLightmapper;
                }
            }

            public void OnEnable()
            {
                lightType = m_SerializedObject.FindProperty("m_Type");
                lightShape = m_SerializedObject.FindProperty("m_Shape");
                range = m_SerializedObject.FindProperty("m_Range");
                spotAngle = m_SerializedObject.FindProperty("m_SpotAngle");
                innerSpotAngle = m_SerializedObject.FindProperty("m_InnerSpotAngle");
                cookieSize = m_SerializedObject.FindProperty("m_CookieSize");
                color = m_SerializedObject.FindProperty("m_Color");
                intensity = m_SerializedObject.FindProperty("m_Intensity");
                bounceIntensity = m_SerializedObject.FindProperty("m_BounceIntensity");
                colorTemperature = m_SerializedObject.FindProperty("m_ColorTemperature");
                useColorTemperature = m_SerializedObject.FindProperty("m_UseColorTemperature");
                cookieProp = m_SerializedObject.FindProperty("m_Cookie");
                shadowsType = m_SerializedObject.FindProperty("m_Shadows.m_Type");
                shadowsStrength = m_SerializedObject.FindProperty("m_Shadows.m_Strength");
                shadowsResolution = m_SerializedObject.FindProperty("m_Shadows.m_Resolution");
                shadowsBias = m_SerializedObject.FindProperty("m_Shadows.m_Bias");
                shadowsNormalBias = m_SerializedObject.FindProperty("m_Shadows.m_NormalBias");
                shadowsNearPlane = m_SerializedObject.FindProperty("m_Shadows.m_NearPlane");
                halo = m_SerializedObject.FindProperty("m_DrawHalo");
                flare = m_SerializedObject.FindProperty("m_Flare");
                renderMode = m_SerializedObject.FindProperty("m_RenderMode");
                cullingMask = m_SerializedObject.FindProperty("m_CullingMask");
                renderingLayerMask = m_SerializedObject.FindProperty("m_RenderingLayerMask");
                lightmapping = m_SerializedObject.FindProperty("m_Lightmapping");
                areaSizeX = m_SerializedObject.FindProperty("m_AreaSize.x");
                areaSizeY = m_SerializedObject.FindProperty("m_AreaSize.y");
                bakedShadowRadiusProp = m_SerializedObject.FindProperty("m_ShadowRadius");
                bakedShadowAngleProp = m_SerializedObject.FindProperty("m_ShadowAngle");

                if (m_KelvinGradientTexture == null)
                    m_KelvinGradientTexture = CreateKelvinGradientTexture("KelvinGradientTexture", 300, 16, kMinKelvin, kMaxKelvin);
            }

            public void OnDestroy()
            {
                if (m_KelvinGradientTexture != null)
                    DestroyImmediate(m_KelvinGradientTexture);
            }

            static Texture2D CreateKelvinGradientTexture(string name, int width, int height, float minKelvin, float maxKelvin)
            {
                // The texture is draw with a simple internal-GUITexture shader that don't perform any gamma correction
                // so we need to provide value for color temperature texture in gamma space and use a linear format for the texture
                var texture = new Texture2D(width, height, TextureFormat.ARGB32, false, true)
                {
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[width * height];

                float mappedMax = Mathf.Pow(maxKelvin, 1f / kSliderPower);
                float mappedMin = Mathf.Pow(minKelvin, 1f / kSliderPower);

                for (int i = 0; i < width; i++)
                {
                    float pixelfrac = i / (float)(width - 1);
                    float mappedValue = (mappedMax - mappedMin) * pixelfrac + mappedMin;
                    float kelvin = Mathf.Pow(mappedValue, kSliderPower);
                    Color kelvinColor = Mathf.CorrelatedColorTemperatureToRGB(kelvin);
                    for (int j = 0; j < height; j++)
                        pixels[j * width + i] = kelvinColor.gamma;
                }

                texture.SetPixels32(pixels);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Apply();
                return texture;
            }

            public void Update()
            {
                m_SerializedObject.Update();
            }

            public void DrawLightType()
            {
                // To the user, we will only display it as a area light, but under the hood, we have Rectangle and Disc. This is not to confuse people
                // who still use our legacy light inspector.

                int selectedLightType = lightType.intValue;
                int selectedShape = isAreaLightType ? lightType.intValue : (int)AreaLightShape.None;

                // Handle all lights that are not in the default set
                if (!Styles.LightTypeValues.Contains(lightType.intValue))
                {
                    if (lightType.intValue == (int)LightType.Disc)
                    {
                        selectedLightType = (int)LightType.Rectangle;
                        selectedShape = (int)AreaLightShape.Disc;
                    }
                }

                var lightTypeRect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(lightTypeRect, Styles.Type, lightType);
                EditorGUI.BeginChangeCheck();

                int type = EditorGUI.IntPopup(lightTypeRect, Styles.Type, selectedLightType, Styles.LightTypeTitles, Styles.LightTypeValues);

                if (EditorGUI.EndChangeCheck())
                {
                    AnnotationUtility.SetGizmosDirty();
                    lightType.intValue = type;
                }
                EditorGUI.EndProperty();

                if (isAreaLightType && selectedShape != (int)AreaLightShape.None)
                {
                    var lightShapeRect = EditorGUILayout.GetControlRect();
                    EditorGUI.BeginProperty(lightShapeRect, Styles.Shape, lightType);
                    EditorGUI.BeginChangeCheck();
                    int shape = EditorGUI.IntPopup(lightShapeRect, Styles.Shape, selectedShape, Styles.AreaLightShapeTitles, Styles.AreaLightShapeValues);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(light, "Adjust Light Shape");
                        lightType.intValue = shape;
                    }
                    EditorGUI.EndProperty();
                }
            }

            public void DrawRange()
            {
                EditorGUILayout.PropertyField(range, Styles.Range);
            }

            [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
            [Obsolete("showAreaOptions argument for DrawRange(showAreaOptions) has been removed. Use DrawRange() instead (UnityUpgradable).")]
            public void DrawRange(bool showAreaOptions)
            {
                DrawRange();
            }

            public void DrawSpotAngle()
            {
                EditorGUILayout.Slider(spotAngle, 1f, 179f, Styles.SpotAngle);
            }

            public void DrawInnerAndOuterSpotAngle()
            {
                float textFieldWidth = 45f;

                float min = innerSpotAngle.floatValue;
                float max = spotAngle.floatValue;

                var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                // This widget is a little bit of a special case.
                // The right hand side of the min max slider will control the reset of the max value
                // The left hand side of the min max slider will control the reset of the min value
                // The label itself will not have a right click and reset value.

                rect = EditorGUI.PrefixLabel(rect, Styles.InnerOuterSpotAngle);
                EditorGUI.BeginProperty(new Rect(rect) { width = rect.width * 0.5f }, Styles.InnerOuterSpotAngle, innerSpotAngle);
                EditorGUI.BeginProperty(new Rect(rect) { xMin = rect.x + rect.width * 0.5f }, GUIContent.none, spotAngle);

                var minRect = new Rect(rect) { width = textFieldWidth };
                var maxRect = new Rect(rect) { xMin = rect.xMax - textFieldWidth };
                var sliderRect = new Rect(rect) { xMin = minRect.xMax + EditorGUI.kSpacing, xMax = maxRect.xMin - EditorGUI.kSpacing };
                EditorGUI.DelayedFloatField(minRect,  innerSpotAngle, GUIContent.none);

                EditorGUI.BeginChangeCheck();
                EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, 0f, 179f);

                if (EditorGUI.EndChangeCheck())
                {
                    innerSpotAngle.floatValue = min;
                    spotAngle.floatValue = max;
                }

                EditorGUI.DelayedFloatField(maxRect,  spotAngle, GUIContent.none);

                EditorGUI.EndProperty();
                EditorGUI.EndProperty();
            }

            public void DrawArea()
            {
                if (lightType.intValue == (int)LightType.Rectangle)
                {
                    EditorGUILayout.PropertyField(areaSizeX, Styles.AreaWidth);
                    EditorGUILayout.PropertyField(areaSizeY, Styles.AreaHeight);
                }
                else if (lightType.intValue == (int)LightType.Disc)
                {
                    EditorGUILayout.PropertyField(areaSizeX, Styles.AreaRadius);
                }
            }

            public void DrawColor()
            {
                if (GraphicsSettings.lightsUseLinearIntensity && GraphicsSettings.lightsUseColorTemperature)
                {
                    EditorGUILayout.PropertyField(useColorTemperature, Styles.UseColorTemperature);
                    if (useColorTemperature.boolValue)
                    {
                        EditorGUILayout.LabelField(Styles.Color);
                        EditorGUI.indentLevel += 1;
                        EditorGUILayout.PropertyField(color, Styles.ColorFilter);
                        EditorGUILayout.SliderWithTexture(Styles.ColorTemperature, colorTemperature, kMinKelvin, kMaxKelvin, kSliderPower, m_KelvinGradientTexture);
                        EditorGUI.indentLevel -= 1;
                    }
                    else
                        EditorGUILayout.PropertyField(color, Styles.Color);
                }
                else
                    EditorGUILayout.PropertyField(color, Styles.Color);
            }

            void OnLightmappingItemSelected(object userData)
            {
                lightmapping.intValue = (int)userData;
                m_SerializedObject.ApplyModifiedProperties();
            }

            public void DrawLightmapping()
            {
                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(rect, Styles.LightmappingMode, lightmapping);
                rect = EditorGUI.PrefixLabel(rect, Styles.LightmappingMode);

                int index = Math.Max(0, Array.IndexOf(Styles.LightmapBakeTypeValues, lightmapping.intValue));

                if (EditorGUI.DropdownButton(rect, Styles.LightmapBakeTypeTitles[index], FocusType.Passive))
                {
                    var menu = new GenericMenu();

                    for (int i = 0; i < Styles.LightmapBakeTypeValues.Length; i++)
                    {
                        int value = Styles.LightmapBakeTypeValues[i];
                        bool selected = (lightmappingTypeIsSame && (value == lightmapping.intValue));

                        if (((value == (int)LightmapBakeType.Mixed) || (value == (int)LightmapBakeType.Baked)) &&
                            ((!SupportedRenderingFeatures.IsLightmapBakeTypeSupported((LightmapBakeType)value) || !Lightmapping.GetLightingSettingsOrDefaultsFallback().bakedGI) && !isPrefabAsset))
                        {
                            menu.AddDisabledItem(Styles.LightmapBakeTypeTitles[i], selected);
                        }
                        else
                        {
                            menu.AddItem(Styles.LightmapBakeTypeTitles[i], selected, OnLightmappingItemSelected, value);
                        }
                    }
                    menu.DropDown(rect);
                }
                EditorGUI.EndProperty();

                // first make sure that the modes arent unsupported, then unenabled
                if (showMixedModeUnsupportedWarning)
                    EditorGUILayout.HelpBox(Styles.MixedUnsupportedWarning.text, MessageType.Warning);
                else if (showBakedModeUnsupportedWarning)
                    EditorGUILayout.HelpBox(Styles.BakedUnsupportedWarning.text, MessageType.Warning);
                else if (showBakingWarning)
                    EditorGUILayout.HelpBox(Styles.BakingWarning.text, MessageType.Warning);
            }

            internal void CheckLightmappingConsistency()
            {
                //Built-in render-pipeline only support baked area light, enforce it as this inspector is the built-in one.
                if (isAreaLightType && lightmapping.intValue != (int)LightmapBakeType.Baked)
                {
                    lightmapping.intValue = (int)LightmapBakeType.Baked;
                    m_SerializedObject.ApplyModifiedProperties();
                }
            }

            public void DrawIntensity()
            {
                EditorGUILayout.PropertyField(intensity, Styles.Intensity);
            }

            public void DrawBounceIntensity()
            {
                if (SupportedRenderingFeatures.IsLightmapBakeTypeSupported(LightmapBakeType.Realtime))
                {
                    EditorGUILayout.PropertyField(bounceIntensity, Styles.LightBounceIntensityRealtimeGISupport);

                    // No shadowing of indirect warning.
                    if (showBounceWarning)
                    {
                        EditorGUILayout.HelpBox(Styles.IndirectBounceShadowWarning.text, MessageType.Warning);
                    }
                }
                else
                {
                    // no realtime gi support, no need to show this property
                    if (lightmapping.intValue != (int)LightmapBakeType.Realtime)
                        EditorGUILayout.PropertyField(bounceIntensity, Styles.LightBounceIntensity);
                }
            }

            Object TextureValidator(Object[] references, System.Type objType, SerializedProperty property, EditorGUI.ObjectFieldValidatorOptions options)
            {
                LightType lightTypeInfo = (LightType)lightType.intValue;
                Object assetTexture = null;
                foreach (Object assetObject in references)
                {
                    if (assetObject is Texture texture)
                    {
                        if (assetTexture == null)
                        {
                            assetTexture = texture;
                        }

                        switch (lightTypeInfo)
                        {
                            case LightType.Spot:
                            case LightType.Directional:
                                if (texture is Texture2D)
                                    return assetObject;
                                break;

                            case LightType.Point:
                                if (texture is Cubemap)
                                    return assetObject;
                                break;

                            default:
                                if (texture is Texture)
                                    return assetObject;
                                break;
                        }
                    }
                }

                if (assetTexture != null && typeof(RenderTexture).IsAssignableFrom(assetTexture.GetType()))
                {
                    return assetTexture;
                }
                return null;
            }

            void TexturePropertyBody(Rect position, SerializedProperty prop, LightType cookieLightType)
            {
                EditorGUI.BeginChangeCheck();
                int controlID = GUIUtility.GetControlID(12354, FocusType.Keyboard, position);

                Type type = null;
                switch (cookieLightType)
                {
                    case LightType.Spot:
                    case LightType.Directional:
                        type = typeof(Texture2D);
                        break;

                    case LightType.Point:
                        type = typeof(Cubemap);
                        break;

                    default:
                        type = typeof(Texture);
                        break;
                }

                var newValue = EditorGUI.DoObjectField(position, position, controlID, prop.objectReferenceValue, prop.objectReferenceValue, type, TextureValidator, false, typeof(RenderTexture)) as Texture;

                if (EditorGUI.EndChangeCheck())
                    prop.objectReferenceValue = newValue;
            }

            public void DrawCookieProperty(SerializedProperty cookieProperty, GUIContent content, LightType cookieLightType)
            {
                Rect controlRect = EditorGUILayout.GetControlRect();
                Rect thumbRect, labelRect;
                EditorGUI.GetRectsForMiniThumbnailField(controlRect, out thumbRect, out labelRect);
                EditorGUI.HandlePrefixLabel(controlRect, labelRect, content, 0, EditorStyles.label);
                TexturePropertyBody(thumbRect, cookieProperty, cookieLightType);
            }

            public void DrawCookie()
            {
                // Don't draw cookie texture UI for area lights as cookies are not supported by them (except by HDRP, but they handle their own UI drawing logic)
                if (isAreaLightType)
                    return;

                DrawCookieProperty(cookieProp, Styles.CookieTexture, (LightType)lightType.intValue);

                if (showCookieSpotRepeatWarning)
                {
                    // warn on spotlights if the cookie is set to repeat
                    EditorGUILayout.HelpBox(Styles.CookieSpotRepeatWarning.text, MessageType.Warning);
                }

                if (showCookieNotEnabledWarning)
                {
                    // warn if cookie support is not enabled for baked lights
                    EditorGUILayout.HelpBox(Styles.CookieNotEnabledWarning.text, MessageType.Warning);
                }

                if (showCookieNotEnabledInfo)
                {
                    // info if cookie support is not enabled for mixed lights
                    EditorGUILayout.HelpBox(Styles.CookieNotEnabledInfo.text, MessageType.Info);
                }
            }

            public void DrawCookieSize()
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(cookieSize, Styles.CookieSize);
                EditorGUI.indentLevel--;
            }

            public void DrawHalo()
            {
                EditorGUILayout.PropertyField(halo, Styles.DrawHalo);
            }

            public void DrawFlare()
            {
                EditorGUILayout.PropertyField(flare, Styles.Flare);
            }

            public void DrawRenderMode()
            {
                EditorGUILayout.PropertyField(renderMode, Styles.RenderMode);
            }

            public void DrawCullingMask()
            {
                EditorGUILayout.PropertyField(cullingMask, Styles.CullingMask);
            }

            public void DrawRenderingLayerMask()
            {
                RenderPipelineAsset srpAsset = GraphicsSettings.currentRenderPipeline;
                bool usingSRP = srpAsset != null;
                if (!usingSRP)
                    return;

                EditorGUI.showMixedValue = renderingLayerMask.hasMultipleDifferentValues;

                var mask = renderingLayerMask.intValue;
                var layerNames = srpAsset.prefixedRenderingLayerMaskNames;
                if (layerNames == null)
                    layerNames = RendererEditorBase.defaultPrefixedRenderingLayerNames;

                var rect = EditorGUILayout.GetControlRect();
                EditorGUI.BeginProperty(rect, Styles.RenderingLayerMask, renderingLayerMask);
                EditorGUI.MaskField(rect, Styles.RenderingLayerMask, mask, layerNames);
                EditorGUI.EndProperty();
                EditorGUI.showMixedValue = false;
            }

            public void ApplyModifiedProperties()
            {
                m_SerializedObject.ApplyModifiedProperties();
            }

            public void DrawShadowsType()
            {
                if (isAreaLightType)
                {
                    var rect = EditorGUILayout.GetControlRect();
                    EditorGUI.BeginProperty(rect, Styles.CastShadows, shadowsType);
                    EditorGUI.BeginChangeCheck();
                    bool shadows = EditorGUI.Toggle(rect, Styles.CastShadows, shadowsType.intValue != (int)LightShadows.None);

                    if (EditorGUI.EndChangeCheck())
                    {
                        shadowsType.intValue = shadows ? (int)LightShadows.Soft : (int)LightShadows.None;
                    }
                    EditorGUI.EndProperty();
                }
                else
                {
                    EditorGUILayout.PropertyField(shadowsType, Styles.ShadowType);
                }
            }

            public void DrawBakedShadowRadius()
            {
                using (new EditorGUI.DisabledScope(shadowsType.intValue != (int)LightShadows.Soft))
                {
                    EditorGUILayout.PropertyField(bakedShadowRadiusProp, Styles.BakedShadowRadius);
                }
            }

            public void DrawBakedShadowAngle()
            {
                using (new EditorGUI.DisabledScope(shadowsType.intValue != (int)LightShadows.Soft))
                {
                    EditorGUILayout.Slider(bakedShadowAngleProp, 0.0F, 90.0F, Styles.BakedShadowAngle);
                }
            }

            public void DrawRuntimeShadow()
            {
                EditorGUILayout.LabelField(Styles.ShadowRealtimeSettings);
                EditorGUI.indentLevel += 1;
                EditorGUILayout.Slider(shadowsStrength, 0f, 1f, Styles.ShadowStrength);
                EditorGUILayout.PropertyField(shadowsResolution, Styles.ShadowResolution);
                EditorGUILayout.Slider(shadowsBias, 0.0f, 2.0f, Styles.ShadowBias);
                EditorGUILayout.Slider(shadowsNormalBias, 0.0f, 3.0f, Styles.ShadowNormalBias);

                // this min bound should match the calculation in SharedLightData::GetNearPlaneMinBound()
                float nearPlaneMinBound = Mathf.Min(0.01f * range.floatValue, 0.1f);
                EditorGUILayout.Slider(shadowsNearPlane, nearPlaneMinBound, 10.0f, Styles.ShadowNearPlane);
                EditorGUI.indentLevel -= 1;
            }
        }

        private static class StylesEx
        {
            public static readonly GUIContent iconRemove = EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove command buffer");
            public static readonly GUIContent DisabledLightWarning = EditorGUIUtility.TrTextContent("Lighting has been disabled in at least one Scene view. Any changes applied to lights in the Scene will not be updated in these views until Lighting has been enabled again.");
            public static readonly GUIStyle invisibleButton = "InvisibleButton";
            public static readonly GUIContent noDiscLightInEnlighten = EditorGUIUtility.TrTextContent("Only the Progressive lightmapper supports Disc lights. The Enlighten lightmapper doesn't so please consider using a different light shape instead or switch to Progressive in the Lighting window.");
        }

        private Settings m_Settings;
        protected Settings settings => m_Settings ?? (m_Settings = new Settings(serializedObject));

        private IMGUI.Controls.SphereBoundsHandle m_BoundsHandle = new IMGUI.Controls.SphereBoundsHandle();

        AnimBool m_AnimShowSpotOptions = new AnimBool();
        AnimBool m_AnimShowPointOptions = new AnimBool();
        AnimBool m_AnimShowDirOptions = new AnimBool();
        AnimBool m_AnimShowAreaOptions = new AnimBool();
        AnimBool m_AnimShowRuntimeOptions = new AnimBool();
        AnimBool m_AnimShowShadowOptions = new AnimBool();
        AnimBool m_AnimBakedShadowAngleOptions = new AnimBool();
        AnimBool m_AnimBakedShadowRadiusOptions = new AnimBool();
        AnimBool m_AnimShowLightBounceIntensity = new AnimBool();

        private bool m_CommandBuffersShown = true;

        protected static readonly Color kGizmoLight = new Color(254 / 255f, 253 / 255f, 136 / 255f, 128 / 255f);
        protected static readonly Color kGizmoDisabledLight = new Color(135 / 255f, 116 / 255f, 50 / 255f, 128 / 255f);

        static readonly Vector3[] directionalLightHandlesRayPositions = new Vector3[]
        {
            new Vector3(1, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, -1, 0),
            new Vector3(1, 1, 0).normalized,
            new Vector3(1, -1, 0).normalized,
            new Vector3(-1, 1, 0).normalized,
            new Vector3(-1, -1, 0).normalized
        };

        private void SetOptions(AnimBool animBool, bool initialize, bool targetValue)
        {
            if (initialize)
            {
                animBool.value = targetValue;
                animBool.valueChanged.AddListener(Repaint);
            }
            else
            {
                animBool.target = targetValue;
            }
        }

        bool spotOptionsValue { get { return settings.typeIsSame && settings.light.type == LightType.Spot; } }
        bool pointOptionsValue { get { return settings.typeIsSame && settings.light.type == LightType.Point; } }
        bool dirOptionsValue { get { return settings.typeIsSame && settings.light.type == LightType.Directional; } }
        bool areaOptionsValue { get { return settings.typeIsSame && settings.isAreaLightType; } }
        bool runtimeOptionsValue { get { return settings.typeIsSame && ((settings.light.type != LightType.Rectangle && settings.light.type != LightType.Disc) && !settings.isCompletelyBaked); } }
        bool bakedShadowRadius { get { return settings.typeIsSame && (settings.light.type == LightType.Point || settings.light.type == LightType.Spot) && settings.isBakedOrMixed; } }
        bool bakedShadowAngle { get { return settings.typeIsSame && settings.light.type == LightType.Directional && settings.isBakedOrMixed; } }
        bool shadowOptionsValue { get { return settings.shadowTypeIsSame && settings.light.shadows != LightShadows.None; } }

        private void UpdateShowOptions(bool initialize)
        {
            SetOptions(m_AnimShowSpotOptions, initialize, spotOptionsValue);
            SetOptions(m_AnimShowPointOptions, initialize, pointOptionsValue);
            SetOptions(m_AnimShowDirOptions, initialize, dirOptionsValue);
            SetOptions(m_AnimShowShadowOptions, initialize, shadowOptionsValue);
            SetOptions(m_AnimShowAreaOptions, initialize, areaOptionsValue);
            SetOptions(m_AnimShowRuntimeOptions, initialize, runtimeOptionsValue);
            SetOptions(m_AnimBakedShadowAngleOptions, initialize, bakedShadowAngle);
            SetOptions(m_AnimBakedShadowRadiusOptions, initialize, bakedShadowRadius);
            SetOptions(m_AnimShowLightBounceIntensity, initialize, true);
        }

        protected virtual void OnEnable()
        {
            settings.OnEnable();

            UpdateShowOptions(true);
        }

        protected virtual void OnDestroy()
        {
            if (m_Settings != null)
            {
                m_Settings.OnDestroy();
                m_Settings = null;
            }
        }

        private void CommandBufferGUI()
        {
            // Command buffers are not serialized data, so can't get to them through
            // serialized property (hence no multi-edit).
            if (targets.Length != 1)
                return;
            var light = target as Light;
            if (light == null)
                return;
            int count = light.commandBufferCount;
            if (count == 0)
                return;

            m_CommandBuffersShown = GUILayout.Toggle(m_CommandBuffersShown, GUIContent.Temp(count + " command buffers"), EditorStyles.foldout);
            if (!m_CommandBuffersShown)
                return;
            EditorGUI.indentLevel++;
            foreach (LightEvent le in (LightEvent[])System.Enum.GetValues(typeof(LightEvent)))
            {
                CommandBuffer[] cbs = light.GetCommandBuffers(le);
                foreach (CommandBuffer cb in cbs)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        // row with event & command buffer information label
                        Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.miniLabel);
                        rowRect.xMin += EditorGUI.indent;
                        Rect minusRect = GetRemoveButtonRect(rowRect);
                        rowRect.xMax = minusRect.x;
                        GUI.Label(rowRect, string.Format("{0}: {1} ({2})", le, cb.name, EditorUtility.FormatBytes(cb.sizeInBytes)), EditorStyles.miniLabel);
                        // and a button to remove it
                        if (GUI.Button(minusRect, StylesEx.iconRemove, StylesEx.invisibleButton))
                        {
                            light.RemoveCommandBuffer(le, cb);
                            SceneView.RepaintAll();
                            PlayModeView.RepaintAll();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
            // "remove all" button
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove all", EditorStyles.miniButton))
                {
                    light.RemoveAllCommandBuffers();
                    SceneView.RepaintAll();
                    PlayModeView.RepaintAll();
                }
            }
            EditorGUI.indentLevel--;
        }

        static Rect GetRemoveButtonRect(Rect r)
        {
            var buttonSize = StylesEx.invisibleButton.CalcSize(StylesEx.iconRemove);
            return new Rect(r.xMax - buttonSize.x, r.y + (int)(r.height / 2 - buttonSize.y / 2), buttonSize.x, buttonSize.y);
        }

        public override void OnInspectorGUI()
        {
            settings.Update();

            UpdateShowOptions(false);

            // Light type (shape and usage)
            settings.DrawLightType();

            if (Lightmapping.GetLightingSettingsOrDefaultsFallback().lightmapper == LightingSettings.Lightmapper.Enlighten && settings.light.type == LightType.Disc)
                EditorGUILayout.HelpBox(StylesEx.noDiscLightInEnlighten.text, MessageType.Warning);

            // When we are switching between two light types that don't show the range (directional lights don't)
            // we want the fade group to stay hidden.
            if (EditorGUILayout.BeginFadeGroup(1.0f - m_AnimShowDirOptions.faded))
                settings.DrawRange();
            EditorGUILayout.EndFadeGroup();

            if (EditorGUILayout.BeginFadeGroup(m_AnimShowSpotOptions.faded))
                settings.DrawSpotAngle();
            EditorGUILayout.EndFadeGroup();

            // Area width & height
            if (EditorGUILayout.BeginFadeGroup(m_AnimShowAreaOptions.faded))
                settings.DrawArea();
            EditorGUILayout.EndFadeGroup();

            settings.DrawColor();

            // Baking type
            settings.CheckLightmappingConsistency();
            if (EditorGUILayout.BeginFadeGroup(1.0F - m_AnimShowAreaOptions.faded))
                settings.DrawLightmapping();
            EditorGUILayout.EndFadeGroup();

            settings.DrawIntensity();

            if (EditorGUILayout.BeginFadeGroup(m_AnimShowLightBounceIntensity.faded))
                settings.DrawBounceIntensity();
            EditorGUILayout.EndFadeGroup();

            ShadowsGUI();

            settings.DrawCookie();

            // Cookie size also requires directional light
            if (EditorGUILayout.BeginFadeGroup(m_AnimShowDirOptions.faded))
                settings.DrawCookieSize();
            EditorGUILayout.EndFadeGroup();

            settings.DrawHalo();
            settings.DrawFlare();
            settings.DrawRenderMode();
            settings.DrawCullingMask();
            settings.DrawRenderingLayerMask();

            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.sceneLighting == false)
                EditorGUILayout.HelpBox(StylesEx.DisabledLightWarning.text, MessageType.Warning);

            CommandBufferGUI();

            settings.ApplyModifiedProperties();
            serializedObject.ApplyModifiedProperties();
        }

        void ShadowsGUI()
        {
            //NOTE: FadeGroup's dont support nesting. Thus we just multiply the fade values here.
            settings.DrawShadowsType();

            EditorGUI.indentLevel += 1;

            float show = m_AnimShowShadowOptions.faded;

            // Baked Shadow radius
            if (EditorGUILayout.BeginFadeGroup(show * m_AnimBakedShadowRadiusOptions.faded))
                settings.DrawBakedShadowRadius();
            EditorGUILayout.EndFadeGroup();

            // Baked Shadow angle
            if (EditorGUILayout.BeginFadeGroup(show * m_AnimBakedShadowAngleOptions.faded))
                settings.DrawBakedShadowAngle();
            EditorGUILayout.EndFadeGroup();

            // Runtime shadows - shadow strength, resolution, bias
            if (EditorGUILayout.BeginFadeGroup(show * m_AnimShowRuntimeOptions.faded))
                settings.DrawRuntimeShadow();
            EditorGUILayout.EndFadeGroup();

            EditorGUI.indentLevel -= 1;
        }

        protected virtual void OnSceneGUI()
        {
            if (!target)
                return;
            Light t = target as Light;

            Color temp = Handles.color;
            if (t.enabled)
                Handles.color = kGizmoLight;
            else
                Handles.color = kGizmoDisabledLight;

            float thisRange = t.range;
            switch (t.type)
            {
                case LightType.Directional:
                    Vector3 lightPos = t.transform.position;
                    float lightSize;
                    using (new Handles.DrawingScope(Matrix4x4.identity))    //be sure no matrix affect the size computation
                    {
                        lightSize = HandleUtility.GetHandleSize(lightPos);
                    }
                    float radius = lightSize * 0.2f;
                    using (new Handles.DrawingScope(Matrix4x4.TRS(lightPos, t.transform.rotation, Vector3.one)))
                    {
                        Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
                        foreach (Vector3 normalizedPos in directionalLightHandlesRayPositions)
                        {
                            Vector3 pos = normalizedPos * radius;
                            Handles.DrawLine(pos, pos + new Vector3(0, 0, lightSize));
                        }
                    }
                    break;
                case LightType.Point:
                    thisRange = Handles.RadiusHandle(Quaternion.identity, t.transform.position, thisRange);
                    if (GUI.changed)
                    {
                        Undo.RecordObject(t, "Adjust Point Light");
                        t.range = thisRange;
                    }
                    break;
                case LightType.Spot:
                    Transform tr = t.transform;
                    Vector3 circleCenter = tr.position;
                    Vector3 arrivalCenter = circleCenter + tr.forward * t.range;
                    float lightDisc = t.range * Mathf.Tan(Mathf.Deg2Rad * t.spotAngle / 2.0f);
                    Handles.DrawLine(circleCenter, arrivalCenter + tr.up * lightDisc);
                    Handles.DrawLine(circleCenter, arrivalCenter - tr.up * lightDisc);
                    Handles.DrawLine(circleCenter, arrivalCenter + tr.right * lightDisc);
                    Handles.DrawLine(circleCenter, arrivalCenter - tr.right * lightDisc);
                    Handles.DrawWireDisc(arrivalCenter, tr.forward, lightDisc);
                    Handles.color = GetLightHandleColor(Handles.color);
                    Vector2 angleAndRange = new Vector2(t.spotAngle, t.range);
                    angleAndRange = Handles.ConeHandle(t.transform.rotation, t.transform.position, angleAndRange, 1.0f, 1.0f, true);
                    if (GUI.changed)
                    {
                        Undo.RecordObject(t, "Adjust Spot Light");
                        t.spotAngle = angleAndRange.x;
                        t.range = Mathf.Max(angleAndRange.y, 0.01F);
                    }
                    break;
                case LightType.Rectangle:
                    EditorGUI.BeginChangeCheck();
                    Vector2 size = Handles.DoRectHandles(t.transform.rotation, t.transform.position, t.areaSize, false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(t, "Adjust Rect Light");
                        t.areaSize = size;
                    }
                    // Draw the area light's normal only if it will not overlap with the current tool
                    if (!((Tools.current == Tool.Move || Tools.current == Tool.Scale) && Tools.pivotRotation == PivotRotation.Local))
                        Handles.DrawLine(t.transform.position, t.transform.position + t.transform.forward);
                    break;
                case LightType.Disc:
                    m_BoundsHandle.radius = t.areaSize.x;
                    m_BoundsHandle.axes = IMGUI.Controls.PrimitiveBoundsHandle.Axes.X | IMGUI.Controls.PrimitiveBoundsHandle.Axes.Y;
                    m_BoundsHandle.center = Vector3.zero;
                    m_BoundsHandle.wireframeColor = Handles.color;
                    m_BoundsHandle.handleColor = GetLightHandleColor(Handles.color);
                    Matrix4x4 mat = new Matrix4x4();
                    mat.SetTRS(t.transform.position, t.transform.rotation, new Vector3(1, 1, 1));
                    EditorGUI.BeginChangeCheck();
                    using (new Handles.DrawingScope(Color.white, mat))
                        m_BoundsHandle.DrawHandle();
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(t, "Adjust Disc Light");
                        t.areaSize = new Vector2(m_BoundsHandle.radius, t.areaSize.y);
                    }
                    break;
            }
            Handles.color = temp;
        }

        private Color GetLightHandleColor(Color wireframeColor)
        {
            Color color = wireframeColor;
            color.a = Mathf.Clamp01(color.a * 2);
            return Handles.ToActiveColorSpace(color);
        }
    }
}
