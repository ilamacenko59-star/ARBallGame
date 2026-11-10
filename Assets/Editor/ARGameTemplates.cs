using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ARGameShapes;

// Готовые шаблоны построек. Каждый шаблон — это набор обычных фигур (вложенные префабы),
// поэтому его можно достраивать тапом по любой детали, а после «Старт» он рушится по частям.
// Размеры в метрах, пивот — центр основания, «перед» шаблона смотрит на игрока (+Z).
public static class ARGameTemplates
{
    const float G = 0.002f; // зазор между деталями, чтобы при включении физики ничего не «взрывалось»

    static string root;
    static Dictionary<string, GameObject> p;

    public static List<Shape> CreateAll(string rootFolder, List<Shape> shapes)
    {
        root = rootFolder;
        ARGameSetup.EnsureFolder(root + "/Prefabs/Templates");
        p = new Dictionary<string, GameObject>();
        foreach (var s in shapes) p[s.name] = s.prefab;

        var list = new List<Shape>
        {
            Jenga(), House(), Wall(), Castle(), Gazebo(), DominoSnake(), Targets(), ArchCircle(),
        };
        return list;
    }

    // ---------- шаблоны ----------

    // Дженга: 8 слоёв по 3 бруска, слои крест-накрест
    static Shape Jenga()
    {
        var t = Begin("Jenga");
        const float h = 0.05f, w = 0.1f + G;
        for (int layer = 0; layer < 8; layer++)
        {
            float y = layer * (h + G);
            for (int i = -1; i <= 1; i++)
            {
                if (layer % 2 == 0) Put(t, "Брусок", new Vector3(0, y, i * w));
                else Put(t, "Брусок", new Vector3(i * w, y, 0), 90f);
            }
        }
        return Finish(t, "Дженга", new Color(0.96f, 0.7f, 0.4f), IconJenga, new Vector3(0.15f, 8 * (h + G), 0.15f));
    }

    // Домик: стены из кубов 4×4 в два ряда, дверь с перемычкой, крыша-плита и пирамида
    static Shape House()
    {
        var t = Begin("House");
        float s = 0.1f + G;
        float[] cells = { -1.5f * s, -0.5f * s, 0.5f * s, 1.5f * s };
        for (int layer = 0; layer < 2; layer++)
        {
            float y = layer * s;
            foreach (float a in cells)
            foreach (float b in cells)
            {
                bool edge = Mathf.Abs(a) > s || Mathf.Abs(b) > s;
                if (!edge) continue;
                bool front = b > s;
                if (front && Mathf.Abs(a) < s) continue; // проём двери: две средние клетки спереди
                if (front && layer == 1) continue;       // спереди сверху лежит перемычка

                Put(t, "Куб", new Vector3(a, y, b));
            }
        }
        // перемычка над дверью
        Put(t, "Брусок", new Vector3(0, s, 1.5f * s));
        // крыша
        Put(t, "Плита", new Vector3(0, 2 * s, 0));
        Put(t, "Пирамида", new Vector3(0, 2 * s + 0.02f + G, 0));
        return Finish(t, "Домик", new Color(0.95f, 0.5f, 0.4f), IconHouse, new Vector3(2 * s, 2 * s + 0.14f, 2 * s));
    }

    // Кирпичная стена-трапеция: ряды 7-6-5-4-3, каждый куб опирается на два нижних
    static Shape Wall()
    {
        var t = Begin("Wall");
        float s = 0.1f + G;
        for (int row = 0; row < 5; row++)
        {
            int count = 7 - row;
            for (int i = 0; i < count; i++)
                Put(t, "Куб", new Vector3((i - (count - 1) * 0.5f) * s, row * s, 0));
        }
        return Finish(t, "Стена", new Color(0.85f, 0.35f, 0.3f), IconWall, new Vector3(3.5f * s, 5 * s, 0.05f));
    }

