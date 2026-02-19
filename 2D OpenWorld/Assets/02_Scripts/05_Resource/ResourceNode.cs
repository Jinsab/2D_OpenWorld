using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.14 오후 17:54
 *  마지막 수정 일자 : 26.02.20 오전 02:17
 *  
 *  [스크립트 목적 및 내용]
 *  1. 채집 노드 (채집 가능한 오브젝트의 기본 클래스)
 *    1-1. 모든 데미지 대상의 공통 구조
 *    1-2. 나무, 돌, 광석, 몬스터 등 전부 동일한 구조로 처리하기 위함
 *     
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class ResourceNode : MonoBehaviour, IHarvestable
{
    [Header("Resource Info")]
    public int maxHP = 5;       // 최대 체력
    [SerializeField]
    private int currentHP;      // 현재 체력
    public int durability = 1;  // 채집 최소 공격력

    [Header("Drop Table")]
    public DropTable dropTable;

    void Start()
    {
        currentHP = maxHP;
    }

    public void Harvest(int power)
    {
        if (durability < power)
        {
            currentHP -= power;

            if (currentHP <= 0)
            {
                CompleteHarvest();
            }
        }
        else
        {
            Debug.Log("너무 단단합니다!");
        }
    }

    private void CompleteHarvest()
    {
        dropTable.Drop(transform.position);
        Destroy(gameObject);
    }
}
