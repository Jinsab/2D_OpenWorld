using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.04.05 오후 17:19
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 장비 시스템
 *    1-1. 슬롯 별 아이템 정보 저장
 *    1-2. 아이템 착용 및 해제
 *    1-3. 데이터 등록, 능력치 적용, 외형 업데이트
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(CharacterVisuals))]
[RequireComponent(typeof(WeaponController))]
public class PlayerEquipment : MonoBehaviour
{
    // 각 슬롯별 현재 장착된 아이템 저장
    // private Dictionary<EquipmentSlot, EquipmentItem> currentEquipments = new Dictionary<EquipmentSlot, EquipmentItem>();
    [SerializeField] private EquipmentInventory equipmentInv;
    [SerializeField] private SerializedDictionary<EquipmentSlot, List<EquipmentInventorySlot>> currentEquipments = new(); private PlayerStats stats;
    private CharacterVisuals visuals;
    private WeaponController weaponController;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        visuals = GetComponent<CharacterVisuals>();
        weaponController = GetComponent<WeaponController>();
    }

    private void OnEnable()
    {
        // 이벤트 구독
        equipmentInv.OnEquipmentChanged += UpdatePlayerState;
    }

    private void OnDisable()
    {
        // 구독 해제 (메모리 누수 방지)
        equipmentInv.OnEquipmentChanged -= UpdatePlayerState;
    }

    // EquipmentInventory에서 InitDictionary 호출 시, 해당 함수 호출
    public void CopyDictionary(SerializedDictionary<EquipmentSlot, List<EquipmentInventorySlot>> dict)
    {
        // 1. Dictionary 초기화 (부위별 슬롯 개수 설정)
        currentEquipments.Clear();
        currentEquipments = dict;
    }


    private void UpdatePlayerState(EquipmentSlot slot, int index)
    {
        // 1. 능력치 초기화 및 재계산
        Equip(slot, index);
        //UpdateStats();

        //// 2. 외형 업데이트
        //UpdateAppearance();

        //// 3. 무기 컨트롤러 업데이트
        //UpdateWeapon();

        //// 4. 세트 효과 체크 (이미 구현된 시스템 호출)
        //SetEffectManager.Instance.CheckSetEffects(equipmentInv.GetAllEquippedItems());
    }

    public void UpdateStats() // EquipmentItem newItem
    {
        //// 1. 이미 같은 슬롯에 장비가 있다면 먼저 해제
        //if (currentEquipments.ContainsKey(newItem.slotType))
        //{
        //    Unequip(newItem.slotType);
        //}

        //// 2. 데이터 등록
        //currentEquipments[newItem.slotType] = newItem;

        //// 3. 능력치 적용 (PlayerStats 이용)
        //stats.EquipItemModifiers(newItem.modifiers, newItem);

        //// 4. 외형 업데이트
        //visuals.UpdateVisual(newItem.slotType, newItem.equipmentSprite);

        //// 5. 무기일 경우 무기 컨트롤러에 알림
        //if (newItem is WeaponItem weaponItem)
        //{
        //    weaponController.EquipWeapon(weaponItem);
        //}

        //Log.Game($"{newItem.itemName} 장착 완료");
    }

    public void Equip(EquipmentSlot slot, int subIndex)
    {
        // 혹시 모를 NullReference 방지 위해 TryGetValue 사용
        if (!currentEquipments.TryGetValue(slot, out var item))
            return;

        // 1. 이미 같은 슬롯에 장비가 있다면 먼저 해제
        // currentDictionary의 경우 기본 값으로 초기화가 되어있기 때문에
        // itemId가 0이 아닌 경우 장착된 아이템이 존재한다고 판단할 수 있다.
        if (item[subIndex].itemId != 0)
            Unequip(slot, subIndex);

        // 2. 데이터 등록
        currentEquipments[slot][subIndex] = item[subIndex];
        EquipmentItem newItem = ItemDatabase.Instance.GetItem(item[subIndex].itemId) as EquipmentItem;

        // 3. 능력치 적용 (PlayerStats 이용)
        stats.EquipItemModifiers(newItem.modifiers, newItem);

        // 4. 외형 업데이트
        visuals.UpdateVisual(newItem.slotType, newItem.equipmentSprite);

        // 5. 무기일 경우 무기 컨트롤러에 알림
        if (newItem is WeaponItem weaponItem)
        {
            weaponController.EquipWeapon(weaponItem);
        }

        Log.Game($"{newItem.itemName} 장착 완료");
    }

    // 장비 해제 로직 (장착된 아이템이 있을 때만 호출)
    public void Unequip(EquipmentSlot slot, int subIndex)
    {
        // 혹시 모를 NullReference 방지 위해 TryGetValue 사용
        if (!currentEquipments.TryGetValue(slot, out var item))
            return;

        // itemId가 0이라면 해당 슬롯에 장착된 아이템이 없다는 뜻이므로 해제할 필요가 없음
        if (item[subIndex].itemId == 0)
            return;

        EquipmentItem equipmentItem = ItemDatabase.Instance.GetItem(item[subIndex].itemId) as EquipmentItem;

        // 1. 능력치 제거
        stats.UnequipItemModifiers(equipmentItem);

        // 2. 외형 제거
        visuals.ClearVisual(slot);

        // 3. 무기 전용 해제 로직
        if (slot == EquipmentSlot.Weapon)
        {
            weaponController.UnEquipWeapon();
        }

        // 4. 데이터 제거
        currentEquipments[slot][subIndex].Clear();

        Log.Game($"{equipmentItem.itemName} 해제 완료");
    }
}