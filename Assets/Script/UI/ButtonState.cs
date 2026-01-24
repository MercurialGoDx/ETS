using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonState : MonoBehaviour
{
    void Update()
    {
        // Когда отпускаешь ЛКМ где угодно — сбрасываем Selected у UI
        if (Input.GetMouseButtonUp(0) && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
