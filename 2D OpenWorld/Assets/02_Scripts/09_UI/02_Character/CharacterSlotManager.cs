using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.03 오후 15:05
 *  마지막 수정 일자 : 26.03.03 오후 16:00
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 캐릭터 슬롯 매니저
 *    1-1. 캐릭터 데이터 매니저에서 최대 슬롯 정보를 받아옴
 *    1-2. 받아온 슬롯 정보에 따라 캐릭터 슬롯을 생성 및 업데이트
 *    1-3. 슬롯이 선택되면 해당 슬롯의 캐릭터 데이터로 캐릭터 생성 화면으로 이동
 *    1-4. 슬롯이 빈 슬롯이면 클릭하여 캐릭터 데이터 생성 창으로 이동되어야 함.
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterSlotManager : MonoBehaviour
{
    [Header("# Character Group")]
    public GameObject selectGroup;
    public GameObject createGroup;
    public CharacterSlot[] characterSlots;

    [Header("# Character Slot Prefab")]
    public GameObject characterSlotPrefab;

    private void Start()
    {
        CreateCharacterSlots();
    }

    // 캐릭터 목록 창이 활성화될 때마다
    // 슬롯 목록도 캐릭터 데이터에 따라 UI 업데이트
    private void OnEnable()
    {
        UpdateCharacterSlots();
    }

    private void CreateCharacterSlots()
    {
        characterSlots = new CharacterSlot[CharacterDataManager.Instance.characterDataList.Length];

        for (int i = 0; i < characterSlots.Length; i++)
        {
            GameObject slot = Instantiate(characterSlotPrefab, this.transform);
            characterSlots[i] = slot.GetComponent<CharacterSlot>();

            slot.SetActive(false); // 초기에는 슬롯 비활성화
        }
    }

    public void UpdateCharacterSlots()
    {
        //// 기존 슬롯 제거
        //foreach (Transform child in transform)
        //{
        //    Destroy(child.gameObject);
        //}
        //// 캐릭터 데이터 리스트를 받아와서 슬롯 생성 및 업데이트
        //Debug.Log(CharacterDataManager.Instance.characterDataList.Length);
        //foreach (CharacterData characterData in CharacterDataManager.Instance.characterDataList)
        //{
        //    Debug.Log("캐릭터 데이터 리스트 받기");
        //    GameObject slot = Instantiate(characterSlotPrefab, this.transform);
        //    CharacterSlot characterSlot = slot.GetComponent<CharacterSlot>();
        //    characterSlot.characterData = characterData;
        //    slot.SetActive(true);
        //}

        // 슬롯 제거 없이 캐릭터 데이터 리스트를 받아와서 슬롯 업데이트
        for (int i = 0; i < characterSlots.Length; i++)
        {
            Debug.Log($"캐릭터 데이터 리스트 받기 - 슬롯 {i}");
            characterSlots[i].characterData = CharacterDataManager.Instance.characterDataList[i];
            characterSlots[i].gameObject.SetActive(true);
        }
    }

    public void ShowSelectGroup()
    {
        selectGroup.SetActive(true);
        createGroup.SetActive(false);
    }

    public void ShowCreateGroup()
    {
        selectGroup.SetActive(false);
        createGroup.SetActive(true);
    }
}
