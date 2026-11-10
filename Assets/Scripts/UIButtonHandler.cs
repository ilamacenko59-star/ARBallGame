using System;
using UnityEngine;
using UnityEngine.UI;

// Кнопки Старт / Огонь / Сброс + переключение между режимом строительства и игрой.
// Остальные скрипты подписываются на статические события, как в оригинальном туториале.
public class UIButtonHandler : MonoBehaviour
{
    [SerializeField] private Button UIStartButton;
    [SerializeField] private Button UIShootButton;
    [SerializeField] private Button UIResetButton;
    [SerializeField] private GameObject buildPanel;
    [SerializeField] private Text hintText;

    public static event Action OnUIStartButtonClicked;
    public static event Action OnUIShootButtonClicked;
    public static event Action OnUIResetButtonClicked;

    const string BuildHint =
        "Выбери фигуру внизу и тапни по полу.\nТап по фигуре — поставишь сверху или сбоку";

    void Start()
    {
        UIStartButton.onClick.AddListener(OnStartButtonClicked);
        UIShootButton.onClick.AddListener(OnShootButtonClicked);
        UIResetButton.onClick.AddListener(OnResetButtonClicked);

        BuildController.OnObjectPlaced += OnObjectPlaced;
        BuildController.OnAllCleared += OnAllCleared;

        buildPanel.SetActive(true);
        UIStartButton.interactable = false;
        UIShootButton.gameObject.SetActive(false);
        UIResetButton.gameObject.SetActive(false);
        SetHint("Поводи телефоном над полом, пока не появится сетка.\nПотом выбери фигуру внизу и тапни по полу");
    }

    private void OnDestroy()
    {
        BuildController.OnObjectPlaced -= OnObjectPlaced;
        BuildController.OnAllCleared -= OnAllCleared;
    }

    private void OnObjectPlaced()
    {
        UIStartButton.interactable = true;
        SetHint("Строй дальше или жми «СТАРТ», чтобы стрелять");
    }

    private void OnAllCleared()
    {
        UIStartButton.interactable = false;
        SetHint(BuildHint);
    }

    void OnStartButtonClicked()
    {
        OnUIStartButtonClicked?.Invoke();
        buildPanel.SetActive(false);
        UIShootButton.gameObject.SetActive(true);
        UIResetButton.gameObject.SetActive(true);
        SetHint("");
    }

    void OnShootButtonClicked()
    {
        OnUIShootButtonClicked?.Invoke();
    }

    void OnResetButtonClicked()
    {
        OnUIResetButtonClicked?.Invoke();
        buildPanel.SetActive(true);
        UIShootButton.gameObject.SetActive(false);
        UIResetButton.gameObject.SetActive(false);
        SetHint("Всё вернулось на место. Можно достроить или снова нажать «СТАРТ»");
    }

    private void SetHint(string text)
    {
        if (hintText == null) return;
        hintText.text = text;
        hintText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }
}
