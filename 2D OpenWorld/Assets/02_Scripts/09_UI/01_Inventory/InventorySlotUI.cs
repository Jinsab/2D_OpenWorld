using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 16:05
 *  마지막 수정 일자 : 26.03.26 오후 18:17
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
public class InventorySlotUI : MonoBehaviour, IPointerDownHandler
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
    [SerializeField] private Inventory inv;
    public bool scaleFlag;

    private int slotIndex;
    
    public void Initialize(Inventory inventory, int idx)
    {
        inv = inventory;
        slotIndex = idx;

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
            ClearVisual();
        }
        else
        {
            Item item = ItemDatabase.Instance.GetItem(slot.itemId);
            icon.sprite = item.Icon;
            icon.enabled = true;
            ItemDropEffect();
            amountText.SetText(item.maxStack > 1 ? slot.amount.ToString() : "");

            Log.DB($"아이템 아이디: {slot.itemId}, 아이템 개수: {slot.amount}");
        }
    }

    public void ClearVisual()
    {
        icon.enabled = false;
        amountText.SetText("");
    }

    // 아이템 드롭 시 크기를 잠깐 키웠다 줄임
    public void ItemDropEffect()
    {
        if (scaleFlag)
            return;

        float targetScale = 1.5f; // 커질 크기
        float duration = 0.12f;    // 애니메이션 시간

        Sequence mySequence = DOTween.Sequence();

        mySequence.Append(icon.transform.DOScale(targetScale, duration)) // 1.5배로
                   .Append(icon.transform.DOScale(1f, duration));         // 1f(원래)로

        scaleFlag = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 1. 조합키 상태 확인
        bool isShift = 
            Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        bool isCtrl =
            Keyboard.current.leftCommandKey.isPressed || Keyboard.current.rightCommandKey.isPressed;

        // 2. 왼쪽 클릭 로직
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isShift)
            {
                // [기능 4] 쉬프트 + 좌클릭: 빠른 보관 (Quick Move)
                // InventoryManager.Instance.QuickMove(slotIndex);
            }
            else
            {
                // [기능 1] 좌클릭: 전체 줍기 / 슬롯 교체 (Swap)
                InventoryManager.Instance.PickUpAll(slotIndex, inv);
            }
        }
        // 3. 오른쪽 클릭 로직
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (isShift)
            {
                // [기능 5] 쉬프트 + 우클릭: 절반 줍기
                InventoryManager.Instance.PickUpHalf(slotIndex, inv);
            }
            else if (isCtrl)
            {
                // [기능 3] 컨트롤 + 우클릭: 10개 줍기
                InventoryManager.Instance.PickUpAmount(slotIndex, inv, 10);
            }
            else
            {
                // [기능 2] 우클릭: 1개 줍기
                InventoryManager.Instance.PickUpAmount(slotIndex, inv, 1);
            }
        }
    }

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