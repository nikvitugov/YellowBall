using UnityEngine;

/// <summary>
/// Настройки конкретной модели героя для PlayerVisual. Вешается на объект модели.
/// </summary>
[DisallowMultipleComponent]
public class HeroModelInfo : MonoBehaviour
{
    [Tooltip("Плоская модель (надутая из картинки): лицо нарисовано только спереди, " +
             "поэтому при развороте она ещё и зеркалится. Объёмные модели просто поворачиваются")]
    public bool flat;

    [Tooltip("Множитель угла поворота к стороне бега для этой модели")]
    public float turnAngleScale = 1f;
}
