using System;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

// Собирает весь проект одной командой: меню «AR Game → Setup Project».
// Создаёт материалы, префабы (башня, шар, AR-плоскость), сцену с AR Session / XR Origin / UI
// и настраивает Android + ARCore. «AR Game → Build APK» собирает Builds/ARBallGame.apk.
public static class ARGameSetup
{
    const string Root = "Assets/ARGame";
    const string ScenePath = "Assets/Scenes/ARGame.unity";
    const string ApkPath = "Builds/ARBallGame.apk";

    // ---------- точки входа ----------

    [MenuItem("AR Game/Setup Project")]
    public static void SetupProject()
    {
        ConfigurePlayerSettings();
        ConfigureIcons();
        ConfigureXR();
        var mats = CreateMaterials();
        var planePrefab = CreatePlanePrefab(mats.plane);
        var ballPrefab = CreateBallPrefab(mats.ball);
        var towerPrefab = CreateTowerPrefab(mats.blocks);
        var shapes = ARGameShapes.CreateAll(Root, towerPrefab);
        var audio = ARGameAudio.CreateAll(Root);
        CreateScene(planePrefab, ballPrefab, shapes, audio);
        AssetDatabase.SaveAssets();
        Debug.Log("[ARGameSetup] Проект настроен.");
    }

