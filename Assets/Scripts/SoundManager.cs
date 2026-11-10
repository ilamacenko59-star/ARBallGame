using UnityEngine;
using UnityEngine.UI;

// Все звуки игры. Интерфейсные звуки играют «в ухо» (2D),
// удары — из точки столкновения (частично 3D, чтобы было слышно, где упало).
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioClip place, remove, click, shoot, start, reset, win, ballHit;
    [SerializeField] private AudioClip[] woodHits;

    [Header("Удары")]
    [SerializeField] private float minImpulse = 0.012f;   // тише этого — не звучит
    [SerializeField] private float fullImpulse = 0.35f;   // при таком импульсе — полная громкость
    [SerializeField] private int maxImpactsPer100ms = 8;

    private AudioSource uiSource;
    private AudioSource[] pool;
    private int next;
    private float mutedUntil;
    private float windowStart;
    private int windowCount;

    void Awake()
    {
        Instance = this;

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;

        pool = new AudioSource[16];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0.5f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 0.6f;
            src.maxDistance = 15f;
            src.dopplerLevel = 0f;
            pool[i] = src;
        }
    }

    void Start()
    {
        UIButtonHandler.OnUIStartButtonClicked += OnStart;
        UIButtonHandler.OnUIResetButtonClicked += OnReset;
        UIButtonHandler.OnUIShootButtonClicked += OnShoot;
        BuildController.OnObjectPlaced += OnPlaced;

        // Клик на все кнопки (кроме «Огонь» — у него свой звук выстрела)
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (b.name == "ShootButton") continue;
            b.onClick.AddListener(() => PlayUI(click, 0.35f));
        }
    }

    void OnDestroy()
    {
        UIButtonHandler.OnUIStartButtonClicked -= OnStart;
        UIButtonHandler.OnUIResetButtonClicked -= OnReset;
        UIButtonHandler.OnUIShootButtonClicked -= OnShoot;
        BuildController.OnObjectPlaced -= OnPlaced;
        if (Instance == this) Instance = null;
    }

    // ---------- игровые события ----------

    private void OnStart()
    {
        PlayUI(start, 0.6f);
        // при включении физики фигуры чуть оседают — не «стучим» этим
        mutedUntil = Time.time + 0.6f;
    }

    private void OnReset()
    {
        PlayUI(reset, 0.5f);
        mutedUntil = Time.time + 0.3f;
    }

    private void OnShoot() => PlayUI(shoot, 0.7f, Random.Range(0.92f, 1.08f));

    private void OnPlaced() => PlayUI(place, 0.55f, Random.Range(0.95f, 1.15f));

    public void PlayRemove() => PlayUI(remove, 0.5f);

    public void PlayWin() => PlayUI(win, 0.8f);

    // ---------- удары ----------

    public void PlayImpact(Vector3 point, float impulse, bool isBall)
    {
        if (Time.time < mutedUntil || impulse < minImpulse) return;

        // ограничиваем лавину звуков, когда рушится большая постройка
        if (Time.time - windowStart > 0.1f) { windowStart = Time.time; windowCount = 0; }
        if (++windowCount > maxImpactsPer100ms) return;

        float vol = Mathf.Clamp01((impulse - minImpulse) / (fullImpulse - minImpulse));
        vol = Mathf.Lerp(0.15f, 1f, Mathf.Sqrt(vol));

        AudioClip clip = isBall ? ballHit : woodHits[Random.Range(0, woodHits.Length)];
        float pitch = isBall ? Random.Range(0.9f, 1.1f) : Random.Range(0.85f, 1.2f);

        var src = pool[next];
        next = (next + 1) % pool.Length;
        src.transform.position = point;
        src.pitch = pitch;
        src.volume = vol * (isBall ? 0.9f : 0.75f);
        src.clip = clip;
        src.Play();
    }

    private void PlayUI(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;
        // PlayOneShot не учитывает pitch отдельно, поэтому выставляем его источнику
        uiSource.pitch = pitch;
        uiSource.PlayOneShot(clip, volume);
    }
}
