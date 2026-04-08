using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.04.07 오후 18:14
 *  마지막 수정 일자 : 26.04.08 오후 17:41
 *  
 *  [스크립트 목적 및 내용]
 *  1. 플레이어의 스탯을 보여주는 스탯 UI 매니저
 *    1-1. 수동 방식이 아닌 자동 방식으로 설계하기
 *         - PlayerStats에 있는 Dictionary<StatType, BaseStat>의 값들을
 *           하나하나 수동으로 텍스트에 대입하는 방식은 스탯이 늘어날수록 관리가 어려움
 *    1-2. UI Prefab화: 각 스탯 한 줄(이름 + 수치)을 하나의 프리팹으로 만들기
 *    1-3. 자동 생성: StatType Enum을 순회하며 UI를 자동 생성하고 리스트에 담음
 *    1-4. 이벤트 기반 갱신: 스탯이 변할 때(OnStatsChanged)만 모든 UI의 텍스트를 갱신
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class StatsUIManager : MonoBehaviour
{
    [Header("# References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Transform statsContentParent;    // 스크롤 뷰의 Content
    [SerializeField] private GameObject categoryPrefab;       // 카테고리 레이아웃 관리용 프리팹
    [SerializeField] private GameObject headerPrefab;         // 헤더 레이아웃 관리용 프리팹
    [SerializeField] private GameObject categoryHeaderPrefab; // 카테고리 제목 (예: "기본 능력치")
    [SerializeField] private GameObject categoryDividePrefab; // 카테고리 구분선 프리팹
    [SerializeField] private GameObject statRowPrefab;        // 실제 스탯 줄 (이름 : 수치)

    // 생성된 UI 요소들을 관리하기 위한 딕셔너리
    private Dictionary<StatType, StatRowUI> statUIEntries = new();

    private void Start()
    {
        InitializeStatUI();

        // PlayerStats의 변경 이벤트 구독
        playerStats.OnStatsChanged += RefreshAllStats;

        // 초기 로드 시 한 번 갱신
        RefreshAllStats();
    }

    private void InitializeStatUI()
    {
        foreach (Transform child in statsContentParent) Destroy(child.gameObject);

        foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
        {
            // 1. 카테고리 시작점인지 확인
            if (type.ToString().EndsWith("_Category_Start"))
            {
                CreateCategoryHeader(type);
                continue; // 더미 값이므로 실제 스탯 줄은 생성하지 않음
            }

            // 2. 실제 스탯 줄 생성
            CreateStatRow(type);
        }
    }

    private void CreateCategoryHeader(StatType type)
    {
        GameObject headerGo = Instantiate(categoryHeaderPrefab, statsContentParent);
        TMP_Text headerText = headerGo.GetComponentInChildren<TMP_Text>();

        // Enum 이름을 예쁘게 변환 (예: "Combat_Def_Category_Start" -> "전투 능력치(방어)")
        headerText.text = GetCategoryDisplayName(type);
    }

    private void CreateStatRow(StatType type)
    {
        GameObject rowGo = Instantiate(statRowPrefab, statsContentParent);
        StatRowUI row = rowGo.GetComponent<StatRowUI>();

        row.SetLabel(GetStatDisplayName(type));
        statUIEntries.Add(type, row);
    }

    // 2. 전체 스탯 수치 갱신
    public void RefreshAllStats()
    {
        Log.Game("스탯 갱신");
        foreach (var pair in statUIEntries)
        {
            StatType type = pair.Key;
            StatRowUI ui = pair.Value;

            float value = playerStats.GetStatValue(type);

            // 타입에 따라 퍼센트(%) 기호를 붙일지 결정
            string formattedValue = FormatStatValue(type, value);
            ui.SetValue(formattedValue);

            if (ui.IsZeroValue)
            {
                ui.SetView(false); // 값이 0이면 UI 숨김
            }
            else
            {
                ui.SetStatText(); // 라벨과 값이 모두 설정된 후 텍스트 업데이트
                ui.SetView(true); // 값이 0이 아니면 UI 보임
            }
        }
    }

    private string GetStatDisplayName(StatType type)
    {
        // 여기서 Enum을 한글 이름으로 변환 (Localization)
        return type switch
        {
            StatType.MaxHealth => "최대 체력",
            StatType.MaxMana => "최대 마나",
            StatType.HealthRegen => "체력 재생",
            StatType.ManaRegen => "마나 재생",
            StatType.Armor => "방어력",
            StatType.Avoidance => "회피 확률",
            StatType.Resistance => "저항력",
            StatType.MeleeAttackDamage => "근접 공격력",
            StatType.RangedAttackDamage => "원거리 공격력",
            StatType.MagicMeleeAttackDamage => "마법 근접 공격력",
            StatType.MagicRangedAttackDamage => "마법 원거리 공격력",
            StatType.MeleeAttackSpeed => "근접 공격 속도",
            StatType.RangedAttackSpeed => "원거리 공격 속도",
            StatType.CritChance => "치명타 확률",
            StatType.CritDamage => "치명타 공격력",
            StatType.MoveSpeed => "이동속도",
            StatType.HarvestDamage => "채집 공격력",
            StatType.HarvestSpeed => "채집 속도",
            StatType.HarvestYield => "채집 수확량",
            StatType.HarvestLuck => "채집 행운",
            StatType.LightRadius => "발광 범위",
            _ => type.ToString()
        };
    }

    private string GetCategoryDisplayName(StatType type)
    {
        return type switch
        {
            StatType.Base_Category_Start => "기본 능력치",
            StatType.Combat_Def_Category_Start => "전투 능력치 (방어)",
            StatType.Combat_Atk_Category_Start => "전투 능력치 (공격)",
            StatType.Combat_Surv_Category_Start => "전투 능력치 (생존)",
            _ => "기타"
        };
    }

    private string FormatStatValue(StatType type, float value)
    {
        // 재생율, 확률 등은 %를 붙여줌
        if (type.ToString().Contains("Regen") || type.ToString().Contains("Evasion"))
            return $"{value:F1}%"; // 소수점 한자리까지

        return ((int)value).ToString(); // 일반 수치는 정수형
    }
}
