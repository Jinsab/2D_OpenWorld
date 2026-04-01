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
 *  마지막 수정 일자 : 26.04.02 오전 01:45
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
    public InventoryData equipmentData;
    public event Action<EquipmentSlot, int> OnEquipmentChanged; // 장비 변경 시 호출 (세트 효과 및 스탯 갱신용)
    // 런타임 효율 및 로직용 (Dictionary)
    [SerializeField] private SerializedDictionary<EquipmentSlot, List<InventorySlot>> equipDict = new();

    private void Awake()
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

            equipDict[slotType] = new List<InventorySlot>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                equipDict[slotType].Add(new InventorySlot());
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

    // 장비 교체 로직 (장착, 해제, 교체 모두 이 함수로 처리 가능)
    public bool EquipItem(EquipmentSlot type, int index, InventorySlot incomingSlot)
    {
        // 1. 데이터 교체 (Swap)
        var target = equipDict[type][index];
        (target.itemId, incomingSlot.itemId) = (incomingSlot.itemId, target.itemId);
        (target.amount, incomingSlot.amount) = (incomingSlot.amount, target.amount);

        // 2. 변경 알림 (세트 효과 및 스탯 갱신용)
        NotifyChange(type, index);
        return true;
    }

    public void NotifyChange(EquipmentSlot type, int idx) => OnEquipmentChanged?.Invoke(type, idx);
}
