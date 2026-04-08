/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 16:52
 *  마지막 수정 일자 : 26.04.08 오후 16:41
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
    None,           // 스탯 없음 (기본값)

    // --- 기본 능력치 ---
    Base_Category_Start,
    MaxHealth,      // 최대 체력 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    HealthRegen,    // 체력 회복 속도 (플레이어에게만 적용)
    MaxMana,        // 최대 마나 (플레이어에게만 적용)
    ManaRegen,      // 마나 회복 속도 (플레이어에게만 적용)

    // --- 전투 능력치 (방어) ---
    Combat_Def_Category_Start,
    Armor,          // 방어력 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    Avoidance,      // 회피율 (플레이어에게만 적용)
    Resistance,     // 저항력 (플레이어에게만 적용)
                    // 추가로 보호막, 상태 이상 저항, 원소 저항 등 세분화 가능

    // --- 전투 능력치 (공격) ---
    Combat_Atk_Category_Start,
    MeleeAttackDamage,          // 근접 공격력 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    RangedAttackDamage,         // 원거리 공격력 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    MagicMeleeAttackDamage,     // 마법 근접 공격력 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    MagicRangedAttackDamage,    // 마법 원거리 공격력 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    MeleeAttackSpeed,           // 근접 공격 속도 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    RangedAttackSpeed,          // 원거리 공격 속도 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    CritChance,                 // 치명타 확률 (플레이어에게만 적용)
    CritDamage,                 // 치명타 배율 (플레이어에게만 적용)
    
    // --- 전투 능력치 (생존) ---
    Combat_Surv_Category_Start,
    MoveSpeed,      // 이동 속도 (플레이어, 몬스터 등 모든 캐릭터에 적용)
    HarvestDamage,  // 채집 공격력 (플레이어에게만 적용)
    HarvestSpeed,   // 채집 속도 (플레이어에게만 적용)
    HarvestYield,   // 채집 수확량 (플레이어에게만 적용)
    HarvestLuck,    // 채집 행운 (플레이어에게만 적용)
    LightRadius     // 발광 범위 (플레이어, 몬스터, 오브젝트 등 모든 요소에 적용)
}