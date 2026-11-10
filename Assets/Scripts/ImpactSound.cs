using UnityEngine;

// Звук удара при столкновении. Вешается на фигуры, блоки башни и шар.
public class ImpactSound : MonoBehaviour
{
    [SerializeField] private bool isBall;

    public bool IsBall => isBall;

    private float lastTime = -1f;

    void OnCollisionEnter(Collision collision)
    {
        if (SoundManager.Instance == null) return;
        if (Time.time - lastTime < 0.07f) return;

        // Столкновение двух объектов со звуком — играет только один из них
        var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<ImpactSound>() : null;
        if (other != null && other.GetHashCode() < GetHashCode()) return;

        bool ball = isBall || (other != null && other.IsBall);
        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;

        SoundManager.Instance.PlayImpact(point, collision.impulse.magnitude, ball);
        lastTime = Time.time;
        if (other != null) other.lastTime = Time.time;
    }
}