    // Замок: 4 башни (3 куба + пирамида) и стены из брусков между ними
    static Shape Castle()
    {
        var t = Begin("Castle");
        float s = 0.1f + G;
        float c = 0.21f; // центр угловой башни (1 см зазора до стен, чтобы башни не цеплялись)
        foreach (float x in new[] { -c, c })
        foreach (float z in new[] { -c, c })
        {
            for (int k = 0; k < 3; k++) Put(t, "Куб", new Vector3(x, k * s, z));
            Put(t, "Пирамида", new Vector3(x, 3 * s, z));
        }
        float ph = 0.05f + G;
        for (int k = 0; k < 4; k++)
        {
            float y = k * ph;
            Put(t, "Брусок", new Vector3(0, y, c));
            Put(t, "Брусок", new Vector3(0, y, -c));
            Put(t, "Брусок", new Vector3(c, y, 0), 90f);
            Put(t, "Брусок", new Vector3(-c, y, 0), 90f);
        }
        return Finish(t, "Замок", new Color(0.7f, 0.72f, 0.8f), IconCastle, new Vector3(c + 0.05f, 3 * s + 0.12f, c + 0.05f));
    }

    // Беседка: 4 колонны, плита-крыша, пирамида сверху
    static Shape Gazebo()
    {
        var t = Begin("Gazebo");
        float c = 0.11f;
        foreach (float x in new[] { -c, c })
        foreach (float z in new[] { -c, c })
            Put(t, "Колонна", new Vector3(x, 0, z));
        float top = 0.3f + G;
        Put(t, "Плита", new Vector3(0, top, 0));
        Put(t, "Пирамида", new Vector3(0, top + 0.02f + G, 0));
        return Finish(t, "Беседка", new Color(0.45f, 0.8f, 0.6f), IconGazebo, new Vector3(0.15f, top + 0.14f, 0.15f));
    }

    // Домино-змейка: 14 костяшек по S-образной линии — толкни первую выстрелом
    static Shape DominoSnake()
    {
        var t = Begin("DominoSnake");
        const int n = 14;
        Vector3 Pos(int i) => new Vector3(0.12f * Mathf.Sin(i * 0.45f), 0, 0.7f - i * 0.11f);
        for (int i = 0; i < n; i++)
        {
            Vector3 a = Pos(i), b = Pos(i + 1);
            float yaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
            Put(t, "Домино", a, yaw);
        }
        return Finish(t, "Домино-змейка", new Color(0.95f, 0.95f, 0.95f), IconDominoSnake, new Vector3(0.18f, 0.2f, 0.75f));
    }

