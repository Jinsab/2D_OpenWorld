using AYellowpaper.SerializedCollections;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.20 오전 01:37
 *  마지막 수정 일자 : 26.04.07 오후 21:14
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
 *  1. https://learn.microsoft.com/ko-kr/dotnet/api/system.collections.generic.dictionary-2.trygetvalue?view=net-8.0
 *  2. https://timeboxstory.tistory.com/138#:~:text=Unity%2C%20C%23%20%2D%20%EB%B0%B0%EC%97%B4%2C%EB%A6%AC%EC%8A%A4%ED%8A%B8%2C%EB%94%95%EC%85%94%EB%84%88%EB%A6%AC%EB%A5%BC%20%EB%8D%B0%EC%9D%B4%ED%84%B0%20%ED%8C%8C%EC%9D%BC%EB%A1%9C%20%EC%A0%80%EC%9E%A5%ED%95%98%EA%B8%B0,%ED%95%98%EA%B3%A0%20%EC%A0%80%EC%9E%A5%ED%95%98%EB%A9%B4%20%EC%84%A4%EC%B9%98%EB%90%9C%20%EB%82%B4%20%EA%B2%8C%EC%9E%84%EB%A7%8C%20%EC%A0%80%EC%9E%A5%EB%90%98%EB%A9%B4%20%EB%90%9C%EB%8B%A4.
 */

public class PlayerStats : MonoBehaviour
{
    // Dictionary 대신 List<struct> 또는 List<class>를 사용하여 직렬화 가능하게 만듭니다.
    [Header("# Stat Connection Data")]
    public PlayerStatData statData;

    // Dictionary를 사용하여 StatType으로 개별 Stat에 접근
    public SerializedDictionary<StatType, CharacterStat> Stats = new();
    public event Action OnStatsChanged;

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
            Stats.ToDictionary(entry => statData.Stats.ToDictionary(entry => entry.statType, entry => entry.stat).Keys);

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
            Log.Error("Game", "PlayerStats: statData is null. Initializing with default values.");
        }
    }

    private void InitStat(StatType type, float baseValue)
    {
        // Dictionary를 사용한 방법
        // 데이터가 없다면 새로 추가, 있다면 기존 데이터 유지
        if (Stats.ContainsKey(type))
        {
            Log.Game($"Stat already initialized: {type}");

            return;
        }
        else
        {
            Log.Game($"Initializing stat: {type} with base value: {baseValue}");

            Stats[type] = new CharacterStat(baseValue);
        }
    }

    // 2. 외부(장비/버프)에서 모디파이어 리스트가 들어왔을 때 처리
    public void UpdateModifiers(List<StatModifier> newModifiers, object source)
    {
        // 모든 능력치에서 해당 소스의 모디파이어를 일단 제거
        foreach (var stat in Stats.Values)
        {
            stat.RemoveAllModifiersFromSource(source);
        }

        // 전달받은 모디파이어들을 각자의 타입에 맞게 배분
        foreach (var mod in newModifiers)
        {
            if (Stats.ContainsKey(mod.StatName))
            {
                Stats[mod.StatName].AddModifier(mod);
            }
        }

        OnStatsChanged?.Invoke();
    }

    // 특정 능력치의 최종값 가져오기
    public float GetStatValue(StatType type)
    {
        return Stats.ContainsKey(type) ? Stats[type].Value : 0;
    }

    // 외부(장착 시스템)에서 호출할 함수
    public void EquipItemModifiers(List<StatModifierData> modifierDatas, object source)
    {
        foreach (var data in modifierDatas)
        {
            // Data(데이터 구조체)를 실제 Modifier(계산용 객체)로 변환
            StatModifier newMod = new StatModifier(data.statType, data.value, data.type, source);

            try
            {
                if (Stats.TryGetValue(data.statType, out CharacterStat stat))
                {
                    stat.AddModifier(newMod);
                }
            }
            catch (ArgumentNullException e)
            {
                Log.Game($"Key is {data.statType}. {e.Message}");
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
