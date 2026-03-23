using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.23 오후 20:41
 *  마지막 수정 일자 : 26.03.23 오후 21:33
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

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    // [기능 1] 전체 줍기 및 스왑 (Left Click)
    public void PickUpAll(int slotIndex, Inventory inv)
    {
        var slot = inv.inventoryData.slots[slotIndex];
        var held = MouseSlotUI.Instance.heldSlot;

        // 1. 커서가 비어있으면 슬롯의 모든 것을 집어듦
        if (held.itemId == 0)
        {
            held.itemId = slot.itemId;
            held.amount = slot.amount;
            slot.Clear();
        }
        // 2. 커서에 아이템이 있고, 슬롯과 같은 아이템이면 합치기
        else if (held.itemId == slot.itemId)
        {
            var item = ItemDatabase.Instance.GetItem(slot.itemId);
            int space = item.maxStack - slot.amount;
            int add = Mathf.Min(space, held.amount);

            slot.amount += add;
            held.amount -= add;

            if (held.amount <= 0) held.Clear();
        }
        // 3. 커서에 아이템이 있고, 슬롯과 다른 아이템이면 Swap
        else
        {
            int tempId = slot.itemId;
            int tempAmount = slot.amount;

            slot.itemId = held.itemId;
            slot.amount = held.amount;

            held.itemId = tempId;
            held.amount = tempAmount;
        }

        inv.NotifyChanged(slotIndex); // UI 갱신 호출
    }

    // [기능 2 & 3] 특정 수량만큼 줍기 (Right Click / Ctrl + Right Click)
    public void PickUpAmount(int slotIndex, Inventory inv, int amount)
    {
        var slot = inv.inventoryData.slots[slotIndex];
        var held = MouseSlotUI.Instance.heldSlot;

        if (slot.itemId == 0) return; // 슬롯이 비었으면 무시

        // 커서가 비었거나 같은 아이템일 때만 동작
        if (held.itemId == 0 || held.itemId == slot.itemId)
        {
            var item = ItemDatabase.Instance.GetItem(slot.itemId);
            int takeAmount = Mathf.Min(slot.amount, amount);

            // 커서 스택 제한 확인
            if (held.itemId != 0)
                takeAmount = Mathf.Min(takeAmount, item.maxStack - held.amount);

            if (takeAmount <= 0) return;

            held.itemId = slot.itemId;
            held.amount += takeAmount;
            slot.amount -= takeAmount;

            if (slot.amount <= 0) slot.Clear();
            inv.NotifyChanged(slotIndex);
        }
    }

    // [기능 5] 절반 줍기 (Shift + Right Click)
    public void PickUpHalf(int slotIndex, Inventory inv)
    {
        var slot = inv.inventoryData.slots[slotIndex];
        if (slot.itemId == 0) return;

        int half = Mathf.CeilToInt(slot.amount / 2f);
        PickUpAmount(slotIndex, inv, half);
    }
}
