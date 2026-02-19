using System.Collections.Generic;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 17:48
 *  마지막 수정 일자 : 26.02.19 오후 17:48
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 스크립트
 *    1-1. 아이템 세트 효과 실 적용 및 관리 스크립트
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class SetEffectManager
{
    private PlayerStats _playerStats;
    // 현재 활성화된 세트 보너스들을 저장 (해제 시 참조용)
    private Dictionary<ItemSet, List<StatModifierData>> _activeSetBonuses = new Dictionary<ItemSet, List<StatModifierData>>();

    public SetEffectManager(PlayerStats stats) => _playerStats = stats;

    public void UpdateSetEffects(List<Item> equippedItems)
    {
        // 1. 기존의 세트 효과를 모두 제거 (초기화 후 재계산 방식이 가장 안전함)
        foreach (var set in _activeSetBonuses.Keys)
        {
            _playerStats.UnequipItemModifiers(set);
        }
        _activeSetBonuses.Clear();

        // 2. 모든 세트 데이터 베이스를 순회하며 체크 (실제로는 장착된 아이템이 속한 세트만 체크하도록 최적화 가능)
        // 여기서는 논리적 흐름을 보여줍니다.
        foreach (var set in ItemDatabase.Instance.allSets)
        {
            int count = 0;
            foreach (var required in set.requiredItems)
            {
                // 현재 장착 중인 아이템 중에 세트 아이템이 있는지 확인
                if (equippedItems.Contains(required)) count++;
            }

            if (count > 0)
            {
                ApplySetBonuses(set, count);
            }
        }
    }

    private void ApplySetBonuses(ItemSet set, int count)
    {
        foreach (var bonus in set.setBonuses)
        {
            if (count >= bonus.requiredCount)
            {
                _playerStats.EquipItemModifiers(bonus.bonuses, set);

                if (!_activeSetBonuses.ContainsKey(set))
                    _activeSetBonuses[set] = new List<StatModifierData>();

                _activeSetBonuses[set].AddRange(bonus.bonuses);
            }
        }
    }
}
