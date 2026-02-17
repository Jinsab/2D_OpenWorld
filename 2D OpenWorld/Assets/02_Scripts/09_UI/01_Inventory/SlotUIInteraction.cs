using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SlotUIInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("# Interaction Image")]
    public Image interactImage;

    public void OnPointerEnter(PointerEventData eventData)
    {
        interactImage.enabled = true;
        Debug.Log("마우스가 UI 위에 들어옴");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        interactImage.enabled = false;
        Debug.Log("마우스가 UI 위에서 나감");
    }
}
