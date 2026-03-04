using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 21:15
 *  마지막 수정 일자 : 26.03.04 오후 16:20
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 슬롯 단위 인벤토리
 *    1-1. 플레이어, 창고, 화로(용광로), 그 외 작업대 등에 입혀 사용할 수 있음
 *    
 *  2. 큰 그림
 *    - Inventory System
 *      ├─ Inventory (인벤토리 데이터 로직)
 *      └─ Inventory UI (전체 UI 관리)
 *         ├─ InventorySlotUI (슬롯 단위 UI)
 *         ├─ DragController (마우스 드래그 전담)
 *         ├─ DragIconUI (아이템 드래그 시 아이콘 표시)
 *         └─ SlotUIInteraction (마우스 호버 시 툴팁 표시 및 하이라이트 효과)
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[System.Serializable]
public class InventorySlot
{
    public int itemId;
    public int amount;

    // 런타임 전용 (저장 안 됨)
    [System.NonSerialized]
    public Item item;

    public InventorySlot(Item item, int amount)
    {
        itemId = item.itemId;
        this.item = item;
        this.amount = amount;
    }
}

[System.Serializable]
public class Inventory : MonoBehaviour
{
    public List<InventorySlot> slots;

    public int maxSlots = 20;

    private void Awake()
    {
        slots = new List<InventorySlot>(maxSlots);
    }

    #region Add
    public int AddItem(Item item, int amount)
    {
        int remaining = amount;

        // 1️. 기존 스택에 먼저 채우기
        foreach (var slot in slots)
        {
            if (slot.itemId != item.itemId)
                continue;

            if (slot.amount >= item.maxStack)
                continue;

            int space = item.maxStack - slot.amount;
            int add = Mathf.Min(space, remaining);

            slot.amount += add;
            remaining -= add;

            if (remaining <= 0)
                return amount;
        }

        // 2️. 빈 슬롯에 새로 추가
        while (remaining > 0 && slots.Count < maxSlots)
        {
            int add = Mathf.Min(item.maxStack, remaining);

            slots.Add(new InventorySlot(item, add));
            remaining -= add;
        }

        return amount - remaining; // 실제로 추가된 수량 반환
    }
    #endregion

    #region Remove
    public int RemoveItem(int itemId, int amount)
    {
        int remaining = amount;

        // 같은 아이템 슬롯을 뒤에서부터 제거 (안전)
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            var slot = slots[i];

            if (slot.itemId != itemId)
                continue;

            if (slot.amount > remaining)
            {
                slot.amount -= remaining;
                return amount;
            }
            else
            {
                remaining -= slot.amount;
                slots.RemoveAt(i);

                if (remaining <= 0)
                    return amount;
            }
        }

        return amount - remaining; // 실제로 제거된 수량
    }
    #endregion

    #region Capacity
    public int GetAvailableSpace(Item item)
    {
        int space = 0;

        // 기존 스택 공간
        foreach (var slot in slots)
        {
            if (slot.itemId == item.itemId)
            {
                space += (item.maxStack - slot.amount);
            }
        }

        // 빈 슬롯 공간
        int emptySlots = maxSlots - slots.Count;
        space += emptySlots * item.maxStack;

        return space;
    }

    public bool CanAdd(Item item, int amount)
    {
        return GetAvailableSpace(item) >= amount;
    }

    public bool IsFull()
    {
        if (slots.Count < maxSlots)
            return false;

        // 모든 슬롯이 maxStack인지 확인
        foreach (var slot in slots)
        {
            if (slot.amount < slot.item.maxStack)
                return false;
        }

        return true;
    }
    #endregion

    #region Utility
    public int GetItemCount(int itemId)
    {
        int total = 0;

        foreach (var slot in slots)
        {
            if (slot.itemId == itemId)
                total += slot.amount;
        }

        return total;
    }

    public void SortByID()
    {
        // ID 순 정렬
        slots.Sort((a, b) => a.itemId.CompareTo(b.itemId));

        // 같은 아이템 자동 병합
        Dictionary<int, int> merged = new Dictionary<int, int>();

        foreach (var slot in slots)
        {
            if (!merged.ContainsKey(slot.itemId))
                merged[slot.itemId] = 0;

            merged[slot.itemId] += slot.amount;
        }

        slots.Clear();

        foreach (var pair in merged)
        {
            Item item = ItemDatabase.Instance.GetItem(pair.Key);

            int total = pair.Value;

            while (total > 0)
            {
                int add = Mathf.Min(item.maxStack, total);
                slots.Add(new InventorySlot(item, add));
                total -= add;
            }
        }
    }
    #endregion
}
