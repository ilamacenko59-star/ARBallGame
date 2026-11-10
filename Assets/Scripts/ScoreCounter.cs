using UnityEngine;
using UnityEngine.UI;

// Небольшое дополнение к видео: считает, сколько блоков сбито с исходного места.
public class ScoreCounter : MonoBehaviour
{
    [SerializeField] private Text scoreText;
    [SerializeField] private float knockedDistance = 0.05f;

    private bool playing;
    private bool won;

    void Start()
    {
        UIButtonHandler.OnUIStartButtonClicked += OnStart;
        UIButtonHandler.OnUIResetButtonClicked += OnReset;
        scoreText.transform.parent.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        UIButtonHandler.OnUIStartButtonClicked -= OnStart;
        UIButtonHandler.OnUIResetButtonClicked -= OnReset;
    }

    private void OnStart()
    {
        playing = true;
        won = false;
        scoreText.transform.parent.gameObject.SetActive(true);
    }

    private void OnReset()
    {
        playing = false;
        scoreText.transform.parent.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playing) return;

        var blocks = FindObjectsByType<ResetRbPositionOfObject>();
        int knocked = 0;
        foreach (var block in blocks)
        {
            Vector3 startWorld = block.transform.parent != null
                ? block.transform.parent.TransformPoint(block.StartLocalPosition)
                : block.StartLocalPosition;
            if (Vector3.Distance(block.transform.position, startWorld) > knockedDistance)
                knocked++;
        }

        bool all = knocked == blocks.Length && blocks.Length > 0;
        scoreText.text = all
            ? $"Сбито всё ({blocks.Length})! Нажми «Сброс»"
            : $"Сбито: {knocked} / {blocks.Length}";

        if (all && !won)
        {
            won = true;
            if (SoundManager.Instance != null) SoundManager.Instance.PlayWin();
        }
    }
}
