using System;
using System.Collections.Generic;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 17:17
 *  마지막 수정 일자 : 26.02.19 오후 17:17
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 스크립트
 *    1-1. 장비/장신구 아이템 장착 시 스탯
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

[Serializable]
public class CharacterStat
{
    public float BaseValue; // 기본값 (예: 기본 공격력 10)

    private readonly List<StatModifier> statModifiers = new List<StatModifier>();
    public IReadOnlyList<StatModifier> StatModifiers => statModifiers.AsReadOnly();

    private bool isDirty = true; // 값이 변경되었는지 확인용
    private float lastValue;     // 캐싱된 최종값

    public float Value
    {
        get
        {
            if (isDirty)
            {
                lastValue = CalculateFinalValue();
                isDirty = false;
            }
            return lastValue;
        }
    }

    public void AddModifier(StatModifier mod)
    {
        isDirty = true;
        statModifiers.Add(mod);
        // 계산 우선순위대로 정렬 (Flat -> PercentAdd -> PercentMult)
        statModifiers.Sort((a, b) => a.Type.CompareTo(b.Type));
    }

    public bool RemoveModifier(StatModifier mod)
    {
        if (statModifiers.Remove(mod))
        {
            isDirty = true;
            return true;
        }
        return false;
    }

    // 아이템 해제 시 소스(Source)를 기준으로 모든 수정치 제거
    public bool RemoveAllModifiersFromSource(object source)
    {
        int removedCount = statModifiers.RemoveAll(m => m.Source == source);
        if (removedCount > 0)
        {
            isDirty = true;
            return true;
        }
        return false;
    }

    private float CalculateFinalValue()
    {
        float finalValue = BaseValue;
        float sumPercentAdd = 0;

        for (int i = 0; i < statModifiers.Count; i++)
        {
            StatModifier mod = statModifiers[i];

            if (mod.Type == StatModType.Flat)
            {
                finalValue += mod.Value;
            }
            else if (mod.Type == StatModType.PercentAdd)
            {
                sumPercentAdd += mod.Value;
                // 모든 PercentAdd를 더한 후 마지막에 기본값에 곱함
                if (i + 1 >= statModifiers.Count || statModifiers[i + 1].Type != StatModType.PercentAdd)
                {
                    finalValue *= (1 + sumPercentAdd);
                }
            }
            else if (mod.Type == StatModType.PercentMult)
            {
                finalValue *= mod.Value; // 복리 계산 (예: x1.2)
            }
        }

        return (float)Math.Round(finalValue, 4);
    }
}
