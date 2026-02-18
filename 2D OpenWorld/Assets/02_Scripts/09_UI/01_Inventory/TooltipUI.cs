using UnityEngine;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:22
 *  마지막 수정 일자 : 26.02.18 오후 12:49
 *  
 *  [스크립트 목적 및 내용]
 *  1. 인벤토리 시스템 - 툴팁 UI 관리
 *    1-1. 마우스가 본인(슬롯) UI 위에 올라왔을 때 툴팁 표기
 *    1-2. 툴팁은 글자 길이에 따라 처리되야하고, 그에 따라 패널 크기가 확장되어야 함
 *    
 *  2. 큰 그림
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
    public Text nameText;
    public Text descText;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    /*  1. TooltipUI 표기 시 아이템 별로 다르게 출력되어야 함
     *  2. 이는 아이템 종류를 따라가야 한다는 것을 의미함
     *  
     *  공통 출력 내용
     *  01. 아이템 이름 (등급 별로 색상, 기본 값은 하얀색)
     *  99. 아이템 설명 (회색)
     *  
     *  Item Type의 종류 별 출력 내용
     *  1. Resource
     *  02. 자원 (회색)
     *  
     *  2. Material
     *  02. 재료 (회색)
     *  
     *  3. Equipment (무기/도구/방어구/장신구)
     *  02. 아이템 레벨(등급)
     *  03. 아이템 효과
     *    03-1. 무기: 물리/마법 피해 00~000 (근거리/원거리)
     *    03-2. 방어구: 최대체력, 방어력
     *  04. 부가 효과
     *    04-1. 무기: 특수 공격 (노란색)
     *  05. 세트 효과
     *    05-1. 방어구: 세트 효과 (활성화 노란색, 비활성화 색), 세트 아이템 목록 (장착 하얀색, 비장착 회색)
     *  06. 내구도 (하얀색)
     *  
     *  4. Consumable
     *  02. 소모품 (회색)
     *  03. 사용시 효과
     *  
     *  5. Placeable
     *  02. 건축 (회색)
     *  
     *  6. Quest
     *  02. 퀘스트 (회색)
     *  03. 퀘스트 제목
     *  04. 퀘스트 내용
     *  
     *  아이템 등급 별 색상
     *  0~4Lv:      Common      일반      하얀색     #FFFFFF
     *  5~8Lv:      Uncommon    고급      초록색     #96FF96
     *  9~12Lv:     Rare        희귀      파란색     #9696FF
     *  13~16Lv:    Epic        서사      보라색     #B428FF
     *  17~20Lv:    Unique      유일      주황색     #FFC896
     *  21~24Lv:    Legendary   전설      노란색     #FFFF0A
     *  25Lv~:      Mystic      신화      붉은색     #FF2864
     */

    public void SetUp(Item item)
    {

    }

    public void Show(Item item)
    {
        panel.SetActive(true);
        nameText.text = item.itemName;
        descText.text = item.itemDesc;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}
