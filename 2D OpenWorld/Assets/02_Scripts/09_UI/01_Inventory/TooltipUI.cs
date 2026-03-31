using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:22
 *  마지막 수정 일자 : 26.03.31 오후 17:12
 *  
 *  [스크립트 목적 및 내용]
 *  1. 툴팁 UI 관리
 *    1-1. 마우스가 본인(슬롯) UI 위에 올라왔을 때 툴팁 표기
 *    1-2. 툴팁은 글자 길이에 따라 처리되야하고, 그에 따라 패널 크기가 확장되어야 함
 *  1. TooltipUI 표기 시 아이템 별로 다르게 출력되어야 함
 *  2. 이는 아이템 종류를 따라가야 한다는 것을 의미함
 *  
 *  2. 공통 출력 내용
 *    01. 아이템 이름 (등급 별로 색상, 기본 값은 하얀색)
 *    99. 아이템 설명 (회색)
 *    
 *    Item Type의 종류 별 출력 내용
 *    1. Resource
 *      1-02. 자원 (회색)
 *    
 *    2. Material
 *      2-02. 재료 (회색)
 *    
 *    3. Equipment (무기/도구/방어구/장신구)
 *      3-02. 아이템 레벨(등급)
 *      3-03. 아이템 효과
 *        03-1. 무기: 물리/마법 피해 00~000 (근거리/원거리)
 *        03-2. 방어구: 최대체력, 방어력
 *      3-04. 부가 효과
 *        04-1. 무기: 특수 공격 (노란색)
 *      3-05. 세트 효과
 *        05-1. 방어구: 세트 효과 (활성화 노란색, 비활성화 색), 세트 아이템 목록 (장착 하얀색, 비장착 회색)
 *      3-06. 내구도 (하얀색)
 *    
 *    4. Consumable
 *      4-02. 소모품 (회색)
 *      4-03. 사용시 효과
 *    
 *    5. Placeable
 *      5-02. 건축 (회색)
 *    
 *    6. Quest
 *      6-02. 퀘스트 (회색)
 *      6-03. 퀘스트 제목
 *      6-04. 퀘스트 내용
 *    
 *    7. 아이템 레어도 별 색상
 *     - Poor        쓰레기    회색       #969696
 *     - Common      일반      하얀색     #FFFFFF
 *     - Uncommon    고급      초록색     #96FF96
 *     - Rare        희귀      파란색     #9696FF
 *     - Epic        서사      보라색     #AE4DE3
 *     - Unique      유일      주황색     #FFC896
 *     - Legendary   전설      노란색     #E5E748
 *     - Mystic      신화      붉은색     #D63E68
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    public GameObject panel;
    public TMP_Text HeaderText;     // 아이템 이름
    public TMP_Text ItemTypeText;   // 아이템 타입
    public TMP_Text ItemStatusText; // 아이템 상태 (레벨 + 효과)
    public TMP_Text SetEffectText;  // 세트 아이템 (효과 + 목록)
    public TMP_Text desciptionText; // 아이템 설명

    private void Awake()
    {
        Instance = this;
        HideTooltip();
    }

    public void DisplayItemInfo(Item item)
    {
        // 1. 아이템 이름과 타입은 모든 아이템에 공통적으로 존재하는 정보이므로, 먼저 표기합니다.
        string colorHex = GetColorByRarity(item.rarity);
        HeaderText.SetText($"<color={colorHex}>{item.itemName}</color>");
        ItemTypeText.SetText($"<color=#E6E6E6>{item.itemType}</color>");

        // 2. 아이템 타입이 장비 아이템일 경우 아이템 상태(레벨, 효과)를 표기합니다.
        StringBuilder sb = new StringBuilder();

        if (item.itemType == Item.ItemType.Equipment)
        {
            // 2-1. 아이템 레벨
            sb.AppendLine($"<color={colorHex}>아이템 레벨 {item.itemLevel}</color>");

            // 2-2. 아이템 효과
            EquipmentItem equipmentItem = item as EquipmentItem;
            
            foreach (var modifier in equipmentItem.modifiers)
            {
                // 현재 아이템 효과 색상은 회색으로 고정되어 있지만,
                // 향후 아이템 효과에 따라 색상을 다르게 할지 고민하기
                sb.AppendLine($"<color=#E6E6E6>{modifier.statType} +{modifier.value}{(modifier.type == StatModType.Flat ? "" : "%")}</color>");
            }

            ItemStatusText.SetText(sb.ToString());
            ItemStatusText.gameObject.SetActive(true);
            sb.Clear();

            // 2-3. 아이템 세트 효과
            if (ItemDatabase.Instance.TryFindSetByItem(item, out ItemSet itemSet))
            {
                // 1. PlayerEquipment - Dictionary<EquipmentSlot, EquipmentItem>
                //    위와 같이 장착 아이템 정보가 Dict로 저장되어있음

                // 2. 위의 데이터를 비교하기 위한 List로 추출해야 함
                //    - EquipmentItem이 null이 아닌 것

                // 3. 이후, List의 Intersect 메서드를 활용하여,
                // itemSet.requiredItems와 비교하여, 일치하는 아이템을 확인함

                // 4. 일치하는 아이템이 있다면, 해당 아이템의 장착을 표기

                // 5. 또한, 일치하는 아이템의 개수에 따라 세트 효과 활성화 여부를 표기

                // 6. 이후, 세트 효과의 내용을 표기

                // var duplicates = itemSet.requiredItems.Intersect().ToList();

                // HashSet<Item> equippedItems =

                SetEffectText.SetText(sb.ToString());
                SetEffectText.gameObject.SetActive(true);
                sb.Clear();
            }

            // 2-4. 아이템 내구도 표기 (0이 아닌 경우에만)
            if (equipmentItem.maxDurability != 0)
            {
                // 0인 경우 붉은색 표기, 그 외에는 하얀색 표기
                if (equipmentItem.currentDurability == 0)
                {
                    sb.AppendLine($"<color=#E73131>내구도 : {equipmentItem.currentDurability}/{equipmentItem.maxDurability}</color>");
                }
                else
                {
                    sb.AppendLine($"<color=#FFFFFF>내구도 : {equipmentItem.currentDurability}/{equipmentItem.maxDurability}</color>");
                }
            }
        }
        else
        {
            ItemStatusText.gameObject.SetActive(false);
            SetEffectText.gameObject.SetActive(false);
        }

        sb.AppendLine($"<color=#7E7E7E>{item.itemDesc}</color>");
        desciptionText.text = sb.ToString();
    }

    // 아이템 희귀도에 따른 색상 반환
    private string GetColorByRarity(Item.ItemRarity rarity)
    {
        switch (rarity)
        {
            case Item.ItemRarity.Poor:
                return "#969696"; // 회색

            case Item.ItemRarity.Common:
                return "#FFFFFF"; // 하얀색

            case Item.ItemRarity.Uncommon:
                return "#96FF96"; // 초록색

            case Item.ItemRarity.Rare:
                return "#9696FF"; // 파란색

            case Item.ItemRarity.Epic:
                return "#B428FF"; // 보라색

            case Item.ItemRarity.Unique:
                return "#FFC896"; // 주황색

            case Item.ItemRarity.Legendary:
                return "#FFFF0A"; // 노란색

            case Item.ItemRarity.Mystic:
                return "#FF2864"; // 붉은색

            // 그 외 모든 경우에 하얀색을 반환
            default:
                return "#FFFFFF"; // 하얀색
        }
    }

    public void ShowTooltip(Item item)
    {
        DisplayItemInfo(item);
        panel.SetActive(true);
    }

    public void TryShowTooltip(int itemId)
    {
        if (ItemDatabase.Instance.TryGetItem(itemId, out Item item))
        {
            DisplayItemInfo(item);
            panel.SetActive(true);
        }
        else
        {
            HideTooltip();
        }
    }

    public void HideTooltip()
    {
        panel.SetActive(false);
    }
}
