using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.03.06 오후 17:48
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어 스탯 - 플레이어의 전체 능력치를 관리하는 클래스
 *    1-1. 로드 시 직렬화된 List 데이터 정보를 기반으로 Dictionary로 초기화함
 *    1-2. 인게임에서 인게임에선 Dictionary로 빠르게 접근하여 계산
 *         - 인게임 로직은 오직 Dictionary만 바라보게 하여 성능 손실을 줄이고,
 *         - 저장 직전에만 리스트로 변환하는 것이 효율적입니다.
 *    1-3. 저장 시에는 다시 직렬화된 List로 변환하여 저장하는 방식으로 구현
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class PlayerStats : MonoBehaviour
{
    // Dictionary 대신 List<struct> 또는 List<class>를 사용하여 직렬화 가능하게 만듭니다.
    [Header("# Stat Connection Data")]
    public PlayerStatData statData;

    // Dictionary를 사용하여 StatType으로 개별 Stat에 접근
    public Dictionary<StatType, CharacterStat> Stats = new Dictionary<StatType, CharacterStat>();

    private void Awake()
    {
        OnAfterDeserialize();
    }

    // Save 시 Dictionary를 직렬화 가능한 List로 변환하여 저장
    public void OnBeforeSerialize()
    {
        // Dictionary에서 직렬화 가능한 List로 변환
        statData.Stats = Stats.Select(kv => new StatEntry(kv.Key, kv.Value)).ToList();
    }

    // Load 시 직렬화된 List에서 Dictionary로 변환하여 사용
    public void OnAfterDeserialize()
    {
        if (statData != null)
        {
            // 직렬화된 List에서 Dictionary로 변환
            Stats = statData.Stats.ToDictionary(entry => entry.statType, entry => entry.stat);

            // 초기화 예시 (실제로는 데이터 시트나 ScriptableObject에서 불러올 수 있음)
            InitStat(StatType.MaxHealth, 100f);
            InitStat(StatType.MaxMana, 100f);
            InitStat(StatType.MoveSpeed, 5f);
            InitStat(StatType.AttackDamage, 10f);
            InitStat(StatType.AttackSpeed, 1f);
            InitStat(StatType.MiningSpeed, 1f);
            InitStat(StatType.Armor, 10f);
            InitStat(StatType.CritChance, 0f);
            InitStat(StatType.CritDamage, 0f);
            InitStat(StatType.LightRadius, 0f);
        }
        else
        {
            Debug.LogWarning("PlayerStats: statData is null. Initializing with default values.");
        }
    }

    private void InitStat(StatType type, float baseValue)
    {
        // Dictionary를 사용한 방법
        // 데이터가 없다면 새로 추가, 있다면 기존 데이터 유지
        if (Stats.ContainsKey(type))
        {
            Debug.Log($"Stat already initialized: {type}");

            return;
        }
        else
        {
            Debug.Log($"Initializing stat: {type} with base value: {baseValue}");

            Stats[type] = new CharacterStat { BaseValue = baseValue };
        }

        // List를 사용한 방법
        // 해당 요소를 가지고 있는지 확인 
        //if (statData.Stats.Exists(e => e.statType == type))
        //{
        //    Debug.Log($"Stat already exists: {type}");
        //}
        //else
        //{
        //    statData.Stats.Add(new StatEntry(type, new CharacterStat { BaseValue = baseValue }));
        //}
    }

    // 외부(장착 시스템)에서 호출할 함수
    public void EquipItemModifiers(List<StatModifierData> modifierDatas, object source)
    {
        foreach (var data in modifierDatas)
        {
            // Data(데이터 구조체)를 실제 Modifier(계산용 객체)로 변환
            StatModifier newMod = new StatModifier(data.value, data.type, source);

            try
            {
                if (Stats.TryGetValue(data.statType, out CharacterStat stat))
                {
                    stat.AddModifier(newMod);
                }
            }
            catch (ArgumentNullException e)
            {
                Debug.Log($"Key is {data.statType}. {e.Message}");
            }

            // List를 사용 방법
            //try
            //{
            //    statData.Stats.FirstOrDefault(entry => entry.statType == data.statType).stat.AddModifier(newMod);
            //}
            //catch (IndexOutOfRangeException e)
            //{
            //    Debug.Log($"EquipItemModifiers Processing failed: {e.Message}");
            //}
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
