using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:14
 *  마지막 수정 일자 : 26.03.30 오후 16:38
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 호버 UI 관리
 *    1-1. 마우스가 본인(슬롯) UI 위에 올라왔을 때 툴팁 표기
 *    1-2. 해당 칸에 하이라이트 표기
 *    
 *  2. 큰 그림
 *    - Inventory System
 *      ├─ Inventory (인벤토리 데이터 로직)
 *      └─ Inventory UI (전체 UI 관리)
 *         ├─ InventorySlotUI (슬롯 단위 UI)
 *         ├─ DragController (마우스 드래그 전담)
 *         ├─ DragIconUI (아이템 드래그 시 아이콘 표시)
 *         ├─ TooltipUI (아이템 설명 표시)
 *         └─ SlotUIInteraction (마우스 호버 시 툴팁 표시 및 하이라이트 효과)
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class SlotUIInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("# Inventory Slot Data")]
    public InventorySlotUI slotUI;

    [Header("# Interaction Image")]
    public Image interactImage;

    public void OnPointerEnter(PointerEventData eventData)
    {
        interactImage.enabled = true;
        // Log.UI("마우스가 UI 위에 들어옴");

        if (ItemDatabase.Instance.TryGetItem(slotUI.GetSlotItemId(), out Item item))
        {
            TooltipUI.Instance.ShowTooltip(item);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        interactImage.enabled = false;
        // Log.UI("마우스가 UI 위에서 나감");
        TooltipUI.Instance.HideTooltip();
    }
}
