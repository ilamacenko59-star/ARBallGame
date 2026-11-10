using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Создаёт набор строительных фигур (префабы + иконки для нижней панели).
// У всех фигур пивот — центр нижней грани, поэтому их удобно ставить на пол и друг на друга.
public static class ARGameShapes
{
    public class Shape
    {
        public string name;
        public GameObject prefab;
        public Sprite icon;
        public Color color;
        public bool isTemplate;
    }

    const float Density = 150f; // кг/м³ — лёгкие «деревянные» фигуры, чтобы шар их сбивал

    static string root;
    static Material pieceMat;

    public static List<Shape> CreateAll(string rootFolder, GameObject towerPrefab)
    {
        root = rootFolder;
        ARGameSetup.EnsureFolder(root + "/Meshes");
        ARGameSetup.EnsureFolder(root + "/Icons");
        ARGameSetup.EnsureFolder(root + "/Prefabs/Shapes");

        pieceMat = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/Piece.mat");
        if (pieceMat == null)
        {
            pieceMat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(pieceMat, root + "/Materials/Piece.mat");
        }
        pieceMat.color = Color.white;
        pieceMat.SetFloat("_Glossiness", 0.3f);
        EditorUtility.SetDirty(pieceMat);

        var list = new List<Shape>();

        // Куб 10 см
        list.Add(Make("Куб", new Color(0.98f, 0.78f, 0.22f), IconCube,
            new Vector3(0.05f, 0.1f, 0.05f), go => AddBox(go, new Vector3(0.1f, 0.1f, 0.1f), 0.05f)));

        // Брусок 30×5×10 см — для стен и перекрытий
        list.Add(Make("Брусок", new Color(0.96f, 0.55f, 0.25f), IconPlank,
            new Vector3(0.15f, 0.05f, 0.05f), go => AddBox(go, new Vector3(0.3f, 0.05f, 0.1f), 0.025f)));

        // Колонна 6×30×6 см
        list.Add(Make("Колонна", new Color(0.25f, 0.62f, 0.95f), IconColumn,
            new Vector3(0.03f, 0.3f, 0.03f), go => AddBox(go, new Vector3(0.06f, 0.3f, 0.06f), 0.15f)));

        // Домино 10×20×3 см
        list.Add(Make("Домино", new Color(0.95f, 0.95f, 0.95f), IconDomino,
            new Vector3(0.05f, 0.2f, 0.015f), go => AddBox(go, new Vector3(0.1f, 0.2f, 0.03f), 0.1f)));

        // Плита 30×2×30 см — пол для следующего этажа
        list.Add(Make("Плита", new Color(0.6f, 0.45f, 0.9f), IconPlate,
            new Vector3(0.15f, 0.02f, 0.15f), go => AddBox(go, new Vector3(0.3f, 0.02f, 0.3f), 0.01f)));

        // Цилиндр Ø10 × 15 см
        list.Add(Make("Цилиндр", new Color(0.35f, 0.82f, 0.45f), IconCylinder,
            new Vector3(0.05f, 0.15f, 0.05f), go => AddCylinder(go, 0.1f, 0.15f)));

        // Шар Ø10 см
        list.Add(Make("Шар", new Color(0.93f, 0.45f, 0.75f), IconSphere,
            new Vector3(0.05f, 0.1f, 0.05f), go => AddSphere(go, 0.1f)));

        // Клин (горка) 15×10×15 см
        var wedge = SaveMesh(BuildWedge(0.15f, 0.1f, 0.15f), "Wedge");
        list.Add(Make("Горка", new Color(0.3f, 0.85f, 0.85f), IconWedge,
            new Vector3(0.075f, 0.1f, 0.075f), go => AddMesh(go, wedge)));

        // Пирамида 12×12 см
        var pyramid = SaveMesh(BuildPyramid(0.12f, 0.12f), "Pyramid");
        list.Add(Make("Пирамида", new Color(0.98f, 0.4f, 0.35f), IconPyramid,
            new Vector3(0.06f, 0.12f, 0.06f), go => AddMesh(go, pyramid)));

        // Арка 30×20×8 см (два столба + перекладина, одно тело)
        list.Add(Make("Арка", new Color(0.85f, 0.7f, 0.5f), IconArch,
            new Vector3(0.15f, 0.2f, 0.04f), go =>
            {
                AddBox(go, new Vector3(0.06f, 0.15f, 0.08f), 0.075f, -0.12f);
                AddBox(go, new Vector3(0.06f, 0.15f, 0.08f), 0.075f, 0.12f);
                AddBox(go, new Vector3(0.3f, 0.05f, 0.08f), 0.175f);
            }));

        // Готовая башня из видео (30 блоков)
        list.Add(new Shape
        {
            name = "Башня",
            prefab = towerPrefab,
            icon = SaveIcon("Tower", IconTower),
            color = new Color(1f, 0.85f, 0.4f),
            isTemplate = true,
        });

        // Готовые постройки из этих фигур
        list.AddRange(ARGameTemplates.CreateAll(root, list));

        AssetDatabase.SaveAssets();
        return list;
    }

    // ---------- сборка префабов ----------

