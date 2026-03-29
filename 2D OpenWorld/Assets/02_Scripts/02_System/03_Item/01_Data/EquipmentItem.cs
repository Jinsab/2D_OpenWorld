using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 15:51
 *  마지막 수정 일자 : 26.03.30 오전 03:30
 *  
 *  [스크립트 목적 및 내용]
 *  1. 장착 아이템 스크립트
 *    1-1. 모든 장착 아이템의 베이스가 되는 데이터 시트
 *    1-2. 내구도 시스템을 위한 내구도 데이터 포함
 *         - 현재 내구도가 0이면 내구도 파괴 상태, 장착 불가
 *         - 최대 내구도가 0이면 내구도 무제한 상태 (내구도 시스템이 없는 경우)
 *    1-3. 캐릭터 외형 변경을 위한 스프라이트 데이터 포함
 *    1-4. 장착 시 적용될 능력치 리스트 포함
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public enum EquipmentSlot
{
    Weapon,     // 무기
    Head,       // 모자
    Chest,      // 상의
    Pants,      // 하의
    Ring,       // 반지
    Pendant,    // 목걸이
    Badge,      // 배지
    Bag,        // 가방
    trinkets    // 기타 장신구
}

public abstract class EquipmentItem : Item
{
    [Header("# Equipment Info")]
    public EquipmentSlot slotType;

    public int currentDurability; // 현재 내구도 (0이면 내구도 파괴 상태, 장착 불가)
    public int maxDurability;     // 최대 내구도 (내구도 시스템이 없는 경우 0으로 설정)

    [Header("# Visuals")]
    public Sprite equipmentSprite; // 캐릭터 외형 변경용 스프라이트 (5장 압축 방식 중 대표 이미지)
                                   // 5장 시스템을 위해 아래와 같이 리스트나 구조체로 관리 가능
                                   // public List<Sprite> visualSprites;

    [Header("# Stats")]
    public List<StatModifierData> modifiers; // 장착 시 적용될 능력치 리스트
}
