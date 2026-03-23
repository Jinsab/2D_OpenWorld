using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 21:15
 *  마지막 수정 일자 : 226.03.23 오후 18:22
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
        InitializeInventory();
    }

    private void InitializeInventory()
    {
        if (inventoryData == null)
        {
            // 연결된 인벤토리 정보가 없으므로 오류 메시지로 표기
            Log.Error("Inventory", "No InventoryData connected!");

            // 임시 데이터 생성 (디버그용)
            inventoryData = new InventoryData
            {
                maxSlots = 20,
                slots = new List<InventorySlot>(inventoryData.maxSlots)
            };

            return;
        }

        // 1. 리스트 객체 자체가 없는 경우 생성 (이미 데이터가 있다면 유지)
        if (inventoryData.slots == null)
        {
            Log.Game("InventoryData Slots Null! Initializing New Slots.");

            inventoryData.slots = new List<InventorySlot>(inventoryData.maxSlots);
        }

        // 2. 고정 크기(maxSlots)만큼 리스트 확장 및 빈 슬롯(ID 0)으로 채우기
        // 가변적 Add/Remove 대신, 항상 고정된 Count를 유지합니다.
        while (inventoryData.slots.Count < inventoryData.maxSlots)
        {
            // Log.Game("InventoryData Slots Less than MaxSlots! Adding Missing Slots.");

            // 기본 생성자는 ID 0, Amount 0
            inventoryData.slots.Add(new InventorySlot());
        }

        // 3. 초과된 슬롯이 있다면 제거 (데이터 정합성)
        if (inventoryData.slots.Count > inventoryData.maxSlots)
        {
            Log.Game("InventoryData Slots More than MaxSlots! Removing Extra Slots.");

            inventoryData.slots.RemoveRange(inventoryData.maxSlots, inventoryData.slots.Count - inventoryData.maxSlots);
        }
    }

    #region Add
    public int AddItem(Item item, int amount)
    {
        int remaining = amount;

        // 1단계: 기존 스택(동일 ID)에 먼저 채우기
        for (int i = 0; i < inventoryData.slots.Count; i++)
        {
            var slot = inventoryData.slots[i];

            if (slot.itemId == item.itemId && slot.amount < item.maxStack)
            {
                int space = item.maxStack - slot.amount;
                int add = Mathf.Min(space, remaining);

                slot.amount += add;
                remaining -= add;

                OnSlotChanged?.Invoke(i); // UI 갱신 알림

                if (remaining <= 0)
                    return amount;
            }
        }

        // 2단계: 빈 슬롯(ID 0)에 새로 추가
        for (int i = 0; i < inventoryData.slots.Count; i++)
        {
            var slot = inventoryData.slots[i];

            if (slot.IsEmpty) // 빈 슬롯 검사
            {
                int add = Mathf.Min(item.maxStack, remaining);
                slot.itemId = item.itemId;
                slot.amount = add;
                remaining -= add;

                OnSlotChanged?.Invoke(i); // UI 갱신 알림

                if (remaining <= 0)
                    return amount;
            }
        }

        return amount - remaining; // 실제로 추가된 수량 반환
    }
    #endregion

    #region Remove
    public int RemoveItem(int itemId, int amount)
    {
        int remaining = amount;

        // 앞에서부터 순회하여 아이템 제거
        for (int i = 0; i < inventoryData.slots.Count; i++)
        {
            var slot = inventoryData.slots[i];

            if (slot.itemId != itemId)
                continue;

            if (slot.amount > remaining)
            {
                slot.amount -= remaining;
                remaining = 0;
                OnSlotChanged?.Invoke(i);
                break;
            }
            else
            {
                remaining -= slot.amount;
                slot.Clear(); // ID 0, Amount 0으로 초기화 (RemoveAt 대신 사용)
                OnSlotChanged?.Invoke(i);

                if (remaining <= 0)
                    break;
            }
        }

        return amount - remaining; // 실제로 제거된 수량
    }
    #endregion

    #region Capacity & Utility
    public int GetAvailableSpace(Item item)
    {
        int space = 0;

        foreach (var slot in inventoryData.slots)
        {
            if (slot.itemId == item.itemId)
            {
                space += (item.maxStack - slot.amount);
            }
            else if (slot.itemId == 0)
            {
                // 빈 슬롯은 maxStack만큼 공간이 있음
                space += item.maxStack;
            }
        }

        return space;
    }

    public bool CanAdd(Item item, int amount)
    {
        return GetAvailableSpace(item) >= amount;
    }

    public bool IsFull()
    {
        // 빈 슬롯이 하나라도 있으면 가득 찬 것이 아님
        return !inventoryData.slots.Any(s => s.itemId == 0);
    }

    public int GetItemCount(int itemId)
    {
        return inventoryData.slots.Where(s => s.itemId == itemId).Sum(s => s.amount);
    }

    public void SortByID()
    {
        // 1. 유효한 아이템만 추출 및 ID순 정렬
        var validItems = inventoryData.slots
            .Where(s => s.itemId != 0)
            .OrderBy(s => s.itemId)
            .ToList();

        // 2. 전체 슬롯 초기화
        foreach (var slot in inventoryData.slots)
        {
            slot.Clear();
        }

        // 3. 정렬된 아이템 다시 채우기 (병합 로직은 별도 구현 권장)
        for (int i = 0; i < validItems.Count; i++)
        {
            inventoryData.slots[i].itemId = validItems[i].itemId;
            inventoryData.slots[i].amount = validItems[i].amount;
            OnSlotChanged?.Invoke(i);
        }
    }
    #endregion
}
