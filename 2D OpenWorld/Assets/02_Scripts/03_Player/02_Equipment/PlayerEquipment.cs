using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.04.06 오후 18:33
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
    [SerializeField] private EquipmentInventory equipmentInventory;
    [SerializeField] private PlayerStats playerStats;
    private CharacterVisuals visuals;
    private WeaponController weaponController;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        visuals = GetComponent<CharacterVisuals>();
        weaponController = GetComponent<WeaponController>();
    }

    private void OnEnable()
    {
        // 1. 장비 데이터 변경 이벤트 구독
        if (equipmentInventory != null)
        {
            equipmentInventory.OnEquipmentChanged += RefreshPlayerEquipment;
        }
    }

    private void OnDisable()
    {
        // 2. 이벤트 구독 해제 (메모리 누수 방지)
        if (equipmentInventory != null)
        {
            equipmentInventory.OnEquipmentChanged -= RefreshPlayerEquipment;
        }
    }

    /// <summary>
    /// 장비가 장착/해제될 때 호출되는 핵심 함수
    /// </summary>
    public void RefreshPlayerEquipment(EquipmentSlot slot, int index)
    {
        if (playerStats == null || equipmentInventory == null) return;

        // 3. EquipmentInventory로부터 모든 장착 아이템의 Modifiers 수집
        List<StatModifier> allModifiers = equipmentInventory.GetAllEquipmentModifiers();

        // 4. PlayerStats에 업데이트 요청 (전체 재계산 방식)
        // Source로 'this' 또는 'equipmentInventory'를 넘겨 장비 스탯임을 명시
        playerStats.UpdateModifiers(allModifiers, equipmentInventory);

        // 5. (선택 사항) 외형 및 무기 로직 업데이트
        UpdateVisuals();

        Log.Game("[PlayerEquipment] 모든 장비 스탯이 갱신되었습니다.");
    }

    private void UpdateVisuals()
    {
        // 여기서 장비 데이터에 따른 스프라이트 교체 로직 등을 수행합니다.
        // 예: playerAppearance.UpdateSprites(equipmentInventory.GetCurrentSprites());

        //// 6. 외형 업데이트
        //// 외형의 경우 모자, 상의, 하의, 무기만 변경
        //if (newItem.slotType == EquipmentSlot.Weapon ||
        //    newItem.slotType == EquipmentSlot.Head ||
        //    newItem.slotType == EquipmentSlot.Chest ||
        //    newItem.slotType == EquipmentSlot.Pants)
        //    visuals.UpdateVisual(newItem.slotType, newItem.equipmentSprite);

        //// 7. 무기일 경우 무기 컨트롤러에 알림
        //if (newItem is WeaponItem weaponItem)
        //{
        //    weaponController.EquipWeapon(weaponItem);
        //}

        foreach(StatType type in System.Enum.GetValues(typeof(StatType)))
        {
            Log.Game($"변경된 스탯: {type} - {playerStats.GetStatValue(type)}");
        }
    }
}