using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Режим строительства (замена AR Magic Bar из видео):
// выбираешь фигуру на нижней панели и тапаешь —
//   по полу (AR-плоскости) → фигура ставится на пол;
//   по верху другой фигуры → ставится сверху;
//   по боку другой фигуры → ставится вплотную сбоку.
// После «Старт» строительство выключается, после «Сброс» снова включается.
[RequireComponent(typeof(ARRaycastManager))]
public class BuildController : MonoBehaviour
{
    [Serializable]
    public class ShapeEntry
    {
        public string name;
        public GameObject prefab;
    }

    [SerializeField] private List<ShapeEntry> shapes = new List<ShapeEntry>();
    [SerializeField] private Button undoButton;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private float rotateStep = 45f;

    public static event Action OnObjectPlaced;
    public static event Action OnAllCleared;
    public static event Action<int> OnSelectionChanged;

    public static BuildController Instance { get; private set; }
    public IReadOnlyList<ShapeEntry> Shapes => shapes;
    public int SelectedIndex { get; private set; }

    private static readonly Color[] tints =
    {
        new Color(0.98f, 0.78f, 0.22f), new Color(0.25f, 0.62f, 0.95f),
        new Color(0.35f, 0.82f, 0.45f), new Color(0.93f, 0.45f, 0.75f),
        new Color(0.96f, 0.55f, 0.25f), new Color(0.6f, 0.45f, 0.9f),
        new Color(0.3f, 0.85f, 0.85f), new Color(0.95f, 0.95f, 0.95f),
    };

    private const float Gap = 0.002f;

    private ARRaycastManager raycastManager;
    private readonly List<GameObject> placed = new List<GameObject>();
    private GameObject ground;
    private float yawOffset;
    private bool buildEnabled = true;

    private static readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private MaterialPropertyBlock mpb;

    void Awake()
    {
        Instance = this;
        mpb = new MaterialPropertyBlock();
        raycastManager = GetComponent<ARRaycastManager>();
        UIButtonHandler.OnUIStartButtonClicked += DisableBuilding;
        UIButtonHandler.OnUIResetButtonClicked += EnableBuilding;
    }

    void Start()
    {
        if (undoButton) undoButton.onClick.AddListener(Undo);
        if (rotateButton) rotateButton.onClick.AddListener(RotateLast);
        if (clearButton) clearButton.onClick.AddListener(ClearAll);
        Select(0);
    }

    void OnDestroy()
    {
        UIButtonHandler.OnUIStartButtonClicked -= DisableBuilding;
        UIButtonHandler.OnUIResetButtonClicked -= EnableBuilding;
        if (Instance == this) Instance = null;
    }

    private void DisableBuilding() => buildEnabled = false;
    private void EnableBuilding() => buildEnabled = true;

    public void Select(int index)
    {
        if (index < 0 || index >= shapes.Count) return;
        SelectedIndex = index;
        OnSelectionChanged?.Invoke(index);
    }

    // ---------- тапы ----------

    void Update()
    {
        if (!buildEnabled || shapes.Count == 0) return;

        Pointer pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        Vector2 screenPos = pointer.position.ReadValue();
        if (IsOverUI(screenPos)) return;

        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(screenPos);
        GameObject prefab = shapes[SelectedIndex].prefab;
        BuildPiece newPiece = prefab.GetComponent<BuildPiece>();

        // 1) Сначала физика: попали в фигуру или в пол/плоскость?
        RaycastHit[] hits = Physics.RaycastAll(ray, 20f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            BuildPiece target = hit.collider.GetComponentInParent<BuildPiece>();
            if (target != null)
            {
                PlaceOnPiece(prefab, newPiece, target, hit);
                return;
            }
            if (hit.collider.gameObject == ground || hit.collider.GetComponentInParent<ARPlane>() != null)
            {
                PlaceOnFloor(prefab, hit.point, cam);
                return;
            }
            // прочее (например, летящие шары) пропускаем
        }

        // 2) Иначе — AR-луч по найденным плоскостям.
        if (raycastManager.Raycast(screenPos, arHits, TrackableType.PlaneWithinPolygon))
            PlaceOnFloor(prefab, arHits[0].pose.position, cam);
    }