    static Shape Make(string name, Color color, Func<float, float, bool> icon, Vector3 extents, Action<GameObject> build)
    {
        string id = IdFor(name);
        var go = new GameObject(id);
        build(go);

        var piece = go.AddComponent<BuildPiece>();
        piece.extents = extents;
        piece.randomTint = true;

        float volume = 0f;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var s = r.bounds.size;
            volume += s.x * s.y * s.z;
        }

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.03f, volume * Density);
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.solverIterations = 20;          // устойчивые высокие стопки (Дженга, замок)
        rb.solverVelocityIterations = 4;
        rb.sleepThreshold = 0.02f;         // мелкие фигуры быстрее «засыпают» и не ползут
        foreach (var col in go.GetComponentsInChildren<Collider>())
        {
            col.sharedMaterial = FrictionMaterial();
            col.contactOffset = 0.003f;    // по умолчанию 1 см — много для деталей в 5 см
        }
        go.AddComponent<EnablePhysicsOnEvent>();
        go.AddComponent<ResetRbPositionOfObject>();
        go.AddComponent<ImpactSound>();
        ARGameSetup.SetRef(go.GetComponent<EnablePhysicsOnEvent>(), "rb", rb);
        ARGameSetup.SetRef(go.GetComponent<ResetRbPositionOfObject>(), "rb", rb);

        // Цвет по умолчанию (в игре каждая фигура перекрашивается в случайный)
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{root}/Prefabs/Shapes/{id}.prefab");
        UnityEngine.Object.DestroyImmediate(go);

        return new Shape { name = name, prefab = prefab, icon = SaveIcon(id, icon), color = color };
    }

    static PhysicsMaterial frictionMat;

    // «Деревянное» трение без отскока
    static PhysicsMaterial FrictionMaterial()
    {
        if (frictionMat != null) return frictionMat;
        string path = root + "/Materials/PieceFriction.physicMaterial";
        frictionMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (frictionMat == null)
        {
            frictionMat = new PhysicsMaterial("PieceFriction");
            AssetDatabase.CreateAsset(frictionMat, path);
        }
        frictionMat.staticFriction = 0.9f;
        frictionMat.dynamicFriction = 0.7f;
        frictionMat.bounciness = 0f;
        frictionMat.frictionCombine = PhysicsMaterialCombine.Maximum;
        frictionMat.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(frictionMat);
        return frictionMat;
    }

    static string IdFor(string name)
    {
        switch (name)
        {
            case "Куб": return "Cube";
            case "Брусок": return "Plank";
            case "Колонна": return "Column";
            case "Домино": return "Domino";
            case "Плита": return "Plate";
            case "Цилиндр": return "Cylinder";
            case "Шар": return "Sphere";
            case "Горка": return "Wedge";
            case "Пирамида": return "Pyramid";
            case "Арка": return "Arch";
            default: return name;
        }
    }

    static void AddBox(GameObject parent, Vector3 size, float centerY, float centerX = 0f)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Part";
        box.transform.SetParent(parent.transform, false);
        box.transform.localPosition = new Vector3(centerX, centerY, 0f);
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = pieceMat;
    }

    static void AddCylinder(GameObject parent, float diameter, float height)
    {
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Part";
        cyl.transform.SetParent(parent.transform, false);
        cyl.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        cyl.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        cyl.GetComponent<MeshRenderer>().sharedMaterial = pieceMat;
        // У примитива капсульный коллайдер — заменяем на выпуклый меш, чтобы цилиндр стоял ровно
        UnityEngine.Object.DestroyImmediate(cyl.GetComponent<CapsuleCollider>());
        var mc = cyl.AddComponent<MeshCollider>();
        mc.sharedMesh = cyl.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;
    }

    static void AddSphere(GameObject parent, float diameter)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "Part";
        s.transform.SetParent(parent.transform, false);
        s.transform.localPosition = new Vector3(0f, diameter * 0.5f, 0f);
        s.transform.localScale = Vector3.one * diameter;
        s.GetComponent<MeshRenderer>().sharedMaterial = pieceMat;
    }

    static void AddMesh(GameObject parent, Mesh mesh)
    {
        var go = new GameObject("Part");
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = pieceMat;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = true;
    }

    // ---------- меши ----------

    // Клин: низ — прямоугольник, высокая сторона сзади (−Z), скат к игроку (+Z).
    static Mesh BuildWedge(float w, float h, float d)
    {
        float x = w / 2, z = d / 2;
        Vector3 bl = new Vector3(-x, 0, z), br = new Vector3(x, 0, z);     // передний нижний край
        Vector3 bbl = new Vector3(-x, 0, -z), bbr = new Vector3(x, 0, -z); // задний нижний край
        Vector3 tl = new Vector3(-x, h, -z), tr = new Vector3(x, h, -z);   // верхний край
        var faces = new List<Vector3[]>
        {
            new[] { bl, br, bbr, bbl },  // низ
            new[] { bbl, bbr, tr, tl },  // задняя стенка
            new[] { bl, br, tr, tl },    // скат
            new[] { bl, bbl, tl },       // левый бок
            new[] { br, bbr, tr },       // правый бок
        };
        return BuildConvexMesh(faces, new Vector3(0, h / 3, 0));
    }

    static Mesh BuildPyramid(float b, float h)
    {
        float x = b / 2;
        Vector3 a = new Vector3(-x, 0, -x), c = new Vector3(x, 0, -x), e = new Vector3(x, 0, x), f = new Vector3(-x, 0, x);
        Vector3 top = new Vector3(0, h, 0);
        var faces = new List<Vector3[]>
        {
            new[] { a, c, e, f },
            new[] { a, c, top }, new[] { c, e, top }, new[] { e, f, top }, new[] { f, a, top },
        };
        return BuildConvexMesh(faces, new Vector3(0, h / 4, 0));
    }

    // Плоские грани (свои вершины на каждую), ориентация треугольников — наружу от центра.
    static Mesh BuildConvexMesh(List<Vector3[]> faces, Vector3 center)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (var face in faces)
        {
            for (int i = 1; i < face.Length - 1; i++)
            {
                Vector3 p0 = face[0], p1 = face[i], p2 = face[i + 1];
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
                Vector3 mid = (p0 + p1 + p2) / 3f;
                // В Unity лицевая сторона — по часовой стрелке, т.е. Cross должен смотреть наружу
                if (Vector3.Dot(n, mid - center) < 0f) { var t = p1; p1 = p2; p2 = t; }
                int start = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{root}/Meshes/{name}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------- иконки (рисуются кодом, белый силуэт; цвет задаёт Image) ----------

    internal static Sprite SaveIcon(string id, Func<float, float, bool> inside)
    {
        const int size = 128;
        const int ss = 4; // сглаживание 4×4
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            int hit = 0;
            for (int sy = 0; sy < ss; sy++)
            for (int sx = 0; sx < ss; sx++)
            {
                float u = (px + (sx + 0.5f) / ss) / size;
                float v = (py + (sy + 0.5f) / ss) / size;
                if (inside(u, v)) hit++;
            }
            byte a = (byte)(255 * hit / (ss * ss));
            pixels[py * size + px] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        string path = $"{root}/Icons/{id}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    internal static bool Rect(float u, float v, float cx, float cy, float hw, float hh) =>
        Mathf.Abs(u - cx) < hw && Mathf.Abs(v - cy) < hh;

    internal static bool Circle(float u, float v, float cx, float cy, float r) =>
        (u - cx) * (u - cx) + (v - cy) * (v - cy) < r * r;

    static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return dx * dx + dy * dy < 1f;
    }

    static bool IconCube(float u, float v) => Rect(u, v, 0.5f, 0.5f, 0.32f, 0.32f);
    static bool IconPlank(float u, float v) => Rect(u, v, 0.5f, 0.5f, 0.44f, 0.12f);
    static bool IconColumn(float u, float v) => Rect(u, v, 0.5f, 0.5f, 0.11f, 0.44f);
    static bool IconDomino(float u, float v) =>
        Rect(u, v, 0.5f, 0.5f, 0.22f, 0.42f)
        && !Circle(u, v, 0.5f, 0.7f, 0.07f) && !Circle(u, v, 0.5f, 0.3f, 0.07f)
        && !Rect(u, v, 0.5f, 0.5f, 0.17f, 0.012f);
    static bool IconPlate(float u, float v) =>
        Mathf.Abs(v - 0.5f) < 0.1f && Mathf.Abs(u - 0.5f - (v - 0.5f) * 1.2f) < 0.32f;
    static bool IconCylinder(float u, float v) =>
        (Rect(u, v, 0.5f, 0.48f, 0.3f, 0.26f) || Ellipse(u, v, 0.5f, 0.22f, 0.3f, 0.1f) || Ellipse(u, v, 0.5f, 0.74f, 0.3f, 0.1f))
        && !(Mathf.Abs(Ellipse01(u, v, 0.5f, 0.74f, 0.3f, 0.1f) - 1f) < 0.12f && v < 0.74f);
    static float Ellipse01(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return dx * dx + dy * dy;
    }
    static bool IconSphere(float u, float v) =>
        Circle(u, v, 0.5f, 0.5f, 0.38f) && !Circle(u, v, 0.38f, 0.62f, 0.08f);
    static bool IconWedge(float u, float v) =>
        u > 0.12f && u < 0.88f && v > 0.18f && v - 0.18f < (u - 0.12f) / 0.76f * 0.62f;
    static bool IconPyramid(float u, float v) =>
        v > 0.16f && v < 0.84f && Mathf.Abs(u - 0.5f) < 0.4f * (0.84f - v) / 0.68f;
    static bool IconArch(float u, float v) =>
        Rect(u, v, 0.5f, 0.48f, 0.4f, 0.3f) && !Rect(u, v, 0.5f, 0.38f, 0.22f, 0.21f);
    static bool IconTower(float u, float v)
    {
        for (int k = 0; k < 4; k++)
        {
            float y0 = 0.12f + k * 0.19f;
            if (v > y0 && v < y0 + 0.17f && Mathf.Abs(u - 0.5f) < 0.42f * (4 - k) / 4f) return true;
        }
        return false;
    }
}
