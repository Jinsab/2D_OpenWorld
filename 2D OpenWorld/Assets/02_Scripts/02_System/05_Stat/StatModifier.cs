/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 16:52
 *  마지막 수정 일자 : 26.02.19 오후 17:18
 *  
 *  [스크립트 목적 및 내용]
 *  1. 스탯 - 아이템 스탯 계산
 *    1-1. 변경할 수치, 계산 방식(비율 합산, 비율 곱산), 출처의 정보를 가짐
 *    
 *  2. 큰 그림
 *    - Stat
 *      ├─ StatType         어떤 능력치들이 있는지 정의
 *      ├─ StatModifier     아이템 스탯 계산 클래스
 *      │  └─ StatModType   아이템 스탯 계산 타입
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
public enum StatModType
{
    Flat = 100,         // 더하기      우선순위 낮음
    PercentAdd = 200,   // % 더하기    중간
    PercentMult = 300   // % 곱하기    우선순위 높음
}

[System.Serializable]
public class StatModifier
{
    public float Value;         // 수치
    public StatModType Type;    // 스탯 타입
    public object Source;       // 객체 (아이템 객체, 스킬 객체)

    public StatModifier(float value, StatModType type, object source)
    {
        Value = value;
        Type = type;
        Source = source;
    }
}
