using UnityEngine;

// По кнопке «Огонь!» вылетает шар из камеры телефона.
public class ShootBallLogic : MonoBehaviour
{
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private float ballSpeed = 6f;
    [SerializeField] private float ballLifetime = 5f;

    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        UIButtonHandler.OnUIShootButtonClicked += ShootBallOnButton;
    }

    private void OnDestroy()
    {
        UIButtonHandler.OnUIShootButtonClicked -= ShootBallOnButton;
    }

    private void ShootBallOnButton()
    {
        if (mainCam == null) mainCam = Camera.main;
        Transform cam = mainCam.transform;

        Vector3 spawnPosition = cam.position + cam.forward * 0.15f;
        GameObject spawnedBall = Instantiate(ballPrefab, spawnPosition, cam.rotation);

        if (spawnedBall.TryGetComponent(out Rigidbody rb))
        {
            rb.AddForce(cam.forward * ballSpeed, ForceMode.VelocityChange);
        }

        Destroy(spawnedBall, ballLifetime);
    }
}
