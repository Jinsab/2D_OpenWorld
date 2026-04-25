using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 15:51
 *  마지막 수정 일자 : 26.04.24 오후 18:30
 *  
 *  [스크립트 목적 및 내용]
 *  1. 무기 아이템 스크립트
 *    1-1. 장착 아이템 스크립트를 상속 받음
 *    1-2. 무기가 가지는 공격 속성 데이터를 추가함
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[CreateAssetMenu(menuName = "Items/Weapon")]
public class WeaponItem : EquipmentItem
{
    public enum WeaponType
    {
        Tool,
        Melee,
        Ranged,
        Magic,
        Summon
    }

    [Header("# Combat Data")]
    public float attackDamage;  // 공격 데미지
    [Tooltip("초 당 공격횟수 (ex) attackDelay 2 => 1초에 2회 공격)")]
    public float attackDelay;   // 공격 간격 (쿨타임)
    public float staminaCost;   // 공격 시 소모 스테미나
    public float manaCost;      // 공격 시 소모 마나

    [Header("# Weapon Setup")]
    public GameObject weaponPrefab; // 손에 들릴 무기 실체 (Weapon 컴포넌트 포함)
    public WeaponType weaponType;   // Melee, Tool, Ranged, Magic, Summon

    public WeaponItem()
    {
        slotType = EquipmentSlot.Weapon;
    }
}
