using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:22
 *  마지막 수정 일자 : 26.02.19 오전 03:01
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 툴팁 UI 관리
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
 *    아이템 등급 별 색상
 *     - 0~4Lv:      Common      일반      하얀색     #FFFFFF
 *     - 5~8Lv:      Uncommon    고급      초록색     #96FF96
 *     - 9~12Lv:     Rare        희귀      파란색     #9696FF
 *     - 13~16Lv:    Epic        서사      보라색     #B428FF
 *     - 17~20Lv:    Unique      유일      주황색     #FFC896
 *     - 21~24Lv:    Legendary   전설      노란색     #FFFF0A
 *     - 25Lv~:      Mystic      신화      붉은색     #FF2864
 *
 * 3. 큰 그림
 *    - Inventory System
 *      ├─ Inventory (인벤토리 데이터 로직)
 *      └─ Inventory UI (전체 UI 관리)
 *         ├─ InventorySlotUI (슬롯 단위 UI)
 *         ├─ DragController (마우스 드래그 전담)
 *         ├─ DragIconUI (아이템 드래그 시 아이콘 표시)
 *         ├─ TooltipUI (아이템 설명 표시)
 *         └─ SlotUIInteraction (마우스 호버 시 툴팁 표시 및 하이라이트 효과)
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    public GameObject panel;
    public RectTransform tooltipRectTransform;
    public TMP_Text HeaderText;     // 아이템 이름
    public TMP_Text ItemTypeText;   // 아이템 타입
    public TMP_Text ItemLevelText;  // 아이템 레벨
    public TMP_Text ItemStatusText; // 아이템 효과
    public TMP_Text SetEffectText;  // 세트 효과
    public TMP_Text SetItemText;    // 세트 아이템 목록
    public TMP_Text durabilityText; // 내구도
    public TMP_Text desciptionText; // 아이템 설명

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    public void SetUp(Item item)
    {


        // 데이터가 있을 때에만 오브젝트를 활성화함
        // 나머지는 Layout Group으로 알아서 가려주기 때문임
        // HeaderText.text = item.itemName;
        // ItemTypeText.text = item.itemType.ToString();
        // ...
        // desciptionText.text = item.itemDesc;
        // panel.SetActive(true);
    }

    public void Show(Item item)
    {
        SetUp(item);
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
