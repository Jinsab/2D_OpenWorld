/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 16:52
 *  마지막 수정 일자 : 26.02.19 오후 17:20
 *  
 *  [스크립트 목적 및 내용]
 *  1. 스탯 - 능력치 정의
 *    1-1. 어떤 능력치들이 있는지의 데이터를 지님
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
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public enum StatType
{
    MaxHealth,      // 최대 체력
    MoveSpeed,      // 이동 속도
    AttackDamage,   // 공격력
    AttackSpeed,    // 공격 속도
    MiningSpeed,    // 채광 속도
    Armor,          // 방어력
    CritChance,     // 치명타 확률
    CritDamage,     // 치명타 공격력
    LightRadius     // 발광 범위
}