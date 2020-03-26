using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#elif UNITY_2018_1_OR_NEWER
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements;
#endif



namespace UTJ
{
#if UNITY_2018_1_OR_NEWER
    // [概要] Light周りで問題になりそうな箇所をチェックするツールです。
    // By Katsumasa Kimura
    public class UnityLightCheckerKun : EditorWindow
    {
        // 間接光の解像度の基準値
        const float INDIRECT_RESOLUTION_LOW = 1.0f;

        // ライトマップ解像度の閾値
        const float BAKED_RESOLUTION_LOW = 10.0f;
        const float BAKED_RESOLUTION_HIGHT = 40.0f;
        // ライトマップサイズの閾値
        const int LIGHTMAP_SIZE_THRESHOLD_HIGHT = 2048;


        // Scene上に存在するStaticなGameObjectsにアタッチされているMeshRendererのリスト
        List<MeshRenderer> meshRenderers;

        // Lightmapの影響を受けるMeshRenderer
        List<MeshRenderer> receiveLightmapsMeshRenderers;
        // LightProbeの影響を受けるMeshRenderer
        List<MeshRenderer> receiveLightProbesMeshRenderers;

        SortedDictionary<float, int> scaleInLightmapDict;

        VisualElement panel;


        [MenuItem("Window/UnityLightCheckerKun")]
        public static void Open()
        {
            UnityLightCheckerKun wnd = GetWindow<UnityLightCheckerKun>();
            wnd.titleContent = new GUIContent("UnityLightCheckerKun");
        }


        public void OnEnable()
        {
#if UNITY_2019_1_OR_NEWER
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UnityLightCheckerKun/Editor/UI/UnityLightCheckerKun.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            root.Add(labelFromUXML);

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UnityLightCheckerKun/Editor/UI/UnityLightCheckerKun.uss");
            root.styleSheets.Add(styleSheet);

            var button = root.Query<ToolbarButton>().AtIndex(0);
            button.clicked += OnButtonClick;           

#else
            var root = UIElementsEntryPoint.GetRootVisualContainer(this);
            root.AddStyleSheetPath("Assets/UnityLightCheckerKun/Editor/UI/UnityLightCheckerKun.uss");            
            var button = new Button(OnButtonClick);
            button.text = "Check";
            button.style.color = Color.white;
            root.Add(button);
#endif
        }


