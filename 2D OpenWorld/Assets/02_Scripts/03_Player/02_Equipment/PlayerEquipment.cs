using UnityEngine;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.03.30 오전 04:53
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
    private SerializedDictionary<EquipmentSlot, EquipmentItem> currentEquipments = new();
    private PlayerStats stats;
    private CharacterVisuals visuals;
    private WeaponController weaponController;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        visuals = GetComponent<CharacterVisuals>();
        weaponController = GetComponent<WeaponController>();
    }

    public void Equip(EquipmentItem newItem)
    {
        // 1. 이미 같은 슬롯에 장비가 있다면 먼저 해제
        if (currentEquipments.ContainsKey(newItem.slotType))
        {
            Unequip(newItem.slotType);
        }

        // 2. 데이터 등록
        currentEquipments[newItem.slotType] = newItem;

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

    public void Unequip(EquipmentSlot slot)
    {
        if (!currentEquipments.TryGetValue(slot, out EquipmentItem item))
            return;

        // 1. 능력치 제거
        stats.UnequipItemModifiers(item);

        // 2. 외형 제거
        visuals.ClearVisual(slot);

        // 3. 무기 전용 해제 로직
        if (slot == EquipmentSlot.Weapon)
        {
            weaponController.UnEquipWeapon();
        }

        // 4. 데이터 제거
        currentEquipments.Remove(slot);

        Log.Game($"{item.itemName} 해제 완료");
    }
}