    [MenuItem("AR Game/Build APK")]
    public static void BuildApk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        EditorUserBuildSettings.buildAppBundle = false;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        });
        Debug.Log($"[ARGameSetup] Сборка: {report.summary.result}, ошибок: {report.summary.totalErrors}, файл: {Path.GetFullPath(ApkPath)}");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result);
    }

    // для запуска из командной строки (-executeMethod)
    public static void SetupCLI() => RunCLI(SetupProject);
    public static void BuildCLI() => RunCLI(BuildApk);
    public static void SetupAndBuildCLI() => RunCLI(() => { SetupProject(); BuildApk(); });

    static void RunCLI(Action action)
    {
        try { action(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    // ---------- настройки ----------

    static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "Ilya";
        PlayerSettings.productName = "AR Ball Game";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ilya.arballgame");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel26)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
    }

    // Иконка приложения. Картинки рисует Tools/make_icon.py → Assets/ARGame/AppIcon.
    static void ConfigureIcons()
    {
        const string dir = Root + "/AppIcon";
        Texture2D Load(string name)
        {
            string path = $"{dir}/{name}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) throw new Exception("Нет иконки: " + path + " (запусти Tools/make_icon.py)");
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 1024;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        var full = Load("icon_full");
        var bg = Load("icon_bg");
        var fg = Load("icon_fg");

        // Иконка по умолчанию (для всех платформ)
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

        var android = NamedBuildTarget.Android;

        // Адаптивная (Android 8+): слой фона + слой переднего плана
        var adaptive = PlayerSettings.GetPlatformIcons(android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive);
        foreach (var icon in adaptive) icon.SetTextures(bg, fg);
        PlayerSettings.SetPlatformIcons(android, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, adaptive);

        // Круглая и «старая» иконки в Unity 6.6 больше не используются: игре нужен Android 8+,
        // а там всегда работает адаптивная.
        Debug.Log($"[ARGameSetup] Иконки: adaptive {adaptive.Length}");
    }

    static void ConfigureXR()
    {
        const string loaderType = "UnityEngine.XR.ARCore.ARCoreLoader";

        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
        if (perTarget == null)
        {
            EnsureFolder("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
        }

        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

        var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        general.InitManagerOnStart = true;
        var manager = general.Manager;
        bool ok = XRPackageMetadataStore.AssignLoader(manager, loaderType, BuildTargetGroup.Android);
        EditorUtility.SetDirty(general);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(perTarget);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ARGameSetup] ARCore loader assigned: {ok}");

        // Depth API не нужен игре: делаем его необязательным, иначе APK не встанет
        // на телефоны без поддержки глубины. (0 = Required, 1 = Optional)
        var arcoreSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/XR/Settings/ARCoreSettings.asset");
        if (arcoreSettings != null)
        {
            var so = new SerializedObject(arcoreSettings);
            so.FindProperty("m_Requirement").enumValueIndex = 0; // ARCore обязателен
            so.FindProperty("m_Depth").enumValueIndex = 1;       // глубина — опционально
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }

    // ---------- ассеты ----------

    struct Mats { public Material plane, ball; public Material[] blocks; }

    static Mats CreateMaterials()
    {
        EnsureFolder(Root + "/Materials");

        var plane = SaveMaterial("Plane", Shader.Find("Sprites/Default"), new Color(1f, 1f, 1f, 0.22f));
        var ball = SaveMaterial("Ball", Shader.Find("Standard"), new Color(0.95f, 0.25f, 0.2f));
        ball.SetFloat("_Glossiness", 0.8f);

        Color[] palette =
        {
            new Color(0.98f, 0.78f, 0.22f), // жёлтый
            new Color(0.25f, 0.62f, 0.95f), // синий
            new Color(0.35f, 0.82f, 0.45f), // зелёный
            new Color(0.93f, 0.45f, 0.75f), // розовый
        };
        var blocks = new Material[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            blocks[i] = SaveMaterial("Block" + i, Shader.Find("Standard"), palette[i]);
            blocks[i].SetFloat("_Glossiness", 0.35f);
        }
        return new Mats { plane = plane, ball = ball, blocks = blocks };
    }

    static Material SaveMaterial(string name, Shader shader, Color color)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject CreatePlanePrefab(Material mat)
    {
        var go = new GameObject("AR Plane");
        go.AddComponent<ARPlane>();
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>();
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.005f;
        lr.sharedMaterial = mat;
        lr.startColor = lr.endColor = Color.white;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<ARPlaneMeshVisualizer>();
        return SavePrefab(go, "AR Plane");
    }

    static GameObject CreateBallPrefab(Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Ball";
        go.transform.localScale = Vector3.one * 0.07f;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var impact = go.AddComponent<ImpactSound>();
        var so = new SerializedObject(impact);
        so.FindProperty("isBall").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return SavePrefab(go, "Ball");
    }

    static GameObject CreateTowerPrefab(Material[] mats)
    {
        const float size = 0.08f;   // блок 8 см
        const int levels = 4;       // пирамида 4x4 + 3x3 + 2x2 + 1 = 30 блоков
        const float gap = 0.001f;

        var root = new GameObject("Tower");
        // Башня — одна из фигур на панели (готовая пирамида из 30 блоков, как в видео).
        // Невидимый пол под постройкой теперь создаёт BuildController.
        var piece = root.AddComponent<BuildPiece>();
        piece.extents = new Vector3(levels * size * 0.5f, levels * (size + gap), levels * size * 0.5f);
        piece.randomTint = false;

        for (int level = 0; level < levels; level++)
        {
            int count = levels - level;
            float offset = (count - 1) * size * 0.5f;
            float y = size * 0.5f + level * (size + gap);
            for (int x = 0; x < count; x++)
            for (int z = 0; z < count; z++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Block_{level}_{x}_{z}";
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = new Vector3(x * size - offset, y, z * size - offset);
                block.transform.localScale = Vector3.one * size;
                block.GetComponent<MeshRenderer>().sharedMaterial = mats[(level + x + z) % mats.Length];

                var rb = block.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                var enable = block.AddComponent<EnablePhysicsOnEvent>();
                SetRef(enable, "rb", rb);
                var reset = block.AddComponent<ResetRbPositionOfObject>();
                SetRef(reset, "rb", rb);
                block.AddComponent<ImpactSound>();
            }
        }
        return SavePrefab(root, "Tower");
    }

    internal static GameObject SavePrefab(GameObject go, string name)
    {
        EnsureFolder(Root + "/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------- сцена ----------

    static void CreateScene(GameObject planePrefab, GameObject ballPrefab,
        System.Collections.Generic.List<ARGameShapes.Shape> shapes, ARGameAudio.Clips audio)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Звуки
        var soundGo = new GameObject("SoundManager");
        var sound = soundGo.AddComponent<SoundManager>();
        var sso = new SerializedObject(sound);
        sso.FindProperty("place").objectReferenceValue = audio.place;
        sso.FindProperty("remove").objectReferenceValue = audio.remove;
        sso.FindProperty("click").objectReferenceValue = audio.click;
        sso.FindProperty("shoot").objectReferenceValue = audio.shoot;
        sso.FindProperty("start").objectReferenceValue = audio.start;
        sso.FindProperty("reset").objectReferenceValue = audio.reset;
        sso.FindProperty("win").objectReferenceValue = audio.win;
        sso.FindProperty("ballHit").objectReferenceValue = audio.ballHit;
        var hits = sso.FindProperty("woodHits");
        hits.arraySize = audio.woodHits.Length;
        for (int i = 0; i < audio.woodHits.Length; i++)
            hits.GetArrayElementAtIndex(i).objectReferenceValue = audio.woodHits[i];
        sso.ApplyModifiedPropertiesWithoutUndo();

        // Свет
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // AR Session
        var session = new GameObject("AR Session");
        session.AddComponent<ARSession>();
        session.AddComponent<ARInputManager>();

        // XR Origin + камера
        var originGo = new GameObject("XR Origin");
        var origin = originGo.AddComponent<XROrigin>();
        var offset = new GameObject("Camera Offset");
        offset.transform.SetParent(originGo.transform, false);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(offset.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 20f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<ARCameraManager>();
        camGo.AddComponent<ARCameraBackground>();
        var tpd = camGo.AddComponent<TrackedPoseDriver>();
        var pos = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
        pos.AddBinding("<HandheldARInputDevice>/devicePosition");
        var rot = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
        rot.AddBinding("<HandheldARInputDevice>/deviceRotation");
        tpd.positionInput = new InputActionProperty(pos);
        tpd.rotationInput = new InputActionProperty(rot);

        origin.CameraFloorOffsetObject = offset;
        origin.Camera = cam;

        var planeManager = originGo.AddComponent<ARPlaneManager>();
        planeManager.planePrefab = planePrefab;
        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
        originGo.AddComponent<ARRaycastManager>();

        var build = originGo.AddComponent<BuildController>();
        var shoot = originGo.AddComponent<ShootBallLogic>();
        SetRef(shoot, "ballPrefab", ballPrefab);

        // UI
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var hint = CreatePanel(canvasGo.transform, "HintPanel", 200, -110, 42);
        hint.resizeTextForBestFit = true;
        hint.resizeTextMinSize = 24;
        hint.resizeTextMaxSize = 42;
        hint.verticalOverflow = VerticalWrapMode.Truncate;
        var score = CreatePanel(canvasGo.transform, "ScorePanel", 130, -110, 56);

        // --- Панель строительства: ряд инструментов + лента фигур ---
        var buildPanel = new GameObject("BuildPanel", typeof(RectTransform));
        buildPanel.transform.SetParent(canvasGo.transform, false);
        var bpRt = (RectTransform)buildPanel.transform;
        bpRt.anchorMin = Vector2.zero;
        bpRt.anchorMax = Vector2.one;
        bpRt.offsetMin = bpRt.offsetMax = Vector2.zero;

        // Лента фигур (горизонтальная прокрутка)
        var palette = new GameObject("Palette", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        palette.transform.SetParent(buildPanel.transform, false);
        var palRt = (RectTransform)palette.transform;
        palRt.anchorMin = new Vector2(0f, 0f);
        palRt.anchorMax = new Vector2(1f, 0f);
        palRt.pivot = new Vector2(0.5f, 0f);
        palRt.sizeDelta = new Vector2(-40f, 240f);
        palRt.anchoredPosition = new Vector2(0f, 40f);
        var palImg = palette.GetComponent<Image>();
        palImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        palImg.type = Image.Type.Sliced;
        palImg.color = new Color(0f, 0f, 0f, 0.35f);

        var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(palette.transform, false);
        var cRt = (RectTransform)content.transform;
        cRt.anchorMin = new Vector2(0f, 0f);
        cRt.anchorMax = new Vector2(0f, 1f);
        cRt.pivot = new Vector2(0f, 0.5f);
        cRt.sizeDelta = Vector2.zero;
        var hlg = content.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(15, 15, 15, 15);
        hlg.spacing = 15;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = palette.GetComponent<ScrollRect>();
        scroll.content = cRt;
        scroll.viewport = palRt;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        for (int i = 0; i < shapes.Count; i++)
            CreatePaletteButton(content.transform, i, shapes[i]);

        // Ряд инструментов
        var tools = new GameObject("Tools", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tools.transform.SetParent(buildPanel.transform, false);
        var tRt = (RectTransform)tools.transform;
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0f);
        tRt.pivot = new Vector2(0.5f, 0f);
        tRt.sizeDelta = new Vector2(-40f, 140f);
        tRt.anchoredPosition = new Vector2(0f, 300f);
        var thlg = tools.GetComponent<HorizontalLayoutGroup>();
        thlg.spacing = 15;
        thlg.childControlWidth = thlg.childControlHeight = true;
        thlg.childForceExpandWidth = thlg.childForceExpandHeight = true;

        Color toolColor = new Color(0.22f, 0.24f, 0.3f, 0.92f);
        var undoBtn = CreateLayoutButton(tools.transform, "UndoButton", "Отмена", toolColor, 40, 1f);
        var rotateBtn = CreateLayoutButton(tools.transform, "RotateButton", "Повернуть", toolColor, 40, 1.15f);
        var startBtn = CreateLayoutButton(tools.transform, "StartButton", "СТАРТ", new Color(0.2f, 0.72f, 0.35f), 56, 1.3f);
        var sc = startBtn.colors;
        sc.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f); // серый, пока нечего запускать
        startBtn.colors = sc;
        var clearBtn = CreateLayoutButton(tools.transform, "ClearButton", "Очистить", toolColor, 40, 1.1f);

        // Привязываем фигуры и кнопки к BuildController
        var so = new SerializedObject(build);
        var list = so.FindProperty("shapes");
        list.arraySize = shapes.Count;
        for (int i = 0; i < shapes.Count; i++)
        {
            var el = list.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("name").stringValue = shapes[i].name;
            el.FindPropertyRelative("prefab").objectReferenceValue = shapes[i].prefab;
        }
        so.FindProperty("undoButton").objectReferenceValue = undoBtn;
        so.FindProperty("rotateButton").objectReferenceValue = rotateBtn;
        so.FindProperty("clearButton").objectReferenceValue = clearBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- Кнопки режима игры ---
        // «Сброс» — слева фиксированной ширины, «ОГОНЬ!» занимает всё остальное место справа.
        // Так кнопки не пересекаются на экране любой ширины.
        var shootBtn = CreateButton(canvasGo.transform, "ShootButton", "ОГОНЬ!",
            new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(460, 200), new Color(0.9f, 0.25f, 0.2f), 72);
        var shootRt = (RectTransform)shootBtn.transform;
        shootRt.anchorMin = new Vector2(0f, 0f);
        shootRt.anchorMax = new Vector2(1f, 0f);
        shootRt.pivot = new Vector2(0.5f, 0f);
        shootRt.offsetMin = new Vector2(30f + 220f + 30f, 150f); // слева: отступ + ширина «Сброса» + зазор
        shootRt.offsetMax = new Vector2(-30f, 150f + 200f);

        var resetBtn = CreateButton(canvasGo.transform, "ResetButton", "Сброс",
            new Vector2(0f, 0f), new Vector2(30, 150), new Vector2(220, 200), new Color(0.3f, 0.3f, 0.35f, 0.9f), 44);
        var resetRt = (RectTransform)resetBtn.transform;
        resetRt.pivot = new Vector2(0f, 0f);
        resetRt.anchoredPosition = new Vector2(30f, 150f);

        var ui = canvasGo.AddComponent<UIButtonHandler>();
        SetRef(ui, "UIStartButton", startBtn);
        SetRef(ui, "UIShootButton", shootBtn);
        SetRef(ui, "UIResetButton", resetBtn);
        SetRef(ui, "buildPanel", buildPanel);
        SetRef(ui, "hintText", hint);

        var counter = canvasGo.AddComponent<ScoreCounter>();
        SetRef(counter, "scoreText", score);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void CreatePaletteButton(Transform parent, int index, ARGameShapes.Shape shape)
    {
        var go = new GameObject("Shape_" + shape.name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 170;
        le.preferredHeight = 210;
        var bg = go.GetComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var iRt = (RectTransform)icon.transform;
        iRt.anchorMin = iRt.anchorMax = new Vector2(0.5f, 1f);
        iRt.pivot = new Vector2(0.5f, 1f);
        iRt.sizeDelta = new Vector2(130, 130);
        iRt.anchoredPosition = new Vector2(0, -12);
        var iImg = icon.GetComponent<Image>();
        iImg.sprite = shape.icon;
        iImg.color = shape.color;
        iImg.preserveAspect = true;
        iImg.raycastTarget = false;

        var label = CreateText(go.transform, shape.name, 30);
        var lRt = (RectTransform)label.transform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(1f, 0f);
        lRt.pivot = new Vector2(0.5f, 0f);
        lRt.offsetMin = new Vector2(4, 8);
        lRt.offsetMax = new Vector2(-4, 58);

        // Шаблоны построек — на тёмно-синем фоне, чтобы отличались от отдельных фигур
        Color normal = shape.isTemplate ? new Color(0.05f, 0.14f, 0.35f, 0.75f) : new Color(0f, 0f, 0f, 0.55f);
        bg.color = normal;

        var pb = go.AddComponent<PaletteButton>();
        var so = new SerializedObject(pb);
        so.FindProperty("normalColor").colorValue = normal;
        so.FindProperty("index").intValue = index;
        so.FindProperty("background").objectReferenceValue = bg;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Button CreateLayoutButton(Transform parent, string name, string label, Color color, int fontSize, float flex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.flexibleWidth = flex;
        le.minWidth = 100;
        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        var text = CreateText(go.transform, label, fontSize);
        text.fontStyle = FontStyle.Bold;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 20;
        text.resizeTextMaxSize = fontSize;
        return go.GetComponent<Button>();
    }

    static Text CreatePanel(Transform parent, string name, float height, float y, int fontSize)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-80f, height);
        rt.anchoredPosition = new Vector2(0f, y);
        var img = panel.GetComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = new Color(0f, 0f, 0f, 0.55f);
        img.raycastTarget = false;
        return CreateText(panel.transform, "", fontSize);
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchor,
        Vector2 pos, Vector2 size, Color color, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        var text = CreateText(go.transform, label, fontSize);
        text.fontStyle = FontStyle.Bold;
        return go.GetComponent<Button>();
    }

    static Text CreateText(Transform parent, string content, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(20, 10);
        rt.offsetMax = new Vector2(-20, -10);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // ---------- утилиты ----------

    internal static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) throw new Exception($"Поле {field} не найдено в {target.GetType().Name}");
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