    // Мишени: 5 колонн полукругом, на каждой куб
    static Shape Targets()
    {
        var t = Begin("Targets");
        for (int i = 0; i < 5; i++)
        {
            float ang = (i - 2) * 22f * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(ang) * 0.4f, 0, -Mathf.Cos(ang) * 0.4f + 0.4f);
            float yaw = (i - 2) * 22f;
            Put(t, "Колонна", pos, yaw);
            Put(t, "Куб", pos + Vector3.up * (0.3f + G), yaw);
        }
        return Finish(t, "Мишени", new Color(0.98f, 0.35f, 0.35f), IconTargets, new Vector3(0.35f, 0.4f, 0.1f));
    }

    // Круг арок (как Стоунхендж) с пирамидой в центре
    static Shape ArchCircle()
    {
        var t = Begin("ArchCircle");
        const int n = 6;
        const float r = 0.36f;
        for (int i = 0; i < n; i++)
        {
            float a = i * 360f / n;
            float rad = a * Mathf.Deg2Rad;
            Put(t, "Арка", new Vector3(Mathf.Sin(rad) * r, 0, Mathf.Cos(rad) * r), a);
        }
        Put(t, "Пирамида", Vector3.zero);
        return Finish(t, "Круг арок", new Color(0.85f, 0.7f, 0.5f), IconArchCircle, new Vector3(r + 0.05f, 0.2f, r + 0.05f));
    }

    // ---------- утилиты ----------

    static GameObject Begin(string id) => new GameObject(id);

    static void Put(GameObject parent, string shape, Vector3 pos, float yaw = 0f)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(p[shape]);
        inst.transform.SetParent(parent.transform, false);
        inst.transform.localPosition = pos;
        inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    static Shape Finish(GameObject t, string name, Color color, Func<float, float, bool> icon, Vector3 extents)
    {
        var piece = t.AddComponent<BuildPiece>();
        piece.extents = extents;
        piece.randomTint = true;
        piece.tintPerPart = true;

        string id = t.name;
        var prefab = PrefabUtility.SaveAsPrefabAsset(t, $"{root}/Prefabs/Templates/{id}.prefab");
        UnityEngine.Object.DestroyImmediate(t);
        return new Shape
        {
            name = name,
            prefab = prefab,
            icon = SaveIcon("T_" + id, icon),
            color = color,
            isTemplate = true,
        };
    }

    // ---------- иконки шаблонов ----------

    static bool IconJenga(float u, float v)
    {
        for (int k = 0; k < 6; k++)
        {
            float y0 = 0.12f + k * 0.13f;
            if (v < y0 || v > y0 + 0.11f) continue;
            if (k % 2 == 0) return Mathf.Abs(u - 0.5f) < 0.3f;
            for (int i = -1; i <= 1; i++) if (Mathf.Abs(u - 0.5f - i * 0.2f) < 0.085f) return true;
        }
        return false;
    }

    static bool IconHouse(float u, float v)
    {
        bool body = Rect(u, v, 0.5f, 0.32f, 0.32f, 0.2f) && !Rect(u, v, 0.5f, 0.24f, 0.08f, 0.12f);
        bool roof = v > 0.52f && v < 0.88f && Mathf.Abs(u - 0.5f) < 0.4f * (0.88f - v) / 0.36f;
        return body || roof;
    }

    static bool IconWall(float u, float v)
    {
        if (!Rect(u, v, 0.5f, 0.5f, 0.42f, 0.34f)) return false;
        float row = (v - 0.16f) / 0.136f;
        int r = Mathf.FloorToInt(row);
        if (row - r < 0.12f) return false;                 // шов между рядами
        float off = (r % 2 == 0) ? 0f : 0.07f;
        float col = (u - 0.08f + off) / 0.14f;
        return col - Mathf.Floor(col) > 0.1f;              // вертикальный шов
    }

    static bool IconCastle(float u, float v)
    {
        bool towerL = Rect(u, v, 0.2f, 0.42f, 0.11f, 0.3f);
        bool towerR = Rect(u, v, 0.8f, 0.42f, 0.11f, 0.3f);
        bool wall = Rect(u, v, 0.5f, 0.3f, 0.3f, 0.18f) && !Rect(u, v, 0.5f, 0.2f, 0.07f, 0.09f);
        bool merlons = v > 0.72f && v < 0.82f && (Mathf.Abs(u - 0.2f) < 0.11f || Mathf.Abs(u - 0.8f) < 0.11f)
                       && ((int)((u + 0.02f) / 0.05f)) % 2 == 0;
        return towerL || towerR || wall || merlons;
    }

    static bool IconGazebo(float u, float v)
    {
        bool legs = Rect(u, v, 0.27f, 0.36f, 0.05f, 0.22f) || Rect(u, v, 0.73f, 0.36f, 0.05f, 0.22f);
        bool plate = Rect(u, v, 0.5f, 0.6f, 0.36f, 0.035f);
        bool roof = v > 0.63f && v < 0.88f && Mathf.Abs(u - 0.5f) < 0.2f * (0.88f - v) / 0.25f;
        return legs || plate || roof;
    }

    static bool IconDominoSnake(float u, float v)
    {
        for (int i = 0; i < 5; i++)
        {
            float cx = 0.14f + i * 0.18f;
            float cy = 0.5f + 0.12f * Mathf.Sin(i * 1.2f);
            if (Rect(u, v, cx, cy, 0.045f, 0.17f)) return true;
        }
        return false;
    }

    static bool IconTargets(float u, float v)
    {
        for (int i = 0; i < 3; i++)
        {
            float cx = 0.2f + i * 0.3f;
            if (Rect(u, v, cx, 0.32f, 0.04f, 0.2f) || Rect(u, v, cx, 0.63f, 0.09f, 0.09f)) return true;
        }
        return false;
    }

    static bool IconArchCircle(float u, float v)
    {
        for (int i = 0; i < 2; i++)
        {
            float cx = 0.28f + i * 0.44f;
            if (Rect(u, v, cx, 0.45f, 0.18f, 0.22f) && !Rect(u, v, cx, 0.36f, 0.09f, 0.14f)) return true;
        }
        return Circle(u, v, 0.5f, 0.15f, 0.06f);
    }
}
