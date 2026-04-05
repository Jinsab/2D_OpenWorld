using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.31 오후 18:14
 *  마지막 수정 일자 : 26.04.05 오후 19:04
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 장착된 장비 전문 관리 인벤토리
 *    1-1. 오직 플레이어의 장비만을 관리
 *    1-2. 장비 데이터 관리 (Dictionary <-> List 변환 로직)
 *         - 게임 실행 시: 저장된 InventoryData (List)를 읽어 Dictionary로 변환(Load).
 *         - 플레이 중: 모든 장착/해제는 Dictionary에서만 수행 (매우 빠름).
 *         - 게임 저장 시: Dictionary의 내용을 InventoryData (List)로 덮어쓰기(Save).
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class EquipmentInventory : MonoBehaviour
{
    [Header("# Equipment Connection Data")]
    public EquipmentInventoryData equipmentData;
    public event Action<EquipmentSlot, int> OnEquipmentChanged; // 장비 변경 시 호출 (세트 효과 및 스탯 갱신용)
    // 런타임 효율 및 로직용 (Dictionary)
    [SerializeField] private SerializedDictionary<EquipmentSlot, List<EquipmentInventorySlot>> equipDict = new();

    private void Start()
    {
        InitDictionary();
    }

    private void InitDictionary()
    {
        // 1. Dictionary 초기화 (부위별 슬롯 개수 설정)
        foreach (EquipmentSlot slotType in Enum.GetValues(typeof(EquipmentSlot)))
        {
            // 무기는 제외 (퀵슬롯 관리용)
            if (slotType == EquipmentSlot.Weapon)
                continue;

            // 기본적으로 1개, 반지는 2개, 장신구는 N개 등 설정 가능
            int capacity = (slotType == EquipmentSlot.Ring) ? 2 : 1;

            equipDict[slotType] = new List<EquipmentInventorySlot>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                equipDict[slotType].Add(new EquipmentInventorySlot(slotType, i));
            }
        }

        
    }

    public bool GetItemSlot(EquipmentSlot type, int index, out InventorySlot slot)
    {
        if (equipDict.TryGetValue(type, out var slots))
        {
            if (index >= 0 && index < slots.Count)
            {
                slot = slots[index];
                return true;
            }
        }

        slot = new InventorySlot();
        return false;
    }

    public EquipmentInventorySlot GetEquipmentItem(EquipmentSlot type, int index)
    {
        return equipDict.TryGetValue(type, out var slots) && index >= 0 && index < slots.Count
            ? slots[index]
            : new EquipmentInventorySlot(type, index); // 유효하지 않은 요청에 대해 빈 슬롯 반환d
    }

    // 장비 교체 로직 (장착, 해제, 교체 모두 이 함수로 처리 가능)
    public void EquipItem(EquipmentSlot type, int index, InventorySlot incomingSlot)
    {
        // 1. 데이터 교체 (Swap)
        var target = equipDict[type][index];
        (target.itemId, incomingSlot.itemId) = (incomingSlot.itemId, target.itemId);
        (target.amount, incomingSlot.amount) = (incomingSlot.amount, target.amount);

        // 2. 변경 알림 (세트 효과 및 스탯 갱신용)
        NotifyChange(type, index);
    }

    // 특정 부위 변경 알림 (세트 효과 및 스탯 갱신용)
    public void NotifyChange(EquipmentSlot type, int idx) => OnEquipmentChanged?.Invoke(type, idx);

    public EquipmentInventoryData SaveToData()
    {
        var data = new EquipmentInventoryData();

        // 1. Dictionary의 내용을 List로 변환하여 저장
        foreach (var kvp in equipDict)
        {
            foreach (var slot in kvp.Value)
            {
                // 빈 슬롯도 위치 정보를 위해 저장
                data.slots.Add(slot);
            }
        }

        return data;
    }

    public void LoadFromData(EquipmentInventoryData equipmentData)
    {
        // 1. 먼저 Dictionary 구조를 초기 상태(빈 슬롯)로 세팅
        InitDictionary();

        // 2. 저장된 데이터를 순회하며 해당 위치에 삽입
        foreach (var entry in equipmentData.slots)
        {
            // 2-1. 데이터 유효성 검사 (slotType, subIndex 범위 체크)
            if (equipDict.TryGetValue(entry.slotType, out var slots))
            {
                // 2-2. 안전하게 subIndex 범위 체크 후 데이터 삽입
                if (entry.subIndex >= 0 && entry.subIndex < slots.Count)
                {
                    slots[entry.subIndex] =
                        new EquipmentInventorySlot(entry.slotType, entry.subIndex)
                        { itemId = entry.itemId, amount = entry.amount };
                }

                // 3. UI 및 스탯 갱신 알림
                NotifyChange(entry.slotType, entry.subIndex);
            }
        }
    }

}
