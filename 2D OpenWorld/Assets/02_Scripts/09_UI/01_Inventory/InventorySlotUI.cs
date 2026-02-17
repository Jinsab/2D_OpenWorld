using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 16:05
 *  마지막 수정 일자 : 26.02.17 오후 16:05
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
 *         └─ DragController (마우스 드래그 전담)
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image icon;
    public TMP_Text amountText;

    private InventoryUI inventoryUI;
    private int index;
    private InventorySlot slotData;

    public void Initialize(InventoryUI ui, int idx)
    {
        inventoryUI = ui;
        index = idx;
    }

    public void Set(InventorySlot slot)
    {
        slotData = slot;
        icon.sprite = slot.item.Icon;
        icon.enabled = true;
        amountText.text = slot.amount > 1 ? slot.amount.ToString() : "";
    }

    public void Clear()
    {
        slotData = null;
        icon.enabled = false;
        amountText.text = "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slotData == null)
            return;

        // 왼쪽 클릭
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            HandleLeftClick();
        }
        // 오른쪽 클릭
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            HandleRightClick();
        }

        inventoryUI.Refresh();
    }

    void HandleLeftClick()
    {
        if (!DragController.Instance.IsDragging())
        {
            // 전체 들기
            DragController.Instance.StartDrag(slotData.item, slotData.amount);
            slotData.amount = 0;
        }
        else
        {
            // 전체 놓기
            inventoryUI.inventory.AddItem(
                DragController.Instance.draggedItem,
                DragController.Instance.draggedAmount
            );

            DragController.Instance.Clear();
        }
    }

    void HandleRightClick()
    {
        if (!DragController.Instance.IsDragging())
        {
            // 절반 들기
            int half = slotData.amount / 2;
            DragController.Instance.StartDrag(slotData.item, half);
            slotData.amount -= half;
        }
        else
        {
            // 1개 놓기
            inventoryUI.inventory.AddItem(
                DragController.Instance.draggedItem,
                1
            );

            DragController.Instance.draggedAmount--;

            if (DragController.Instance.draggedAmount <= 0)
                DragController.Instance.Clear();
        }
    }
}