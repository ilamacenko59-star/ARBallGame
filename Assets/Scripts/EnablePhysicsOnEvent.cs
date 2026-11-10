using UnityEngine;

// Блок стоит неподвижно, пока не нажата кнопка «Старт».
public class EnablePhysicsOnEvent : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        UIButtonHandler.OnUIStartButtonClicked += StartPhysicsOnButtonClicked;
    }

    private void StartPhysicsOnButtonClicked()
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();
    }

    private void OnDestroy()
    {
        UIButtonHandler.OnUIStartButtonClicked -= StartPhysicsOnButtonClicked;
    }
}
