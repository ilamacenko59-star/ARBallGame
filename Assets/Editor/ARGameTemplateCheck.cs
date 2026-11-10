using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Проверка шаблонов: детали не должны пересекаться друг с другом,
// иначе при включении физики постройку разбросает.
public static class ARGameTemplateCheck
{
    [MenuItem("AR Game/Check Templates")]
    public static void Check()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var folders = new[] { "Assets/ARGame/Prefabs/Templates", "Assets/ARGame/Prefabs" };
        int problems = 0;
        foreach (var folder in folders)
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponent<BuildPiece>() == null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Physics.SyncTransforms();
            var cols = go.GetComponentsInChildren<Collider>();
            var bodies = new List<(Collider c, Rigidbody rb)>();
            foreach (var c in cols) bodies.Add((c, c.attachedRigidbody));

            int bad = 0;
            float worst = 0f;
            for (int i = 0; i < bodies.Count; i++)
            for (int j = i + 1; j < bodies.Count; j++)
            {
                if (bodies[i].rb == bodies[j].rb) continue; // части одной фигуры (арка)
                var a = bodies[i].c; var b = bodies[j].c;
                if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation, out _, out float dist) && dist > 0.0005f)
                {
                    bad++;
                    worst = Mathf.Max(worst, dist);
                }
            }

            var bp = go.GetComponent<BuildPiece>();
            Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Debug.Log($"[Check] {prefab.name}: деталей {go.GetComponentsInChildren<Rigidbody>().Length}, " +
                      $"пересечений {bad} (макс {worst * 1000f:0.0} мм), низ y={bounds.min.y:0.000}, " +
                      $"размер {bounds.size.x:0.00}×{bounds.size.y:0.00}×{bounds.size.z:0.00}, extents {bp.extents}");
            problems += bad;

            // Устойчивость: включаем физику на 3 секунды без выстрелов и смотрим, что сдвинулось
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(10f, 0.02f, 10f);
            ground.transform.position = new Vector3(0f, -0.01f, 0f);
            var rbs = go.GetComponentsInChildren<Rigidbody>();
            var start = new Vector3[rbs.Length];
            for (int i = 0; i < rbs.Length; i++)
            {
                start[i] = rbs[i].position;
                rbs[i].isKinematic = false;
                rbs[i].useGravity = true;
            }
            var prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            float dt = Time.fixedDeltaTime;
            int steps = Mathf.RoundToInt(3f / dt);
            for (int s = 0; s < steps; s++) Physics.Simulate(dt);
            Physics.simulationMode = prevMode;
            int moved = 0;
            float maxMove = 0f;
            for (int i = 0; i < rbs.Length; i++)
            {
                float d = Vector3.Distance(rbs[i].position, start[i]);
                maxMove = Mathf.Max(maxMove, d);
                if (d > 0.02f)
                {
                    moved++;
                    Debug.Log($"[Check]    сдвинулся {rbs[i].name} с {start[i]} на {d * 100f:0.0} см, " +
                              $"вектор {(rbs[i].position - start[i]) * 100f} см, наклон {Vector3.Angle(rbs[i].transform.up, Vector3.up):0.0}°, " +
                              $"спит={rbs[i].IsSleeping()} v={rbs[i].linearVelocity.magnitude:0.000}");
                }
            }
            Debug.Log($"[Check] {prefab.name}: за 3 с сдвинулось >2 см: {moved} из {rbs.Length} (макс {maxMove * 100f:0.0} см)");
            Object.DestroyImmediate(ground);
            Object.DestroyImmediate(go);
        }
        Debug.Log($"[Check] Итого пересечений: {problems}, шаг физики {Time.fixedDeltaTime}");
    }

    public static void CheckCLI()
    {
        try { Check(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
