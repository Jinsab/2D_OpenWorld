using UnityEngine;
using System.Collections.Generic;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 17:41
 *  마지막 수정 일자 : 26.02.19 오후 17:41
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 스크립트
 *    1-1. 아이템 세트 효과 데이터
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[CreateAssetMenu(menuName = "Items/Item Set")]
public class ItemSet : ScriptableObject
{
    public string setName;
    public List<Item> requiredItems; // 세트에 포함된 아이템 리스트

    // 개수별 보너스 설정 (예: 2세트 효과, 4세트 효과)
    public List<SetBonus> setBonuses;

    [System.Serializable]
    public struct SetBonus
    {
        public int requiredCount; // 필요한 아이템 개수
        public List<StatModifierData> bonuses; // 제공할 능력치들
    }
}
