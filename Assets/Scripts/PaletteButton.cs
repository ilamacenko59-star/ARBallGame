using UnityEngine;
using UnityEngine.UI;

// Кнопка фигуры на нижней панели. Выбранная фигура — светлый фон, жёлтая рамка и чуть крупнее.
[RequireComponent(typeof(Button))]
public class PaletteButton : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color selectedColor = new Color(0.32f, 0.34f, 0.42f, 0.95f);

    private Outline outline;

    void Awake()
    {
        outline = background.GetComponent<Outline>();
        if (outline == null) outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.8f, 0.1f, 1f);
        outline.effectDistance = new Vector2(6f, -6f);

        BuildController.OnSelectionChanged += OnSelectionChanged;
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (BuildController.Instance != null) BuildController.Instance.Select(index);
        });
    }

    void Start()
    {
        if (BuildController.Instance != null) OnSelectionChanged(BuildController.Instance.SelectedIndex);
    }

    void OnDestroy()
    {
        BuildController.OnSelectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged(int selected)
    {
        bool on = selected == index;
        if (background != null) background.color = on ? selectedColor : normalColor;
        if (outline != null) outline.enabled = on;
        transform.localScale = on ? Vector3.one * 1.06f : Vector3.one;
    }
}
