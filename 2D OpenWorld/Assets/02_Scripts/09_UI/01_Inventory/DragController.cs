using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 16:04
 *  마지막 수정 일자 : 26.02.17 오후 22:14
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 인벤토리 UI 관리
 *    1-1. UI에서 slots 리스트를 직접 수정하면 안됨(반드시 Inventory 함수를 통해 처리)
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

public class DragController : MonoBehaviour
{
    public static DragController Instance;

    public Item draggedItem;
    public int draggedAmount;

    private void Awake()
    {
        Instance = this;
    }

    public void StartDrag(Item item, int amount)
    {
        draggedItem = item;
        draggedAmount = amount;
    }

    public void Clear()
    {
        draggedItem = null;
        draggedAmount = 0;
    }

    public bool IsDragging()
    {
        return draggedItem != null;
    }
}
