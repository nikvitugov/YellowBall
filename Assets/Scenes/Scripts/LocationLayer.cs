using UnityEngine;

/// <summary>
/// Слой локации: Background (фон, позади всего), Middle (игровой: платформы, шипы, игрок),
/// Foreground (передний план, перед игроком). Вешается на объект или префаб — все его
/// рендереры (спрайты, модели, частицы) получают нужный Sorting Layer и порядок.
///
/// Порядок отрисовки задаётся слоем и числом Order, а не расстоянием до камеры, поэтому
/// перекрывающиеся объекты не мерцают и не «перепрыгивают» друг через друга, когда камера едет.
/// Слои создаёт меню Yellow Light / Setup Layers & Quality.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class LocationLayer : MonoBehaviour
{
    public enum Layer { Background, Middle, Foreground }

    /// <summary>Имена Sorting Layer для каждого слоя. Middle = встроенный Default.</summary>
    public static readonly string[] SortingLayerNames = { "Background", "Default", "Foreground" };

    public Layer layer = Layer.Middle;
    [Tooltip("Порядок внутри слоя: больше — рисуется поверх")]
    public int order;

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => Apply();
#endif

    /// <summary>Применить слой ко всем рендерерам объекта и его детей.</summary>
    public void Apply()
    {
        string name = SortingLayerNames[(int)layer];
        int id = SortingLayer.NameToID(name);
        if (!SortingLayer.IsValid(id)) return;     // слои ещё не созданы

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.sortingLayerID != id) r.sortingLayerID = id;
            if (r.sortingOrder != order) r.sortingOrder = order;
        }
    }
}
