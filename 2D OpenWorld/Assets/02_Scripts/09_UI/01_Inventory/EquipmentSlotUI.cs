using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.31 오후 21:02
 *  마지막 수정 일자 : 26.04.02 오전 01:44
 *  
 *  [스크립트 목적 및 내용]
 *  1. 
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class EquipmentSlotUI : MonoBehaviour, IPointerDownHandler, IDropHandler // 드래그 앤 드롭 대응
{
    [Header("# Slot Info")]
    public Image panelImage; // 패널 이미지 (슬롯 테두리)
    public Image baseIcon;   // 빈 슬롯 아이콘 이미지 (장착된 아이템이 없을 때 활성화)
    public Image itemIcon;   // 장착 슬롯 아이콘 이미지 (장착된 아이템이 있을 때 활성화)

    [Header("# Slot Data")]
    public EquipmentSlot slotType; // 이 슬롯이 허용하는 부위 (Helmet, Weapon 등)
    public int subIndex; // EquipmentInventory에서의 인덱스
    public bool scaleFlag;
    [SerializeField] private EquipmentInventory equipmentInv;
    [SerializeField] private Inventory inv;

    private void OnEnable()
    {
        // 중요: 인벤토리의 이벤트에 내 함수(RefreshSlot)를 연결(구독)
        // AllDataRefresh();
        equipmentInv.OnEquipmentChanged += RefreshSlot;
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위해 오브젝트가 꺼질 때 구독 해제
        equipmentInv.OnEquipmentChanged -= RefreshSlot;
    }

    // 이벤트가 발생하면 실행될 함수
    private void RefreshSlot(EquipmentSlot type, int index)
    {
        // 1. 자신의 slotType과 subIndex가 일치하는지 확인
        if (type == slotType && index == subIndex)
        {
            // 2. 해당 인덱스의 데이터와 UI 연결
            if (equipmentInv.GetItemSlot(type, index, out InventorySlot slot))
            {
                scaleFlag = false;
                UpdateVisual(slot);
                AudioManager.Instance.Play(SND.UI_Item_Drop);
                Log.UI($"{index}번 UI 슬롯 갱신 완료 (ID: {slot.itemId}, Qty: {slot.amount})");
            }
            // 3. 만약 다른 타입의 아이템이거나, 장착 실패로 인해 데이터가 갱신되지 않았다면, 장착 실패 효과 실행
            else
            {
                FailVisual();
            }
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
            itemIcon.sprite = item.Icon;
            itemIcon.enabled = true;
            baseIcon.enabled = false;
            ItemDropEffect();
        }
    }

    public void ClearVisual()
    {
        itemIcon.enabled = false;
        baseIcon.enabled = true;
    }

    // 장착 실패 시 패널을 잠깐 빨갛게 깜빡이게 하는 효과
    public void FailVisual()
    {
        // 장착 실패로 인해 패널이 빨갛게 깜빡이는 효과를 주는 코드


        // 장착 실패 사운드 재생
    }

    // 아이템 드롭 시 크기를 잠깐 키웠다 줄임
    public void ItemDropEffect()
    {
        if (scaleFlag)
            return;

        float targetScale = 1.5f; // 커질 크기
        float duration = 0.12f;    // 애니메이션 시간

        Sequence mySequence = DOTween.Sequence();

        mySequence.Append(itemIcon.transform.DOScale(targetScale, duration)) // 1.5배로
                   .Append(itemIcon.transform.DOScale(1f, duration));        // 1f(원래)로

        scaleFlag = true;
    }

    public bool CheckItem()
    {
        // 체크해야 될 사항은 총 2가지
        // 1. 해당 슬롯에 장착하려는 아이템 (마우스에 올려진 아이템)
        // 2. 해당 슬롯에 장착되어 있는 아이템 (장착된 아이템)

        // 4가지 경우의 수
        // 1. 마우스에 아이템이 없고, 슬롯에도 아이템이 없는 경우 (빈 슬롯 클릭)
        // 2. 마우스에 아이템이 없고, 슬롯에는 아이템이 있는 경우 (장착된 아이템 클릭)
        // 3. 마우스에 아이템이 있고, 슬롯에는 아이템이 없는 경우 (빈 슬롯에 장착 시도)
        // 4. 마우스에 아이템이 있고, 슬롯에도 아이템이 있는 경우 (장착된 아이템과 교체 시도)

        // 1. 마우스 슬롯(드롭 중인 아이템) 및 장착 아이템 확인하기
        var held = MouseSlotUI.Instance.heldSlot;
        equipmentInv.GetItemSlot(slotType, subIndex, out InventorySlot slot);

        if (held.IsEmpty)
        {
            if (slot.IsEmpty)
                return false;
            else
                return true;
        }

        // 2-1. 검증1: 장비 아이템인가?
        if (ItemDatabase.Instance.GetItem(held.itemId) is not EquipmentItem item)
            return false;

        // 2-2. 검증2: 부위가 일치하는가?
        if (item.slotType != this.slotType)
            return false;

        return true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Middle)
            return;

        bool isShift =
            Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

        if (eventData.button == PointerEventData.InputButton.Left && isShift)
        {
            // [기능 4] 쉬프트 + 좌클릭: 빠른 보관 (Quick Move)
            equipmentInv.GetItemSlot(slotType, subIndex, out InventorySlot slot);

            if (!slot.IsEmpty)
                InventoryManager.Instance.TryUnequipToInventory(slotType, subIndex, equipmentInv, inv);
            
            return;
        }

        if (CheckItem())
        {
            InventoryManager.Instance.TryEquipFromMouse(slotType, subIndex, equipmentInv);
            AudioManager.Instance.Play(SND.UI_Item_Pickup);
        }
        else
        {
            FailVisual();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        //if (CheckItem())
        //{
        //    // 장착 로직 실행 (Swap 등)
        //    InventoryManager.Instance.TryEquipFromMouse(slotType, subIndex, equipmentInv);
        //}
    }

    // 드롭하여 장착 시도
    //public void OnDrop(PointerEventData eventData)
    //{
    //    // InventoryManager에게 "이 부위의 이 칸에 장착해줘"라고 요청
    //    InventoryManager.Instance.TryEquipFromMouse(slotType, subIndex, equipmentInv);
    //}
}
