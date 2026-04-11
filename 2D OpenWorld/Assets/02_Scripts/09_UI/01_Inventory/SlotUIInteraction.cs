using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:14
 *  마지막 수정 일자 : 26.04.11 오후 20:50
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 호버 UI 관리
 *    1-1. 마우스가 본인(슬롯) UI 위에 올라왔을 때 툴팁 표기
 *    1-2. 해당 칸에 하이라이트 표기
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class SlotUIInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("# Inventory Slot Data")]
    public InventorySlotUI slotUI;
    public EquipmentSlotUI equipSlotUI;

    [Header("# Interaction Image")]
    public Image interactImage;

    public void OnPointerEnter(PointerEventData eventData)
    {
        interactImage.enabled = true;
        // Log.UI("마우스가 UI 위에 들어옴");

        if (slotUI != null)
        {
            if (ItemDatabase.Instance.TryGetItem(slotUI.GetSlotItemId(), out Item item))
            {
                TooltipUI.Instance.ShowTooltip(item);
            }
        }
        else if (equipSlotUI != null)
        {
            if (ItemDatabase.Instance.TryGetItem(equipSlotUI.GetSlotItemId(), out Item item))
            {
                TooltipUI.Instance.ShowTooltip(item);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        interactImage.enabled = false;
        // Log.UI("마우스가 UI 위에서 나감");
        TooltipUI.Instance.HideTooltip();
    }

    private void OnDisable()
    {
        interactImage.enabled = false;
        TooltipUI.Instance.HideTooltip();
    }
}
