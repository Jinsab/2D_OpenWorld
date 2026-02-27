using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.27 오후 18:53
 *  마지막 수정 일자 : 26.02.27 오후 18:53
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 슬롯 시스템 - 캐릭터 선택 및 생성 칸 이동
 *    1-1. 슬롯에 따라 캐릭터 선택 또는 생성 화면으로 이동
 *    
 *  2. 큰 그림
 *    - Character Create System (캐릭터 생성 시스템)
 *      ├─ CharacterDataManager (캐릭터 데이터 매니저) 
 *      │  └─ CharacterCreationManager (캐릭터 생성 매니저)
 *      │     └─ CharacterData (캐릭터 전체 데이터)
 *      │        ├─ CharacterAppearanceData(캐릭터 외형 데이터)
 *      │        └─ EquipmentOption (성별 전용 아이템)
 *      │
 *      └─ CharacterSelectManager (캐릭터 선택 매니저 - 전체 슬롯 관리)
 *         └─ CharacterSlot (캐릭터 슬롯 데이터 - 슬롯 업데이트 스크립트)
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterSlot : MonoBehaviour
{
    public GameObject infoGroup;
    public GameObject emptyGroup;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
