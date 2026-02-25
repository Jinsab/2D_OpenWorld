using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.25 오후 23:11
 *  마지막 수정 일자 : 26.02.25 오후 23:18
 *  
 *  [스크립트 목적 및 내용]
 *  1. 캐릭터 생성 시스템 - 캐릭터 데이터 관리
 *    1-1. 플레이어 최종 외형 정보 저장
 *    1-2. 이는 유지되어, 실제 인게임 씬으로 넘겨주어야 함
 *    
 *  2. 큰 그림
 *    - Character Create System (캐릭터 생성 시스템)
 *      └─ CharacterDataManager (캐릭터 데이터 매니저) 
 *         └─ CharacterCreationManager (캐릭터 생성 매니저)
 *            ├─ CharacterAppearanceData (캐릭터 외형 데이터)
 *            └─ EquipmentOption (성별 전용 아이템)
 *               
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterDataManager : MonoBehaviour
{
    public static CharacterDataManager Instance;

    // 플레이어가 최종 선택한 에셋들을 저장
    public CharacterAppearanceData playerAppearance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴 방지
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
