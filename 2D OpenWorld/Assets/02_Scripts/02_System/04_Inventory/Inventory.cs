using System;
using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 21:15
 *  마지막 수정 일자 : 26.03.06 오후 14:40
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 슬롯 단위 인벤토리
 *    1-1. 플레이어, 창고, 화로(용광로), 그 외 작업대 등에 입혀 사용할 수 있음
 *    
 *  2. 큰 그림
 *    - Inventory System
 *      ├─ Inventory (InventoryData를 가지는 로직, 아이템 추가, 삭제 등 가공의 역할을 함)
 *      │  └─ InventoryData (인벤토리 데이터 - 아이템 ID, 수량 등 '값'만 가집니다.) (저장 대상)
 *      │
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
public class Inventory : MonoBehaviour
{
    [Header("# Inventory Connection Data")]
    public InventoryData inventoryData;
    public event Action<int> OnSlotChanged; // 데이터 변경 시 실행 이벤트

    private void Start()
    {
        // 인벤토리 데이터 초기화
        // 이 때, 저장된(이미 있는) 데이터는 유지되어야 하므로, null 체크 후 초기화

        if (inventoryData != null)
        {
            // 연결된 인벤토리 정보가 있다면,
            // 슬롯 리스트가 null인 경우에만 초기화 (이미 데이터가 있다면 유지)
            if (inventoryData.slots == null)
            {
                Log.Game("InventoryData Slots Null! Initializing New Slots.");

                inventoryData.slots = new List<InventorySlot>(inventoryData.maxSlots);
            }
            else
            {
                Log.Game("InventoryData Connected! Using Existing Data.");

                // 인벤토리 현재 슬롯이 maxSlots보다 적은 경우,
                // 부족한 슬롯을 채워줌
                if (inventoryData.slots.Count < inventoryData.maxSlots)
                {
                    Log.Game("InventoryData Slots Less than MaxSlots! Adding Missing Slots.");
                    
                    int slotsToAdd = inventoryData.maxSlots - inventoryData.slots.Count;
                    for (int i = 0; i < slotsToAdd; i++)
                    {
                        inventoryData.slots.Add(new InventorySlot());
                    }
                }

                // 반대로 인벤토리 슬롯이 maxSlots보다 많은 경우,
                // 초과된 슬롯을 제거 (데이터 정합성 유지)
                if (inventoryData.slots.Count > inventoryData.maxSlots)
                {
                    Log.Game("InventoryData Slots More than MaxSlots! Removing Extra Slots.");

                    int slotsToRemove = inventoryData.slots.Count - inventoryData.maxSlots;
                    inventoryData.slots.RemoveRange(inventoryData.maxSlots, slotsToRemove);
                }
            }
        }
        else
        {
            // 연결된 인벤토리 정보가 없으므로 오류 메시지로 표기
            Log.Error("Game", "InventoryData Not Connected! Using Temporary Data.");

            // 임시 데이터 생성 (디버그용)
            inventoryData = new InventoryData
            {
                maxSlots = 20,
                slots = new List<InventorySlot>(inventoryData.maxSlots)
            };
        }
    }

    #region Add
    public int AddItem(Item item, int amount)
    {
        int remaining = amount;
        int index = 0;

        // 1️. 기존 스택에 먼저 채우기
        foreach (var slot in inventoryData.slots)
        {
            index++;

            if (slot.itemId != item.itemId)
                continue;

            if (slot.amount >= item.maxStack)
                continue;

            int space = item.maxStack - slot.amount;
            int add = Mathf.Min(space, remaining);

            slot.amount += add;
            remaining -= add;

            OnSlotChanged?.Invoke(index);

            if (remaining <= 0)
                return amount;
        }

        // 2️. 빈 슬롯에 새로 추가
        while (remaining > 0 && inventoryData.slots.Count < inventoryData.maxSlots)
        {
            int add = Mathf.Min(item.maxStack, remaining);

            inventoryData.slots.Add(new InventorySlot(item, add));
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
        for (int i = inventoryData.slots.Count - 1; i >= 0; i--)
        {
            var slot = inventoryData.slots[i];

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
                inventoryData.slots.RemoveAt(i);

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
        foreach (var slot in inventoryData.slots)
        {
            if (slot.itemId == item.itemId)
            {
                space += (item.maxStack - slot.amount);
            }
        }

        // 빈 슬롯 공간
        Log.Game("최대 공간: " + inventoryData.maxSlots);
        Log.Game("사용 중인 공간: " + inventoryData.slots.Count);

        int emptySlots = inventoryData.maxSlots - inventoryData.slots.Count;
        space += emptySlots * item.maxStack;

        Log.Game("남은 공간: " + space);
        return space;
    }

    public bool CanAdd(Item item, int amount)
    {
        return GetAvailableSpace(item) >= amount;
    }

    public bool IsFull()
    {
        if (inventoryData.slots.Count < inventoryData.maxSlots)
            return false;

        // 모든 슬롯이 maxStack인지 확인
        foreach (var slot in inventoryData.slots)
        {
            if (slot.amount < ItemDatabase.Instance.GetItem(slot.itemId).maxStack)
                return false;
        }

        return true;
    }
    #endregion

    #region Utility
    public int GetItemCount(int itemId)
    {
        int total = 0;

        foreach (var slot in inventoryData.slots)
        {
            if (slot.itemId == itemId)
                total += slot.amount;
        }

        return total;
    }

    public void SortByID()
    {
        // ID 순 정렬
        inventoryData.slots.Sort((a, b) => a.itemId.CompareTo(b.itemId));

        // 같은 아이템 자동 병합
        Dictionary<int, int> merged = new Dictionary<int, int>();

        foreach (var slot in inventoryData.slots)
        {
            if (!merged.ContainsKey(slot.itemId))
                merged[slot.itemId] = 0;

            merged[slot.itemId] += slot.amount;
        }

        inventoryData.slots.Clear();

        foreach (var pair in merged)
        {
            Item item = ItemDatabase.Instance.GetItem(pair.Key);

            int total = pair.Value;

            while (total > 0)
            {
                int add = Mathf.Min(item.maxStack, total);
                inventoryData.slots.Add(new InventorySlot(item, add));
                total -= add;
            }
        }
    }
    #endregion
}