        SerializedObject getLightmapSettings()
        {
            var getLightmapSettingsMethod = typeof(LightmapEditorSettings).GetMethod("GetLightmapSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var lightmapSettings = getLightmapSettingsMethod.Invoke(null, null) as Object;
            return new SerializedObject(lightmapSettings);
        }


        SerializedObject getRenderSettings()
        {
            var getRenderSettingsMethod = typeof(RenderSettings).GetMethod("GetRenderSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var getRenderSettings = getRenderSettingsMethod.Invoke(null, null) as Object;
            return new SerializedObject(getRenderSettings);
        }


        Foldout CreateFoldout(string text)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            foldout.text = text;
            return foldout;
        }



        void OnButtonClick()
        {
#if UNITY_2019_1_OR_NEWER
            var root = rootVisualElement;
            var scrollView = root.Query<ScrollView>().AtIndex(0);
#else
            var root = UIElementsEntryPoint.GetRootVisualContainer(this);
            var scrollView = root;
#endif

            if (panel != null)
            {
                scrollView.Remove(panel);
            }
            panel = new VisualElement();
            scrollView.Add(panel);

            LightingWindow(panel);
            QualitySettingsWindow(panel);
            LightCheck(panel);
            MeshRendererCheck(panel);            
        }


        bool LightingWindow(VisualElement parent)
        {            
            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "Lighting Window";
            parent.Add(foldout);
            
            foldout.value |= LightingEnviroment(foldout);
            foldout.value |= LightingRealtimeLighting(foldout);
            foldout.value |= LightingMixedLighting(foldout);
            foldout.value |= LightingLightmappingSettings(foldout);
            
            return foldout.value;
        }


        bool QualitySettingsWindow(VisualElement parent)
        {
            var foldout = CreateFoldout("QualitySettings");
            parent.Add(foldout);
            foldout.value |= QualitySettingsPixelLightCount(foldout);
            foldout.value |= QualitySettingsRealtimeReflectionProbes(foldout);
            foldout.value |= QualitySettingsShadowmask(foldout);
            foldout.value |= QualitySettingsShadows(foldout);
            foldout.value |= QualitySettingsShadowResolution(foldout);
            foldout.value |= QualitySettingsShadowProjection(foldout);
            foldout.value |= QualitySettingsShadowDistance(foldout);
            foldout.value |= QualitySettingsShadowCascades(foldout);
            return foldout.value;
        }


        bool QualitySettingsPixelLightCount(VisualElement parent)
        {
            var foldout = CreateFoldout("Pixel Light Count : ");
            parent.Add(foldout);
            foldout.text += QualitySettings.pixelLightCount;

            var lights = new List<Light>((Light[])(Resources.FindObjectsOfTypeAll(typeof(Light))));
            lights = lights.FindAll(light => light.gameObject.activeInHierarchy);
            var pixelLights =  lights.FindAll(light => light.renderMode == LightRenderMode.ForcePixel);
            // Middle品質の初期値が３
            if (QualitySettings.pixelLightCount > 3)
            {
                var label = new Label();
                label.style.color = Color.yellow;
                foldout.Add(label);
                foldout.value = true;
                label.text = "[警告]pixelLightCountに大きな値が設定されています。";
            }
            if (pixelLights.Count > QualitySettings.pixelLightCount)
            {
                var label = new Label();
                label.style.color = Color.yellow;
                foldout.Add(label);
                foldout.value = true;
                label.text = "[警告]RenderModeにImportanが設定されている数(";
                label.text += pixelLights.Count;
                label.text += ")がPixel Light Countを超えています。";
            }
            {
                var label = new Label();
                label.text = "ForwardAdd Pass処理するライトの最大数を指定します。";
                label.text += "\n最も明るいディレクショナルライトがFowardBase Passで処理され、それ以外のディレクショナルライトとスポットライト、ポイントライトがForwardAdd Passで処理するライトの対象となります。";
                label.text += "\nForwardAdd Passに割り当てられなかったライトは明るいものから４個がFowardBase Passで補完情報として使用されます。";
                label.text += "Pixel Lightの数はパフォーマンスに大きく影響を及ぼします。詳しくは下記のURLを参照して下さい。";
                label.text += "\nhttps://docs.unity3d.com/ja/current/Manual/RenderTech-ForwardRendering.html";
                foldout.Add(label);
            }
            return foldout.value;
        }


        bool QualitySettingsRealtimeReflectionProbes(VisualElement parent)
        {
            var foldout = CreateFoldout("Realtime Reflection Probes : ");
            parent.Add(foldout);
            foldout.text += QualitySettings.realtimeReflectionProbes;
            var reflectionProbes = new List<ReflectionProbe>((ReflectionProbe[])(Resources.FindObjectsOfTypeAll<ReflectionProbe>()));
            reflectionProbes = reflectionProbes.FindAll(reflectionProbe => reflectionProbe.gameObject.activeInHierarchy);
            reflectionProbes = reflectionProbes.FindAll(reflectionProbe => reflectionProbe.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime);
            if (QualitySettings.realtimeReflectionProbes)
            {
                if(reflectionProbes.Count == 0)
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.style.color = Color.yellow;
                    label.text = "[警告]Realtimeで更新を行うReflectionProbeがScene内に存在しません。";
                    foldout.value = true;
                } else
                if(reflectionProbes.Count != 0)
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.style.color = Color.yellow;
                    label.text = "[警告]Realtimeで更新を行うReflectionProbeがScene内に存在します。";
                    foldout.value = true;
                }
            }
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "Reflection Probeを実行時に更新するか否かを指定します。";
            }
            return foldout.value;
        }


        bool QualitySettingsShadowmask(VisualElement parent)
        {
            var foldout = CreateFoldout("Shadowmask : ");
            parent.Add(foldout);
            foldout.text += QualitySettings.shadowmaskMode;
            if(QualitySettings.shadowmaskMode == ShadowmaskMode.DistanceShadowmask)
            {
                if(Lightmapping.bakedGI == false)
                {
                    var label = new Label("[警告]Baked Global Illuminationが有効ではありません。");
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    foldout.value = true;
                }               
                else
                if(LightmapEditorSettings.mixedBakeMode != MixedLightingMode.Shadowmask)
                {
                    var label = new Label("[警告]Mixed LightingのLighting ModeがShadowmaskではありません。");
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    foldout.value = true;
                }
            }
            {
                var label = new Label("Shadow Distanceに応じてShadowMaskとShadowMapを切り替えて使用するかを指定します。");
                foldout.Add(label);
            }
            return foldout.value;
        }


        bool QualitySettingsShadows(VisualElement parent)
        {
            var foldout = CreateFoldout("Shadows : " + QualitySettings.shadows);
            parent.Add(foldout);
            if (QualitySettings.shadows == ShadowQuality.Disable)
            {
                var label = new Label("[警告]Shadowsの描画が無効なっています。");
                foldout.Add(label);
                label.style.color = Color.yellow;
                foldout.value = true;
            }
            if(QualitySettings.shadows == ShadowQuality.All)
            {
                var label = new Label("[警告]Soft Shadowsが有効になっています。\nSoft Shadowが有効の場合、フィルタリング処理がかかり滑らかな表現になりますが、その分GPUの処理負荷がかかります。");
                foldout.Add(label);
                label.style.color = Color.yellow;
                foldout.value = true;
            }
            {
                var label = new Label("Shadowsの描画の有無及び描画時にフィルタリング処理を行うかを指定します。フィルタリング処理を行うことで輪郭部分が滑らかになりますが、その分GPUの負荷が高くなります。");
                foldout.Add(label);
            }
            return foldout.value;
        }


        bool QualitySettingsShadowResolution(VisualElement parent)
        {
            var foldout = CreateFoldout("Shadow Resolution : " + QualitySettings.shadowResolution);
            parent.Add(foldout);

            if(QualitySettings.shadowResolution > ShadowResolution.Low)
            {
                var label = new Label("[警告]Shadow Resolutionに " + QualitySettings.shadowResolution + " より大きな解像度が設定されています。");
                label.style.color = Color.yellow;
                foldout.Add(label);
                foldout.value = true;
            }
            {
                var label = new Label("Shadowmapの解像度(にかかる係数)を指定します。");
                label.text += "\n Shadowmapの解像度は画面の解像度、ライトの種類、及びこの値で決定されます。";
                label.text += "\n Type :: Directional : x 1.9, Sopt : x 1, Point : x 0.5";
                label.text += "\n Resolution ::  Low : x 1/4, Medium : x 1/2, Hight : x 1, Very Hight : x 2";
                label.text += "\n ※Light側にShadowCustomResolutionの設定がある場合はそちらが使用されます。";
                label.text += "\n 詳しくは下記をご確認下さい。";
                label.text += "\n https://docs.unity3d.com/jp/460/Manual/ShadowSizeDetails.html";
                foldout.Add(label);
            }

            return foldout.value;
        }


        bool QualitySettingsShadowProjection(VisualElement parent)
        {
            var foldout = CreateFoldout("Shadow Projection : " + QualitySettings.shadowProjection);
            parent.Add(foldout);

            if(QualitySettings.shadowProjection == ShadowProjection.StableFit)
            {
                if (QualitySettings.shadowCascades == 1)
                {
                    var label = new Label("[確認]StableFitが選択されていますが、ShaowCascadeが無効になっています。");
                    foldout.Add(label);
                }
            }
            {
                var label = new Label("Shadowmapのサイズを決めるサイズの基準を指定します。CloseFitはカメラとオブジェクトの距離によってShadowmapの解像度が決定される為、カメラから近いオブジェクトの影の解像度が向上しますが、カメラのオブジェクトの距離が切り替わる際に影がちらつく場合があります。");
                foldout.Add(label);
            }
            return foldout.value;
        }


        bool QualitySettingsShadowDistance(VisualElement parent)
        {
            var foldout = CreateFoldout("Shadowmask Distance : " + QualitySettings.shadowDistance);
            parent.Add(foldout);
            // 500以上は意味がないと記載されているのでそれを目安に
            // https://docs.unity3d.com/jp/460/Manual/Shadows.html
            if (QualitySettings.shadowDistance > 500.0f)
            {
                var label = new Label("[警告] ShadowDistanceに500以上の値が設定されています。一般的には500m以上は意味がないと言われていますので出来るだけ小さな値になるように調整して下さい。");
                foldout.Add(label);
            }
            {
                var label = new Label("Realtime描画されるShadowの距離を指定します。Distance Shadowmaskの場合はこの距離以上の影はShadowmaskで描画されます。");
                foldout.Add(label);
            }
            return foldout.value;
        }


        bool QualitySettingsShadowCascades(VisualElement parent)
        {
            string[] dispNames = {
                " ",
                "No Cascades",
                "Two Cascades",
                "",
                "Four Cascades",
            };

            var foldout = CreateFoldout("Shadowmask Cascades : " + dispNames[QualitySettings.shadowCascades]);
            parent.Add(foldout);
            bool isMobile = false;
            if( EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                isMobile = true;
            }

            if (QualitySettings.shadowCascades > 1)
            {
                if (isMobile)
                {
                    var label = new Label("[警告]Shadow Cascadesが有効になっていますが、モバイルのGPUでは対応していない可能性があります。");
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                }
            }
            {
                var label = new Label("Shadow Cacadeを有効にするか無効にするか、有効にした場合何分割するかを指定します。詳細は下記のURLをご確認下さい。\n https://docs.unity3d.com/ja/2018.4/Manual/DirLightShadows.html");
                foldout.Add(label);
            }

            return foldout.value;
        }


        bool LightCheck(VisualElement parent)
        {
            var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
            List<Light> lights = new List<Light>(objs);
            lights = lights.FindAll(light => light.gameObject.activeInHierarchy);

            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Light : " + lights.Count;
            foldout.value |= LightType(foldout);
            foldout.value |= LightBakeType(foldout);
            foldout.value |= LightIndirectMultiplier(foldout);

            return foldout.value;
        }


        bool LightingEnviroment(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "Environment";            
            parent.Add(foldout);

            foldout.value |= LightingEnviromentSkyboxMaterial(foldout);
            foldout.value |= LightingEnviromentSunSource(foldout);
            foldout.value |= LightingEnviromentEnviromentLighting(foldout);
            foldout.value |= LightingEnviromentEnviromentReflections(foldout);
            
            return foldout.value;
        }


        bool LightingEnviromentSkyboxMaterial(VisualElement parent)
        {
            SerializedObject renderSettings = getRenderSettings();
            SerializedProperty ambientSource = renderSettings.FindProperty("m_AmbientMode");
            SerializedProperty defaultReflectionMode = renderSettings.FindProperty("m_DefaultReflectionMode");


            // Enviroment Lighting Sourc,Enviroment Source,Cameraのクリアフラグをチェック           
            bool isNeedSkyboxMaterial = false;
            if ((UnityEngine.Rendering.AmbientMode)ambientSource.intValue == UnityEngine.Rendering.AmbientMode.Skybox)
            {
                isNeedSkyboxMaterial = true;
            }
            else if ((UnityEngine.Rendering.AmbientMode)defaultReflectionMode.intValue == UnityEngine.Rendering.AmbientMode.Skybox)
            {
                isNeedSkyboxMaterial = true;
            }
            else
            {
                var objs = (Camera[])(Resources.FindObjectsOfTypeAll(typeof(Camera)));
                foreach (var camera in objs)
                {
                    if (camera.gameObject.activeInHierarchy == false)
                    {
                        continue;
                    }
                    if (camera.clearFlags == CameraClearFlags.Skybox)
                    {
                        isNeedSkyboxMaterial = true;
                        break;
                    }
                }
            }
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            parent.Add(foldout);
            foldout.value = false;
            foldout.text = "Skybox Material";
            if (isNeedSkyboxMaterial == false && RenderSettings.skybox != null)
            {
                var label = new Label();
                foldout.Add(label);
                label.style.color = Color.yellow;
                label.text = "[警告]Skyboxが使用されていませんがSkyboxMaterialが使用されています。";
                label.text += "\nSkyboxを使用しない場合、この項目にnoneを設定することで実行時のメモリーの削減が期待出来ます。";                
                foldout.value = true;
            }
            if(RenderSettings.sun != null){
                if (RenderSettings.skybox == null){
                    var label = new Label();
                    foldout.Add(label);
                    label.style.color = Color.yellow;                    
                    label.text = "[警告] Sun Sourceか指定されていますが、Skybox Materialが指定されていません。";
                    foldout.value = true;
                }
                else 
                if( RenderSettings.skybox.shader.name != "Skybox/Procedural"
                    && RenderSettings.skybox.IsKeywordEnabled("_SUNDISK_SIMPLE") != false)
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.style.color = Color.yellow;
                    label.text = "[警告] Sun Sourceか指定されていますが、Sun ProcedualではないMaterialが設定されています。";
                    foldout.value = true;
                }
            }

            {
                var label = new Label();
                foldout.Add(label);
                label.text = "Skyboxとして使用するMaterialを指定します。";
                label.text += "\nBuilt-in ShaderとしてShader/SkyboxにいくつかのShaderが用意されています。";
            }
            
            return foldout.value;
        }


        bool LightingEnviromentSunSource(VisualElement parent)
        {
            var skyboxMaterial = RenderSettings.skybox;
            var sun = RenderSettings.sun;

            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Sun Source : ";
            if(sun != null)
            {
                foldout.text += sun.name;
            }
            // Skybox Proceduralであるか確認
            if ((skyboxMaterial.shader.name == "Skybox/Procedural")
            || (skyboxMaterial.IsKeywordEnabled("_SUNDISK_SIMPLE")))
            {
                if(sun == null)
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.style.color = Color.yellow;
                    label.text = "[警告]Skybox Procedualが使用されていますが、Sun Sourceが指定されていません。";
                    foldout.value = true;
                }
            }
            {
                var label = new Label();
                label.text  = "Skybox MaterialがSkybox/Proceduralを使用している場合、この設定でDirectional Lightを指定して太陽（Sceneを照らす最も大きくて遠い光源）の方向を指定します。";
                label.text += "\nSun Sourceに指定されたLightの向きを変えることで時刻の変化による移り変わりを再現することが出来ます。";
                label.text += "\nNoneの場合、Scene内の最も明るいDirectional Lightが使用されます。";
                foldout.Add(label);
            }

            return foldout.value;
        }


        bool LightingEnviromentEnviromentLighting(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Enviroment Lighting";

            foldout.value |=foldout.value |= LightingEnviromentEnviromentLightingSource(foldout);
            foldout.value |= LightingEnviromentEnviromentLightingAmbientMode(foldout);
            return foldout.value;
        }


        bool LightingEnviromentEnviromentLightingSource(VisualElement parent)
        {            
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Source : ";

            var so = getRenderSettings();
            var ambientSource = so.FindProperty("m_AmbientMode");
            foldout.text += ambientSource.displayName;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "Ambientの取得元を設定します。";
            }
            return foldout.value;
        }


        // Ambient Mode
        bool LightingEnviromentEnviromentLightingAmbientMode(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Ambient Mode: ";
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "動的オブジェクトへの間接光の処理をBaked/Realtime GIのどちらで行うか指定します。";
                label.text += "\n Baked : 間接光をLightProbeへBakeします。";
                label.text += "\n Realtime : 間接光をLightProbeへRealtimeに更新します。";
                label.text += "\nRealtime Global Illumination/Baked Global Illuminationの両方にチェックが入っている時のみ有効です。";
            }
            return foldout.value;
        }


        // Enviroment Reflection
        // ようはScene全体にかかるReflection Probe
        // Ambientみたいなもの
        bool LightingEnviromentEnviromentReflections(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            foldout.text = "Environment Reflections";
            parent.Add(foldout);

            foldout.value |= LightingEnviromentEnviromentReflectionsSource(foldout);
            foldout.value |= LightingEnviromentEnviromentReflectionsCompression(foldout);
            foldout.value |= LightingEnviromentEnviromentReflectionsIntensityMultiplier(foldout);
            foldout.value |= LightingEnviromentEnviromentReflectionsBounces(foldout);
            return foldout.value;
        }


        // Source
        bool LightingEnviromentEnviromentReflectionsSource(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Source : ";
            foldout.text += RenderSettings.defaultReflectionMode.ToString();
            // SkyBoxの場合
            if (RenderSettings.defaultReflectionMode == UnityEngine.Rendering.DefaultReflectionMode.Skybox)
            {
                // 解像度が一定以上の場合
                if (RenderSettings.defaultReflectionResolution > 256)
                {
                    var label = new Label();
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    label.text = "[警告]高解像度のResolutinが指定されています。 (" + RenderSettings.defaultReflectionResolution + ")";
                    foldout.value = true;
                }
                // コメント
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.text = "Reflection Probeの解像度は一般的に低解像度で十分であると言われています。";
                    label.text += "\n目安としてPCで512,コンソールで256、モバイルではそれ以下です。";
                }
            }
            else 
            // Customeの場合
            if (RenderSettings.defaultReflectionMode == UnityEngine.Rendering.DefaultReflectionMode.Custom)
            {
                // Costomの場合はQubeマップを指定する
                if (RenderSettings.customReflection == null)
                {
                    var label = new Label();
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    label.text = "[警告]Customeが設定されていますが、Cubemapが設定されていません。";
                    foldout.value = true;
                }
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.text = "Scene全体で使用するReflectionProbeのSorceにSkyboxを使用するか独自に用意したQubemapを使用するか指定します。";
                }
            }
            return foldout.value;
        }


        bool LightingEnviromentEnviromentReflectionsCompression(VisualElement parent)
        {
            SerializedObject so = getLightmapSettings();
            SerializedProperty reflectionCompression = so.FindProperty("m_LightmapEditorSettings.m_ReflectionCompression");

            var foldout = new Foldout();
            foldout.text = "Compression : " + reflectionCompression.displayName;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            if (reflectionCompression.intValue == 0)
            {
                // 非圧縮
                if (RenderSettings.defaultReflectionResolution > 256)
                {
                    var label = new Label();
                    label.text = "[警告]Enviroment ReflectionsのCompressionにUncompressedが設定されています。";
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    foldout.value = true;
                }
            } else if (reflectionCompression.intValue == 1)
            {
                // 圧縮
                if (RenderSettings.defaultReflectionResolution < 256)
                {
                    var label = new Label();
                    label.text = "[警告]Enviroment ReflectionsのCompressionにCompressedが設定されています。";
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    foldout.value = true;
                }
            }
            // コメント
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "リフレクション用テクスチャを圧縮するか指定します。";
                label.text += "ビジュアル的に許容できる場合は圧縮することで実行時のメモリーの削減が期待出来ます。";
                label.text += "Resolutionの値が低い場合に圧縮するとノイズがのる場合があるので注意が必要です。";
                label.text += "非圧縮のリフレクション用のテクスチャが必要とするサイズは 8 x Resulution x Resulution [byte]です。";
            }
            return foldout.value;
        }


        bool LightingEnviromentEnviromentReflectionsIntensityMultiplier(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Intensity Multiplier : " + RenderSettings.reflectionIntensity;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "リフレクションソース (Reflection Source プロパティーで指定されたスカイボックスやキューブマップ) の色が、オブジェクトに反射する際の係数を表します";
            }
            return foldout.value;
        }


        bool LightingEnviromentEnviromentReflectionsBounces(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Bounces : " + RenderSettings.reflectionBounces;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "Scene内に配置されている全てのReflectionProbeの反射回数です。";
                label.text += "反射回数が高い程正しい結果に近づきますがBake(処理)時間が伸びます。";
            }
            return foldout.value;
        }


        bool LightingRealtimeLighting(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Realtime Lighting";
            
            // Realtime Global Illumination
            foldout.value |= LightingRealtimeLightingRealtimeGlobalIllumination(foldout);
            return foldout.value;
        }


        bool LightingRealtimeLightingRealtimeGlobalIllumination(VisualElement parent)
        {
            SerializedObject lightmapSettings = getLightmapSettings();
            SerializedProperty enabledRealtimeGI = lightmapSettings.FindProperty("m_GISettings.m_EnableRealtimeLightmaps");


            //var m = typeof(UnityEngine.Rendering.SupportedRenderingFeatures).GetMethod("IsLightmapBakeTypeSupported", BindingFlags.Static | BindingFlags.NonPublic);                    
            //var realtimeGISupported = m.Invoke(null, new object[] { (object)LightmapBakeType.Realtime });
            //var bakedGISupported = m.Invoke(null, new object[] { (object)LightmapBakeType.Baked });
            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "RealtimeGlobal Illumination : " + enabledRealtimeGI.boolValue;
            parent.Add(foldout);
                     
            if (enabledRealtimeGI.boolValue)
            {
                var label = new Label();
                foldout.Add(label);
                label.style.color = Color.yellow;                
                label.text = "[警告]Realtime Global Illumination が有効に設定されていますが、";
                label.text += "Realtime Global IlluminationはDeprecateとしてマークされています。";
                foldout.value = true;
            }
            if (LightmapSettings.lightmaps.Length < 1 && enabledRealtimeGI.boolValue)
            {
                var label = new Label();
                foldout.Add(label);
                label.style.color = Color.yellow;
                label.text = "[警告]Realtime Global Illumination が有効に設定されていますがBakeが行われていません。";
                foldout.value = true;
            }
            {
                var label = new Label();
                foldout.Add(label);
                label.text  = "\n間接光の計算を実行時に行う為の事前計算を行い、実行時にリアルタイムライトマップにBakeし都度更新します。";                
                label.text += "\nBake結果はLightingDataに保存されます。";
                label.text += "\n時間と共に徐々に太陽の位置を変化させたい場合などに使用します。";                
                label.text += "\nRealtime GIは多くのリソースを消費します。";                
                label.text += "\nhttps://docs.unity3d.com/ja/current/Manual/GIIntro.html";
            }
            return foldout.value;
        }


        bool LightingMixedLighting(VisualElement parent)
        {
            var mixedLightingFoldout = new Foldout();
            mixedLightingFoldout.value = false;
            mixedLightingFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            mixedLightingFoldout.text = "Mixed Lighting";
            parent.Add(mixedLightingFoldout);

            mixedLightingFoldout.value = LightingMixedLightingBakedGlobalIllumination(mixedLightingFoldout);
            return mixedLightingFoldout.value;
        }


        bool LightingMixedLightingBakedGlobalIllumination(VisualElement parent)
        {
            var so = getLightmapSettings();
            var enabledBakedGI = so.FindProperty("m_GISettings.m_EnableBakedLightmaps");
            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "Baked Global Illumination : " + enabledBakedGI.boolValue;
            parent.Add(foldout);
            if (enabledBakedGI.boolValue == true)
            {
                if(LightmapSettings.lightmaps.Length < 1)
                {
                    var label = new Label();
                    foldout.Add(label);
                    label.style.color = Color.yellow;
                    label.text = "[警告]Baked Global Illumination が有効に設定されていますがBakeが行われていません。";
                    foldout.value = true;
                }
            }
            
                // 設定がOFFになっているが、Scene内にRealtimeではないLightが存在する
            if (enabledBakedGI.boolValue == false)
            {
                bool isUseBakedLight = false;
                var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
                foreach (var light in objs)
                {
                    if (light.gameObject.activeInHierarchy == false)
                    {
                        continue;
                    }
                    if (light.lightmapBakeType != LightmapBakeType.Realtime)
                    {
                        isUseBakedLight = true;
                        break;
                    }
                }
                if (isUseBakedLight)
                {
                    var label = new Label();
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    label.text = "[警告]Scene内にModeがRealTime以外のLightが配置されていますが、Baked Global Illuminationが無効になっています。";
                    foldout.value = true;
                }
            }

            {                
                var label = new Label();
                foldout.Add(label);
                label.text = "間接光（IndirectLight)をLightmapにBakeします。。";
                label.text += "\nModeがBaked及びMixedのLightが対象となります。";
                label.text += "\n※Baked Global Illuminationが無効な場合、LightのModeは強制的にRealtimeとして処理されます。";
            }
            return foldout.value;
        }


        bool LightingMixedLightingLighingMode(VisualElement parent)
        {
            SerializedObject so = getLightmapSettings();
            var mixedBakeMode = so.FindProperty("m_LightmapEditorSettings.m_MixedBakeMode");


            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "Lighting Mode : " + mixedBakeMode.displayName;
            if(mixedBakeMode.intValue == (int)MixedLightingMode.Shadowmask)
            {
                foldout.text += "(" + QualitySettings.shadowmaskMode.ToString() + ")";
            }
            parent.Add(foldout);

            if (mixedBakeMode.intValue == (int)MixedLightingMode.Shadowmask)
            {
                var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
                var list = new List<Light>(objs);
                var q = list.FindAll(light => light.gameObject.activeInHierarchy && light.lightmapBakeType == LightmapBakeType.Mixed);
                if(q.Count > 4)
                {
                    var label = new Label();
                    parent.Add(label);
                    label.style.color = Color.yellow;
                    label.text = "[警告]４個以上のBakteTypeがMixedのLightが４個以上含まれています。 ";
                    label.text+= "Shadowmaskに含めることが出来るMixed Lightは4個迄です。";
                    label.text += "それ以上のLightを使用する場合はShadowmapを使用するため処理負荷が発生します。";
                }                
            }
            {
                var label = new Label();
                parent.Add(label);
                label.text = "ModeがMixedになっているLighに関して、Direct/Indirectをどのように扱うかを指定します。";
                label.text += "https://docs.unity3d.com/ja/current/Manual/LightMode-Mixed.html";
            }
            return foldout.value;
        }


        bool LightingLightmappingSettings(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "LightmappingSettings";
            parent.Add(foldout);


            switch (LightmapEditorSettings.lightmapper)
            {
                case LightmapEditorSettings.Lightmapper.Enlighten:
                    {
                        foldout.value = LightingLightmappingSettingsEnlighten(foldout);
                    }
                    break;
                case LightmapEditorSettings.Lightmapper.ProgressiveCPU:
                case LightmapEditorSettings.Lightmapper.ProgressiveGPU:
                    {
                        foldout.value = LightingLightmappingSettingsProgressive(foldout);
                    }
                    break;
            }
            foldout.value |= LightingLightmappingSettingIndirectResolution(foldout);
            foldout.value |= LightingLightmappingSettingsLightmapResolution(foldout);
            foldout.value |= LightingLightmappingSettingsLightmapPadding(foldout);

            return foldout.value;
        }


        // Enlighten
        bool LightingLightmappingSettingsEnlighten(VisualElement parent)
        {
            bool result = false;
            result |= LightingLightmappingSettingsEnlightenLightmapper(parent);
            result |= LightingLightmappingSettingsEnlightenFinalGather(parent);
            result |= LightingLightmappingSettingsLightmapSize(parent);
            result |= LightingLightmappingSettingsCompressLightmaps(parent);
            result |= LightingLightmappingSettingsDirectionalMode(parent);
            result |= LightingLightmappingSettingsAO(parent);
            result |= LightingLightmappingSettingsIndirectIntensity(parent);
            result |= LightingLightmappingSettingsAlbedoBoost(parent);

            return result;
        }


        bool LightingLightmappingSettingsProgressive(VisualElement parent)
        {
            var so = getLightmapSettings();
            var foldout = new Foldout();
            foldout.text = "Lightmapper : ";
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);


            if(LightmapEditorSettings.lightmapper == LightmapEditorSettings.Lightmapper.ProgressiveCPU)
            {
                foldout.text += LightmapEditorSettings.Lightmapper.ProgressiveCPU;
            } 
            else
            if (LightmapEditorSettings.lightmapper == LightmapEditorSettings.Lightmapper.ProgressiveGPU)
            {
                foldout.text += LightmapEditorSettings.Lightmapper.ProgressiveGPU;

                var label = new Label();
                label.text = "[警告]:ProgressiveGPUが選択されています。";
                label.style.color = Color.yellow;
                foldout.Add(label);
                label = new Label();
                label.text = "ProgressiveGPUのステータスはPreview版です。";
                label.text += "\n十分にご確認の上お使い下さい。";
                foldout.Add(label);
                foldout.value = true;
            }

            foldout.value |= LightingLightmappingSettingsProgressivePrioritizeView(foldout);
            foldout.value |= LightingLightmappingSettingsProgressiveDirectSamples(foldout);
            foldout.value |= LightingLightmappingSettingsProgressiveIndirectSamples(foldout);
            foldout.value |= LightingLightmappingSettingsProgressiveFiltering(foldout);
            foldout.value |= LightingLightmappingSettingsProgressiveIndirectResolution(foldout);
            foldout.value |= LightingLightmappingSettingsLightmapResolution(foldout);
            foldout.value |= LightingLightmappingSettingsLightmapPadding(foldout);
            foldout.value |= LightingLightmappingSettingsLightmapSize(foldout);
            foldout.value |= LightingLightmappingSettingsCompressLightmaps(foldout);
            foldout.value |= LightingLightmappingSettingsDirectionalMode(foldout);
            foldout.value |= LightingLightmappingSettingsAO(foldout);
            foldout.value |= LightingLightmappingSettingsIndirectIntensity(foldout);
            foldout.value |= LightingLightmappingSettingsAlbedoBoost(foldout);


            return foldout.value;
        }


        bool LightingLightmappingSettingsEnlightenLightmapper(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.text = "Lightmapper : Enlighten";
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = true;
            parent.Add(foldout);
            var label = new Label();
            label = new Label();
            label.text = "[警告]EnlightenはDeprecateとしてマークされています。";
            label.style.color = Color.yellow;
            foldout.Add(label);
            label = new Label();
            label.text = "ビルトインレンダラーを使用している場合、Unity2020.4LTS迄、HDRPを使用している場合、Unity2019.4LTS迄のサポートとなります。";
            label.text += "\n早い段階でプログレッシブライトマッパーへの移行をお勧めします。";
            label.text += "\nhttps://blogs.unity3d.com/jp/2019/07/03/enlighten-will-be-replaced-with-a-robust-solution-for-baked-and-real-time-giobal-illumination/";
            label.text += "\n";
            foldout.Add(label);

            return foldout.value;
        }


        bool LightingLightmappingSettingsEnlightenFinalGather(VisualElement parent)
        {
            var so = getLightmapSettings();
            var finalGather = so.FindProperty("m_LightmapEditorSettings.m_FinalGather");
            var finalGatherFoldout = new Foldout();
            finalGatherFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            finalGatherFoldout.value = false;
            finalGatherFoldout.text = "Final Gather : " + finalGather.boolValue;
            parent.Add(finalGatherFoldout);
            var label = new Label();
            finalGatherFoldout.Add(label);
            if (finalGather.boolValue) {
                label.style.color = Color.yellow;
                label.text = "[警告]FinalGatherが無効になっています。";
                finalGatherFoldout.Add(new Label("無効にすると品質が低下する場合があります。リリース時迄には有効にすることを検討して下さい。\n"));
                finalGatherFoldout.value = true;
            }
            return finalGatherFoldout.value;
        }


        bool LightingLightmappingSettingIndirectResolution(VisualElement parent)
        {
            var so = getLightmapSettings();
            var resolution = so.FindProperty("m_LightmapEditorSettings.m_Resolution");
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            parent.Add(foldout);
            foldout.value = false;
            foldout.text = "Indirect Resolution : " + resolution.floatValue;

            if (resolution.floatValue < INDIRECT_RESOLUTION_LOW)
            {
                var subLabel = new Label();
                subLabel.style.color = Color.yellow;
                subLabel.text = "[警告]低い値が設定されています・間接光の品質を満たしているか確認を行って下さい。";
                foldout.Add(subLabel);
                var label = new Label();
                label.text = "この値は間接光のクオリティにのみ影響します。（直接光には影響しません。）";
                label.text += "値が大きいとクオリティが上がりますがLightmapのBake時間が伸びます。";
                label.text += "Scene View > Shading menu > Realtime GI > UV Charts からどのように影響を及ぼしているか確認することが出来ます";
                label.text += "\n";
                foldout.Add(label);
                foldout.value = true;
            }
            return foldout.value;
        }


        bool LightingLightmappingSettingsLightmapResolution(VisualElement parent)
        {
            var so = getLightmapSettings();

            var bakeResolution = so.FindProperty("m_LightmapEditorSettings.m_BakeResolution");
            var lightmapResolutionFoldout = new Foldout();
            lightmapResolutionFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            parent.Add(lightmapResolutionFoldout);
            lightmapResolutionFoldout.value = false;
            lightmapResolutionFoldout.text = "Lightmap Resolution : " + bakeResolution.floatValue;
            if (bakeResolution.floatValue < BAKED_RESOLUTION_LOW)
            {
                var label = new Label();
                lightmapResolutionFoldout.Add(label);
                label.text = "[警告]値が基準値(" + BAKED_RESOLUTION_LOW + ")を下回っています。";
                label.style.color = Color.yellow;
                lightmapResolutionFoldout.Add(label);
                label = new Label();
                label.text = "クオリティに問題がないかアートディレクターに確認を行って下さい。";
                lightmapResolutionFoldout.Add(label);
                lightmapResolutionFoldout.value = true;
            }
            else if (bakeResolution.floatValue > BAKED_RESOLUTION_HIGHT)
            {
                var label = new Label();
                lightmapResolutionFoldout.Add(label);
                label.text = "[警告]値が基準値(" + BAKED_RESOLUTION_HIGHT + ")を上回っています。";
                label.style.color = Color.yellow;
                lightmapResolutionFoldout.Add(label);
                label = new Label();
                label.text = "一般的には" + BAKED_RESOLUTION_HIGHT + "あれば十分であると言われています。";
                lightmapResolutionFoldout.Add(label);
                lightmapResolutionFoldout.value = true;
            }

            {
                var label = new Label();
                label.text = "\n1Unit(Size:1x1x1)のオブジェクトの各面に何テクセル割り当てるかを指定します。";
                label.text += "\n10texelsと40texelsを比較した場合、10[texels]=10x10=100,40[texels]=40x40=1600となる為、16倍となります";
                label.text += "\nBake時間、Runtimeの使用メモリ（LightmapTexture）、クオリティに影響を及ぼします。";
                label.text += "\n";
                lightmapResolutionFoldout.Add(label);
            }

            return lightmapResolutionFoldout.value;
        }


        bool LightingLightmappingSettingsLightmapPadding(VisualElement parent)
        {
            var so = getLightmapSettings();
            var padding = so.FindProperty("m_LightmapEditorSettings.m_Padding");
            var lightmapPaddingFoldout = new Foldout();
            parent.Add(lightmapPaddingFoldout);
            lightmapPaddingFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            lightmapPaddingFoldout.value = false;
            lightmapPaddingFoldout.text = "Lightmap Padding : " + padding.intValue;
            if (padding.intValue == 0)
            {
                var label = new Label();
                label.text = "[警告]Paddingに0が設定されています。";
                label.style.color = Color.yellow;
                lightmapPaddingFoldout.Add(label);
                label = new Label();
                label.text = "Lightmap上のUVが連続する部分にノイズが載っていないかSceneを確認して下さい。";
                lightmapPaddingFoldout.Add(label);
                lightmapPaddingFoldout.value = true;
            }
            else if (padding.intValue > 2)
            {
                var label = new Label();
                label.text = "[警告]Paddingに" + padding.intValue + " が設定されています。";
                label.style.color = Color.yellow;
                lightmapPaddingFoldout.Add(label);
                label = new Label();
                label.text = "Bakeの対象となるオブジェクトが多い場合、Lightmap Textureのサイズに影響を及ぼす場合があります。";
                lightmapPaddingFoldout.value = true;
            }
            return lightmapPaddingFoldout.value;
        }


        bool LightingLightmappingSettingsLightmapSize(VisualElement parent)
        {
            var so = getLightmapSettings();
            var lightmapSize = so.FindProperty("m_LightmapEditorSettings.m_AtlasSize");
            var lightnapSizeFoldOut = new Foldout();

            lightnapSizeFoldOut.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            lightnapSizeFoldOut.value = false;
            parent.Add(lightnapSizeFoldOut);
            lightnapSizeFoldOut.text = "Lightmap Size : " + lightmapSize.intValue;

            if (lightmapSize.intValue > LIGHTMAP_SIZE_THRESHOLD_HIGHT)
            {
                var label = new Label();
                label.text = "[警告]LightmapのTextureサイズが2048x2048をオーバーしています。";
                label.style.color = Color.yellow;
                lightnapSizeFoldOut.Add(label);
                label = new Label();
                label.text = "モバイル端末においても殆どの端末(iOS:iPhone4S以降、Android:4.0以降(端末によっては一部対応していないものある))で4096x4096がサポートされていますが、メモリーの消費量は増大します。";
                lightnapSizeFoldOut.Add(label);
                lightnapSizeFoldOut.value = true;
            }
            if (LightmapSettings.lightmaps.Length > 1)
            {
                var label = new Label();
                label.text = "[警告]Lightmapが " + LightmapSettings.lightmaps.Length + "枚出力されています。";
                label.style.color = Color.yellow;
                lightnapSizeFoldOut.Add(label);
                label = new Label();
                label.text = "テクスチャの枚数が多くなる程、実行時のオーバーヘッドが大きくなります。\n";
                label.text += "Textureに隙間が出ない範囲で可能な限り少ない枚数のTextureになるようにパラメータを調整して下さい。";
                label.text += "\n - Lightmap Resolutionで全体的な解像度を調整する";
                label.text += "\n - Lightmap Paddingで余計な隙間が出来ている場合は調整する";
                label.text += "\n - Mesh Renderer->Lightmapping->Scale in Lightmapでオブジェクト毎に個別に階層度を調整する";
                label.text += "\n - 小さい・カメラから遠い場所におかれている等、重要ではないオブジェクトはLightProbeを使用する";
                lightnapSizeFoldOut.Add(label);
                lightnapSizeFoldOut.value = true;
            }
            return lightnapSizeFoldOut.value;
        }


        bool LightingLightmappingSettingsCompressLightmaps(VisualElement parent)
        {
            var so = getLightmapSettings();
            var textureCompression = so.FindProperty("m_LightmapEditorSettings.m_TextureCompression");
            var lightmapSize = so.FindProperty("m_LightmapEditorSettings.m_AtlasSize");

            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Compress Lightmaps : " + textureCompression.boolValue;
            if (textureCompression.boolValue == false
            && lightmapSize.intValue >= 1024)
            {
                var label = new Label();
                label.text = "[警告] LightmapのResolutionが1024x1024以上ですが、圧縮が無効になっています。";
                label.style.color = Color.yellow;
                foldout.Add(label);
                label = new Label("Lightmapの解像度が一定値以上の場合は、圧縮を有効にすることでメモリーの削減が期待出来ます。");
                foldout.Add(label);
                foldout.value = true;

            }
            if (textureCompression.boolValue == true
            && lightmapSize.intValue < 512)
            {
                var label = new Label();
                label.text = "[警告] LightmapのResolutionが512x512未満ですが、圧縮が有効になっています。";
                label.style.color = Color.yellow;
                foldout.Add(label);
                label = new Label("Lightmapの解像度が一定値未満場合は、圧縮を有効にすることノイズが目立つ場合があります。");
                foldout.Add(label);
                foldout.value = true;
            }
            return foldout.value;
        }


        bool LightingLightmappingSettingsDirectionalMode(VisualElement parent)
        {
            var so = getLightmapSettings();
            var lightmapDirectionalMode = so.FindProperty("m_LightmapEditorSettings.m_LightmapsBakeMode");
            var foldout = new Foldout();
            foldout.text = "Directional Mode : ";
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = true;
            parent.Add(foldout);


            if (LightmapEditorSettings.lightmapsMode == LightmapsMode.CombinedDirectional)
            {
                foldout.text += "Directional";

                bool isHaveDir = false;
                foreach (LightmapData ld in LightmapSettings.lightmaps)
                {
                    if (ld.lightmapDir)
                    {
                        isHaveDir = true;
                        break;
                    }

                }
                if (isHaveDir == false)
                {
                    var label = new Label();
                    label.text = "[警告]Directionalが選択されていますが、Lightmapに反映されていません。";
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    label = new Label("3DモデルにNormalmapが含まれていない場合、Non-Directionalを選択することでBake時間が短縮される可能性があります。");
                    foldout.Add(label);
                    foldout.value = true;
                }
                if (isHaveDir)
                {
                    var label = new Label();
                    label.text = "[警告]Directionalの場合、Normalmap用のテクスチャを生成する為、品質は向上しますが、Textureサイズは倍になります。";
                    label.style.color = Color.yellow;
                    foldout.Add(label);
                    label = new Label();
                    label.text = "この設定とは別に環境光ではNormalmapが反映される為、Enviroment ReflectionsのSourcrをSkyboxに設定すればある程度の品質は確保出来る可能性があります。";
                    foldout.Add(label);
                    foldout.value = true;
                }
            } else
            {
                foldout.text += "Non-Directional";
            }

            {
                var label = new Label();
                foldout.Add(label);
                label.text = "Directionalを選択した場合、法線用のライトマップを追加でBakeします。";                
            }

            return foldout.value;
        }


        bool LightingLightmappingSettingsAO(VisualElement parent)
        {
            var so = getLightmapSettings();
            var ambientOcclusion = so.FindProperty("m_LightmapEditorSettings.m_AO");

            var foldout = new Foldout();
            foldout.text = "Ambient Occlusion : " + ambientOcclusion.boolValue;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);

            if (ambientOcclusion.boolValue == false)
            {
                var label = new Label();
                label.text = "[確認]Ambient Occlusionが無効になっています。";
                label.style.color = Color.green;
                foldout.Add(label);
                foldout.value = true;
            }
            {
                var label = new Label();
                label.text = "遮蔽領域による周辺の光の減衰をライトマップに書き込むことによってより現実的な絵作りが期待出来ます。";
                foldout.Add(label);                
            }
            return foldout.value;
        }


        bool LightingLightmappingSettingsIndirectIntensity(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Indirect Intensity : " + Lightmapping.indirectOutputScale;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "リアルタイムおよびベイク処理されたライトマップに保存されている間接光の明るさを制御します。" ;
                label.text += "値が大きくなるとBake時間が伸びます。";                
            }

            return foldout.value;
        }


        bool LightingLightmappingSettingsAlbedoBoost(VisualElement parent)
        {

            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Albedo Boost : " + Lightmapping.bounceBoost;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "サーフェース間で反射する際の係数を指定します。";
                label.text += "\n1.0が物理的に正しい値です。";
            }

            return foldout.value;
        }        
        

        bool LightingLightmappingSettingsProgressivePrioritizeView(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Prioritize View : " + LightmapEditorSettings.prioritizeView;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "有効な場合、ScneViewに移る範囲からBakeを行います。";
            }
            return foldout.value;
        }


        bool LightingLightmappingSettingsProgressiveDirectSamples(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Direct Samples : " + LightmapEditorSettings.directSampleCount;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "各テクセルから放たれる直接光(Light)のサンプル（パス）数";
                label.text += "数値が高いと品質は上がりますが、Bake時間が伸びます。";
            }
            return foldout.value;
        }


        bool LightingLightmappingSettingsProgressiveIndirectSamples(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Indirect Samples : " + LightmapEditorSettings.indirectSampleCount;
            if (LightmapEditorSettings.indirectSampleCount > 100)
            {
                var label = new Label();
                label.style.color = Color.yellow;
                foldout.Add(label);
                label.text = "[警告]Indirect Samplesの値が100を超えています。";
                foldout.value = true;
            }            
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "各テクセルから放たれる間接光(ReflectionProbe)のサンプル（パス）数";
                label.text += "数値が高いと品質は上がりますが、Bake時間が伸びます。";
                label.text += "一般的には100未満の値で十分ですが、ノイズが載る場合は512から初めて、ノイズが載らない値まで調整を行って下さい。";
            }

            return foldout.value;
        }


        bool LightingLightmappingSettingsProgressiveIndirectBounces(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Bounces : " + LightmapEditorSettings.bounces;
            if (LightmapEditorSettings.bounces > 2)
            {
                var label = new Label();
                foldout.Add(label);
                label.style.color = Color.yellow;
                label.text = "[警告]bouncesが2を超えています。";
            }
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "間接光のサンプル(パス)が何回反射するかを指定します。";
                label.text += "\n反射回数が多いとBake時間が伸びます";
                label.text += "\n通常は２回で十分ですが室内のような反射物が多い場合は増やす必要がある場合があります。";
            }

            return foldout.value;
        }


        bool LightingLightmappingSettingsProgressiveFiltering(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Filtering : " + LightmapEditorSettings.filteringMode;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "ライトマップの後処理を構成して、ノイズを制限します。";
                label.text += "\nAUTOの場合、Advanceのデフォルト値が使用されます。";
                label.text += "Advanceでは直接光・間接光・環境光のそれぞれに対してフィルターをNone,Gaussian,A-Toursから指定可能です。。";
                label.text += "A-Toursではエッジを認識しますが、Gaussianは認識しない為、A-Toursはメリハリがあり、Gaussianはボヤっとした傾向になるようです。";
            }

            return foldout.value;
        }


        bool LightingLightmappingSettingsProgressiveIndirectResolution(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Indirect Resolution : " + LightmapEditorSettings.realtimeResolution;
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "Realtime GIが有効な場合に使用されるRuntimeで生成されるライトマップの解像度です。";
            }

            return foldout.value;
        }

       

        bool LightType(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);
            foldout.text = "Type";

            var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
            List<Light> lights = new List<Light>(objs);
            lights = lights.FindAll(light => light.gameObject.activeInHierarchy).FindAll(light => light.type == UnityEngine.LightType.Disc);
            if((LightmapEditorSettings.lightmapper == LightmapEditorSettings.Lightmapper.Enlighten) && (lights.Count != 0))
            {
                var label = new Label();
                label.style.color = Color.yellow;
                foldout.Add(label);
                label.text = "LightmapperにEnlightenが設定されていますが、Discライトが存在します。";
                label.text += "\nDiscライトはEnlightenではサポートされていません。";
                var f = new Foldout();
                f.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                f.value = false;
                foldout.Add(f);
                f.text = "Disc : " + lights.Count;
                foreach(var light in lights)
                {
                    var l = new Label();
                    l.text = light.name;
                    f.Add(l);
                }
                foldout.value = true;
            }
            return foldout.value;
        }


        bool LightBakeType(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "BakeType";
            parent.Add(foldout);


            var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
            List<Light> lights = new List<Light>(objs);
            lights = lights.FindAll(light => light.gameObject.activeInHierarchy);

            Foldout[] foldouts = new Foldout[3];
            string[] names = {"Realtime","Baked","Mixed" };
            for(var i = 0; i < foldouts.Length; i++)
            {
                foldouts[i] = new Foldout();
                foldouts[i].Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                foldouts[i].value = false;
                foldout.Add(foldouts[i]);
                foldouts[i].text = names[i];
                foldouts[i].value = false;
            }
            foreach(var light in lights)
            {
                int i;
                var label = new Label();

                if (light.lightmapBakeType == LightmapBakeType.Realtime)
                {
                    
                    i = 0;
                } else if(light.lightmapBakeType == LightmapBakeType.Baked)
                {
                    i = 1;
                } else
                {
                    i = 2;
                }
                label.text = light.name;
                foldouts[i].Add(label);
            }          
            return foldout.value;
        }


        bool LightIndirectMultiplier(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.value = false;
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.text = "Indirect Multiplier";
            parent.Add(foldout);

            var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
            List<Light> lights = new List<Light>(objs);
            lights = lights.FindAll(light => light.gameObject.activeInHierarchy);

            var sub1 = new Foldout();
            sub1.value = false;
            sub1.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            sub1.text = "[警告]Sportライト、PointライトはRealtimeで投影する影に対応していません。";

            var sub2 = new Foldout();
            sub2.value = false;
            sub2.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            sub2.text = "[警告]1.0より大きい値が設定されています。";

            foreach (var light in lights)
            {
                if ((light.type == UnityEngine.LightType.Point || light.type == UnityEngine.LightType.Spot) && light.bounceIntensity > 0f)
                {
                    var label = new Label();
                    label.text = light.name + ":" + light.bounceIntensity;
                    sub1.Add(label);
                }
                if(light.bounceIntensity > 1f)
                {
                    var label = new Label();
                    label.text = light.name + ":" + light.bounceIntensity;
                    sub2.Add(label);
                }
            }
            if(sub1.childCount != 0)
            {                
                foldout.Add(sub1);
                foldout.value = true;
            }
            if(sub2.childCount != 0)
            {
                foldout.Add(sub2);
                foldout.value = true;
            }
            {
                var label = new Label();
                label.text = "間接光の係数として使用されます。";
                foldout.Add(label);
            }

            return foldout.value;
        }



        void LightShadowType(VisualElement parent)
        {
            var foldout = new Foldout();
            foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            foldout.value = false;
            parent.Add(foldout);

            var objs = (Light[])(Resources.FindObjectsOfTypeAll(typeof(Light)));
            List<Light> lights = new List<Light>(objs);
            lights = lights.FindAll(light => light.gameObject.activeInHierarchy);
            {
                var label = new Label();
                foldout.Add(label);
                label.text = "このライトが影を投影形式を指定します。";
                label.text += "\nSoft Shadow はフィルターをかけてからシャドウマップの内容にフィルターをかけてから描画する為、ジャギーが目立たなくなります。";
            }
            Foldout[] foldouts = new Foldout[3];
            string[] names = {"No Shadows","Hard Shadows","Soft Shadows"};
            for(var i = 0; i< foldouts.Length; i++)
            {
                foldouts[i] = new Foldout();
                foldouts[i].Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                foldouts[i].value = false;
                foldout.text = names[i];
                foldout.Add(foldouts[i]);
            }

            foreach(var light in lights)
            {
                int i = 0;
                if(light.shadows == LightShadows.None)
                {
                    i = 0;
                } else if(light.shadows == LightShadows.Hard)
                {
                    i = 1;
                } else if(light.shadows == LightShadows.Soft)
                {
                    i = 2;
                }
                var label = new Label();
                label.text = light.name;
                foldouts[i].Add(label);
            }
            for (var i = 0; i < foldouts.Length; i++)
            {
                foldout.text += " : " + foldouts[i].childCount;
            }
        }


        void MeshRendererCheck(VisualElement parent)
        {
#if UNITY_2019_1_OR_NEWER
            var meshRendererFoldout = new Foldout();
            meshRendererFoldout.name = "MeshRendererFoldout";
            meshRendererFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            parent.Add(meshRendererFoldout);
            meshRendererFoldout.text  = "MeshRenderer";           
            meshRendererFoldout.value = false;
            meshRenderers = new List<MeshRenderer>();
            var objs = (MeshRenderer[])(Resources.FindObjectsOfTypeAll(typeof(MeshRenderer)));
            foreach (var meshRenderer in objs)
            {
                if (meshRenderer.gameObject.activeInHierarchy == false)
                {
                    continue;
                }
                if (meshRenderer.gameObject.isStatic == false)
                {
                    continue;
                }
                meshRenderers.Add(meshRenderer);                
            }
            // StaticなGameObjectの一覧
            var staticMeshRendererFoldout = new Foldout();
            staticMeshRendererFoldout.value = false;
            staticMeshRendererFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
            staticMeshRendererFoldout.text = "Static Mesh Renderer : " + meshRenderers.Count + " 個";
            staticMeshRendererFoldout.text += "\nStaticにチェックが入っているGameObjectに含まれるMeshRendeの数です。";
            staticMeshRendererFoldout.text += "\nStaticなGameObjectがLightmapのBakeの対象となります。";
            foreach (var meshRenderer in meshRenderers)
            {
                var label = new Label();
                label.text = meshRenderer.name;
                staticMeshRendererFoldout.Add(label);
            }
            meshRendererFoldout.Add(staticMeshRendererFoldout);
            
           
            // Lightingに関する項目
            {
                var lightingFoldout = new Foldout();
                lightingFoldout.name = "lightingFoldout";
                lightingFoldout.value = false;
                lightingFoldout.text = "Lighting";
                lightingFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                meshRendererFoldout.Add(lightingFoldout);

                // ReceiveGlobalIlluminationに関する項目
                {
                    var receiveGlobalIlluminationFoldot = new Foldout();
                    receiveGlobalIlluminationFoldot.name = "receiveGlobalIlluminationFoldot";
                    receiveGlobalIlluminationFoldot.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                    lightingFoldout.Add(receiveGlobalIlluminationFoldot);
                    receiveGlobalIlluminationFoldot.value = false;
                    receiveGlobalIlluminationFoldot.text = "ReceiveGlobalIllumination";
                    receiveGlobalIlluminationFoldot.text += "\n重要なオブジェクトにみLightmapのBake対象にしましょう。";
                    receiveGlobalIlluminationFoldot.text += "Scene内にある小さなオブジェクトはLightProbeを使用することを検討して下さい。";
                    receiveGlobalIlluminationFoldot.text += "Receive Global Illumination を LightProbe にすることによってLightProbeによってライティングされるようになり、LightmapのサイズとBake時間の削減が期待出来ます。";

                    receiveLightmapsMeshRenderers = new List<MeshRenderer>();
                    receiveLightProbesMeshRenderers = new List<MeshRenderer>();
                    foreach (var meshRenderer in meshRenderers)
                    {

                        if (meshRenderer.receiveGI == ReceiveGI.Lightmaps)
                        {
                            receiveLightmapsMeshRenderers.Add(meshRenderer);
                        }
                        else
                        {
                            receiveLightProbesMeshRenderers.Add(meshRenderer);
                        }
                    }

                    // ReceiveGlobalIlluminationの設定がLightmapsなMeshRendererの一覧
                    {
                        var receiveLightmapsMeshRenderersFoldout = new Foldout();
                        receiveLightmapsMeshRenderersFoldout.name = "receiveLightmapsMeshRenderersFoldout";
                        receiveLightmapsMeshRenderersFoldout.value = false;
                        receiveLightmapsMeshRenderersFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                        receiveLightmapsMeshRenderersFoldout.text = "ReceiveLightmaps : " + receiveLightmapsMeshRenderers.Count + " 個";
                        receiveGlobalIlluminationFoldot.Add(receiveLightmapsMeshRenderersFoldout);
                        foreach (var meshRenderer in receiveLightmapsMeshRenderers)
                        {
                            var label = new Label();
                            label.text = meshRenderer.name;
                            receiveLightmapsMeshRenderersFoldout.Add(label);
                        }
                    }
                    // ReceiveGlobalIlluminationの設定がLightmapsなLightProbeの一覧
                    {
                        var receiveLightProbesMeshRenderersFoldout = new Foldout();
                        receiveLightProbesMeshRenderersFoldout.name = "receiveLightProbesMeshRenderersFoldout";
                        receiveLightProbesMeshRenderersFoldout.value = false;
                        receiveLightProbesMeshRenderersFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                        receiveLightProbesMeshRenderersFoldout.text = "ReceiveLightProbes : " + receiveLightProbesMeshRenderers.Count + " 個";
                        receiveGlobalIlluminationFoldot.Add(receiveLightProbesMeshRenderersFoldout);
                        foreach (var meshRenderer in receiveLightProbesMeshRenderers)
                        {
                            var label = new Label();
                            label.text = meshRenderer.name;
                            receiveLightProbesMeshRenderersFoldout.Add(label);
                        }
                    }
                }
            }
            // Lightmappingに関する項目
            {               

                var lightmappingFoldout = new Foldout();
                lightmappingFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                lightmappingFoldout.name = "lightmappingFoldout";
                lightmappingFoldout.value = false;
                lightmappingFoldout.text = "Lightmapping";
                meshRendererFoldout.Add(lightmappingFoldout);

                // Scale In Lightmapに関する項目
                {
                    var scaleInLightmapFoldout = new Foldout();
                    scaleInLightmapFoldout.name = "scaleInLightmapFoldout";
                    scaleInLightmapFoldout.value = false;
                    scaleInLightmapFoldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                    lightmappingFoldout.Add(scaleInLightmapFoldout);
                    scaleInLightmapFoldout.text = "ScaleInLightmap";
                    scaleInLightmapFoldout.text += "\nこの値はLightmap Resolutionの係数として使用されます。全体に対してこのオブジェクトの解像度を調整したい場合に1以外の値を設定して下さい。";
                    scaleInLightmapFoldout.text += "\n例えばLightmap Resolutionが10でScale In Lightmapが2の場合、このオブジェクトは10 * 2 = 20texels per unitとして処理されます。";
                    scaleInLightmapFoldout.text += "\nこの項目を展開することでスケールの値とそのスケールを指定しているMeshRendererの個数が確認出来ます。";
                    scaleInLightmapFoldout.text += "\nこの値が0のオブジェクトがBakeの対象外となります。";

                    scaleInLightmapDict = new SortedDictionary<float, int>();
                    foreach (var meshRenderer in receiveLightmapsMeshRenderers)
                    {
                        // ScaleInLightmapの分布を収集する
                        var so = new SerializedObject(meshRenderer);
                        var f = so.FindProperty("m_ScaleInLightmap").floatValue;


                        // floatをKeyにするってどうなのと思うが・・・
                        if (scaleInLightmapDict.ContainsKey(f) == true)
                        {
                            scaleInLightmapDict[f]++;
                        }
                        else
                        {
                            scaleInLightmapDict.Add(f, 1);
                        }
                    }

                    foreach (var item in scaleInLightmapDict)
                    {
                        var foldout = new Foldout();
                        foldout.Query<Toggle>().AtIndex(0).style.marginLeft = 0;
                        scaleInLightmapFoldout.Add(foldout);
                        foldout.value = false;
                        foldout.text = item.Key.ToString() + " : " + item.Value.ToString();
                        foreach (var meshRenderer in receiveLightmapsMeshRenderers)
                        {
                            var so = new SerializedObject(meshRenderer);
                            var f = so.FindProperty("m_ScaleInLightmap").floatValue;
                            if (item.Key == f)
                            {
                                var label = new Label();
                                label.text = meshRenderer.name;
                                foldout.Add(label);
                            }
                        }
                    }

                }
            }
#endif
        }
    }
#endif
}
