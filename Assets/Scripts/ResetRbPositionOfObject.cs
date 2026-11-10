using UnityEngine;

// По кнопке «Сброс» возвращает фигуру туда, где она стояла в момент нажатия «Старт».
public class ResetRbPositionOfObject : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;

    private Vector3 rbStartPosition;
    private Quaternion rbStartRotation;

    public Vector3 StartLocalPosition => rbStartPosition;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        Snapshot();
        UIButtonHandler.OnUIStartButtonClicked += Snapshot;
        UIButtonHandler.OnUIResetButtonClicked += ResetRbPositionOnButtonClicked;
    }

    // Запоминаем позицию: в режиме строительства фигуру могли повернуть после установки.
    private void Snapshot()
    {
        // «Старт» нажимается только в режиме строительства, когда всё стоит на месте.
        rbStartPosition = rb.transform.localPosition;
        rbStartRotation = rb.transform.localRotation;
    }

    private void ResetRbPositionOnButtonClicked()
    {
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;

        rb.transform.localPosition = rbStartPosition;
        rb.transform.localRotation = rbStartRotation;
    }

    private void OnDestroy()
    {
        UIButtonHandler.OnUIStartButtonClicked -= Snapshot;
        UIButtonHandler.OnUIResetButtonClicked -= ResetRbPositionOnButtonClicked;
    }
}
