using UnityEngine;
using System.Collections.Generic;



public class PlayerStats : MonoBehaviour
{
    // Dictionary를 사용하여 StatType으로 개별 Stat에 접근
    public Dictionary<StatType, CharacterStat> Stats = new Dictionary<StatType, CharacterStat>();

    private void Awake()
    {
        // 초기화 예시 (실제로는 데이터 시트나 ScriptableObject에서 불러올 수 있음)
        InitStat(StatType.MaxHealth, 100f);
        InitStat(StatType.MoveSpeed, 5f);
        InitStat(StatType.AttackDamage, 10f);
    }

    private void InitStat(StatType type, float baseValue)
    {
        Stats[type] = new CharacterStat { BaseValue = baseValue };
    }

    // 외부(장착 시스템)에서 호출할 함수
    public void EquipItemModifiers(List<StatModifierData> modifierDatas, object source)
    {
        foreach (var data in modifierDatas)
        {
            // Data(데이터 구조체)를 실제 Modifier(계산용 객체)로 변환
            StatModifier newMod = new StatModifier(data.value, data.type, source);

            if (Stats.TryGetValue(data.statType, out CharacterStat stat))
            {
                stat.AddModifier(newMod);
            }
        }
    }

    public void UnequipItemModifiers(object source)
    {
        foreach (var stat in Stats.Values)
        {
            stat.RemoveAllModifiersFromSource(source);
        }
    }
}
