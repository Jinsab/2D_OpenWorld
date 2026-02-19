using System.Collections.Generic;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 15:51
 *  마지막 수정 일자 : 26.02.20 오전 02:01
 *  
 *  [스크립트 목적 및 내용]
 *  1. 장착 아이템 스크립트
 *    1-1. 모든 장착 아이템의 베이스가 되는 데이터 시트
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

    [Header("# Visuals")]
    public Sprite equipmentSprite; // 캐릭터 외형 변경용 스프라이트 (5장 압축 방식 중 대표 이미지)
                                   // 5장 시스템을 위해 아래와 같이 리스트나 구조체로 관리 가능
                                   // public List<Sprite> visualSprites;

    [Header("# Stats")]
    public List<StatModifierData> modifiers; // 장착 시 적용될 능력치 리스트
}
