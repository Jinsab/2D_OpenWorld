/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 17:24
 *  마지막 수정 일자 : 26.02.19 오후 17:24
 *  
 *  [스크립트 목적 및 내용]
 *  1. 스탯
 *    1-1. 
 *    
 *  2. 큰 그림
 *    - Stat
 *      ├─ StatModifierData     스탯 데이터 구조체
 *      │  ├─ StatType          어떤 능력치들이 있는지 정의
 *      │  └─ StatModifier      아이템 스탯 계산 클래스
 *      │     └─ StatModType    아이템 스탯 계산 타입
 *      │  
 *      ├─ CharacterStat    개별 능력치 관리 클래스
 *      └─ PlayerStats      플레이어 전체 능력치 관리
 *      
 *  3. 추후 고려해야 할 사항
 *    3-1. 정렬 순서 (Priority)
 *         - 계산할 때 항상 Flat(합산)을 먼저 처리하고 Percent(곱하기)를 나중에 처리
 *    3-2. 세트 효과 (Set Bonuses)
 *         - 특정 아이템들을 같이 장착했을 때만 활성화되는 StatModifier 리스트를 따로 관리
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[System.Serializable]
public struct StatModifierData
{
    public StatType statType;     // 어떤 능력치를? (공격력, 이동속도 등)
    public StatModType type;      // 어떤 방식으로? (Flat, PercentAdd 등)
    public float value;           // 수치는 얼마큼?
}
