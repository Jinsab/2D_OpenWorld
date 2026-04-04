using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.24 오후 17:40
 *  마지막 수정 일자 : 26.04.03 오후 17:23
 *  
 *  [스크립트 목적 및 내용]
 *  1. 게임 매니저
 *    1-1. 게임 전반적인 시스템 관리
 *    1-2. 플레이어 관리
 *  
 *  2. 추후 고려사항
 *    2-1. 오브젝트 풀링(프리팹 풀링) 형태로 개선해야 함
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header(" # Player Data")]
    public GameObject Player;
    public Inventory Inventory;
    public EquipmentInventory EquipmentInventory;

    private void Awake()
    {
        Instance = this;
    }
}