    private void PlaceOnFloor(GameObject prefab, Vector3 point, Camera cam)
    {
        EnsureGround(point.y);

        Vector3 toCamera = cam.transform.position - point;
        toCamera.y = 0f;
        float yaw = toCamera.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toCamera.normalized, Vector3.up).eulerAngles.y
            : 0f;

        Spawn(prefab, point + Vector3.up * Gap, Quaternion.Euler(0f, yaw + yawOffset, 0f));
    }

    private void PlaceOnPiece(GameObject prefab, BuildPiece newPiece, BuildPiece target, RaycastHit hit)
    {
        // Выравниваем по фигуре, на которую ставим (+ свой поворот).
        float baseYaw = target.transform.eulerAngles.y;
        Quaternion rot = Quaternion.Euler(0f, baseYaw + yawOffset, 0f);

        Vector3 pos;
        if (hit.normal.y > 0.7f)
        {
            // Сверху
            pos = hit.point + Vector3.up * Gap;
        }
        else
        {
            // Сбоку: отступаем от грани на полширины новой фигуры
            Vector3 n = hit.normal;
            n.y = 0f;
            n.Normalize();
            Vector3 localN = Quaternion.Inverse(rot) * n;
            Vector3 e = newPiece != null ? newPiece.extents : new Vector3(0.05f, 0.1f, 0.05f);
            float halfAlongNormal = Mathf.Abs(localN.x) * e.x + Mathf.Abs(localN.z) * e.z;

            pos = hit.point + n * (halfAlongNormal + Gap);
            pos.y = hit.collider.bounds.min.y; // ставим на тот же уровень, что и соседа
        }

        Spawn(prefab, pos, rot);
    }

    private void Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        GameObject go = Instantiate(prefab, pos, rot);
        var piece = go.GetComponent<BuildPiece>();
        if (piece != null && piece.randomTint)
        {
            if (piece.tintPerPart)
            {
                // Шаблон: каждая деталь своим цветом
                foreach (var part in go.GetComponentsInChildren<BuildPiece>())
                    if (part != piece) Tint(part.gameObject, RandomTint());
            }
            else
            {
                Tint(go, RandomTint());
            }
        }
        placed.Add(go);
        OnObjectPlaced?.Invoke();
    }

    private static Color RandomTint() => tints[UnityEngine.Random.Range(0, tints.Length)];

    private void Tint(GameObject target, Color c)
    {
        foreach (var r in target.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }

    private void EnsureGround(float y)
    {
        if (ground != null) return;
        // Невидимый пол под постройкой, чтобы фигуры не проваливались сквозь AR-плоскость.
        ground = new GameObject("Ground");
        ground.transform.position = new Vector3(0f, y, 0f);
        var box = ground.AddComponent<BoxCollider>();
        box.size = new Vector3(40f, 0.02f, 40f);
        box.center = new Vector3(0f, -0.01f, 0f);
    }

    // ---------- кнопки ----------

    public void Undo()
    {
        if (!buildEnabled || placed.Count == 0) return;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayRemove();
        Destroy(placed[placed.Count - 1]);
        placed.RemoveAt(placed.Count - 1);
        if (placed.Count == 0) AfterCleared();
    }

    public void RotateLast()
    {
        if (!buildEnabled) return;
        yawOffset = (yawOffset + rotateStep) % 360f;
        if (placed.Count > 0)
            placed[placed.Count - 1].transform.Rotate(0f, rotateStep, 0f, Space.World);
    }

    public void ClearAll()
    {
        if (!buildEnabled) return;
        if (placed.Count > 0 && SoundManager.Instance != null) SoundManager.Instance.PlayRemove();
        foreach (var go in placed) if (go != null) Destroy(go);
        placed.Clear();
        AfterCleared();
    }

    private void AfterCleared()
    {
        if (ground != null) Destroy(ground);
        ground = null;
        OnAllCleared?.Invoke();
    }

    private bool IsOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        return uiHits.Count > 0;
    }
}
