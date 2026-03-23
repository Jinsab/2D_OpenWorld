using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 16:05
 *  마지막 수정 일자 : 26.03.23 오후 18:22
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

[Serializable]
public class InventorySlotUI : MonoBehaviour//, IPointerClickHandler
{
    [Header("# Slot Info")]
    public Image panelImage;
    public Image icon;
    public TMP_Text amountText;
    public TMP_Text toggleText;

    [Header("# Slot Data")]
    public bool isToggle = false;
    public Sprite togglePanel;
    public Sprite slotPanel;
    // [SerializeField] private InventorySlot slotData;

    private InventoryUI inventoryUI;
    private int index;

    public void Initialize(InventoryUI ui, int idx)
    {
        inventoryUI = ui;
        index = idx;

        if (isToggle)
        {
            panelImage.sprite = togglePanel;
            toggleText.gameObject.SetActive(true);
        }
        else
        {
            panelImage.sprite = slotPanel;
        }
    }

    public void UpdateVisual(InventorySlot slot)
    {
        if (slot == null)
            return;

        // itemId가 0이라는 것은 해당 슬롯에 아이템이 없다는 것과 동일함
        if (slot.itemId == 0)
        {
            icon.enabled = false;
            amountText.SetText("");
        }
        else
        {
            Item item = ItemDatabase.Instance.GetItem(slot.itemId);
            icon.sprite = item.Icon;
            icon.enabled = true;
            amountText.SetText(item.maxStack != 1 ? slot.amount.ToString() : "");

            Log.DB($"아이템 아이디: {slot.itemId}, 아이템 개수: {slot.amount}");
        }
    }

    //public void OnPointerClick(PointerEventData eventData)
    //{
    //    if (slotData == null)
    //        return;

    //    if (IsShift())
    //    {
    //        QuickMove();
    //        return;
    //    }

    //    // 왼쪽 클릭
    //    if (eventData.button == PointerEventData.InputButton.Left)
    //    {
    //        HandleLeftClick();
    //    }
    //    // 오른쪽 클릭
    //    else if (eventData.button == PointerEventData.InputButton.Right)
    //    {
    //        HandleRightClick();
    //    }

    //    inventoryUI.Refresh();
    //}

    //void HandleLeftClick()
    //{
    //    if (!DragController.Instance.IsDragging())
    //    {
    //        // 전체 들기
    //        DragController.Instance.StartDrag(ItemDatabase.Instance.GetItem(slotData.itemId), slotData.amount);
    //        slotData.amount = 0;
    //    }
    //    else
    //    {
    //        // 전체 놓기
    //        inventoryUI.inventory.AddItem(
    //            DragController.Instance.draggedItem,
    //            DragController.Instance.draggedAmount
    //        );

    //        DragController.Instance.Clear();
    //    }
    //}

    //void HandleRightClick()
    //{
    //    if (!DragController.Instance.IsDragging())
    //    {
    //        // 절반 들기
    //        int half = slotData.amount / 2;
    //        DragController.Instance.StartDrag(ItemDatabase.Instance.GetItem(slotData.itemId), half);
    //        slotData.amount -= half;
    //    }
    //    else
    //    {
    //        // 1개 놓기
    //        inventoryUI.inventory.AddItem(
    //            DragController.Instance.draggedItem,
    //            1
    //        );

    //        DragController.Instance.draggedAmount--;

    //        if (DragController.Instance.draggedAmount <= 0)
    //            DragController.Instance.Clear();
    //    }
    //}

    //bool IsShift()
    //{
    //    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    //}

    private void QuickMove()
    {
        // 추후 구현할 내용

        // Inventory target = InventoryManager.Instance.GetOtherInventory(inventoryUI.inventory);
        //Inventory target = new Inventory();

        //if (target == null)
        //    return;

        //int moved = target.AddItem(slotData.item, slotData.amount);
        //inventoryUI.inventory.RemoveItem(slotData.itemId, moved);
    }
}