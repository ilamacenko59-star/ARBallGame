using UnityEngine;

// Метка «строительной» фигуры. Пивот у всех фигур — по центру нижней грани.
public class BuildPiece : MonoBehaviour
{
    [Tooltip("Половина ширины (X), высота (Y), половина глубины (Z) в метрах")]
    public Vector3 extents = new Vector3(0.05f, 0.1f, 0.05f);

    [Tooltip("Красить каждую новую фигуру в случайный цвет")]
    public bool randomTint = true;

    [Tooltip("Для готовых шаблонов: красить каждую деталь в свой цвет")]
    public bool tintPerPart = false;
}
