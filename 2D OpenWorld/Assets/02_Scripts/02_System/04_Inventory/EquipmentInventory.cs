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
 *  마지막 수정 일자 : 26.03.31 오후 18:14
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 장착된 장비 전문 관리 인벤토리
 *    1-1. 오직 플레이어의 장비만을 관리
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class EquipmentInventory : MonoBehaviour
{
    [Header("# Equipment Connection Data")]
    public InventoryData equipmentData;
    public event Action OnEquipmentChanged; // 장비 변경 시 호출 (세트 효과 및 스탯 갱신용)
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

    public InventorySlot GetItemSlot(EquipmentSlot type, int index)
    {
        return equipDict[type][index];
    }

    // 장착 로직
    public bool EquipItem(EquipmentSlot type, int index, InventorySlot incomingSlot)
    {
        EquipmentItem item = ItemDatabase.Instance.GetItem(incomingSlot.itemId) as EquipmentItem;

        // 검증: 부위가 일치하는가?
        if (item.slotType != type) return false;

        // 데이터 교체 (Swap)
        var target = equipDict[type][index];
        (target.itemId, incomingSlot.itemId) = (incomingSlot.itemId, target.itemId);
        (target.amount, incomingSlot.amount) = (incomingSlot.amount, target.amount);

        NotifyChange();
        return true;
    }

    // 장착 시도 (부위와 리스트 인덱스 전달)
    public bool Equip(EquipmentSlot type, int subIndex, InventorySlot mouseSlot)
    {
        if (!equipDict.ContainsKey(type) || subIndex >= equipDict[type].Count)
            return false;

        EquipmentItem item = ItemDatabase.Instance.GetItem(mouseSlot.itemId) as EquipmentItem;

        if (item.slotType != type)
            return false;

        // 데이터 스왑 (Swap)
        var targetSlot = equipDict[type][subIndex];

        int tempId = targetSlot.itemId;
        int tempAmt = targetSlot.amount;

        targetSlot.itemId = mouseSlot.itemId;
        targetSlot.amount = mouseSlot.amount;

        mouseSlot.itemId = tempId;
        mouseSlot.amount = tempAmt;

        // 세트 효과 및 스탯 갱신 알림
        // UpdateEquipmentStats();
        return true;
    }

    public void NotifyChange() => OnEquipmentChanged?.Invoke();
}
