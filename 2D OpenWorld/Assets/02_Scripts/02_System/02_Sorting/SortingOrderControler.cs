using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.13 오후 18:09
 *  마지막 수정 일자 : 26.03.24 오후 19:40
 *  
 *  [스크립트 목적 및 내용]
 *  1. Position Y 값에 따른 Sorting Order 변경 스크립트
 *    1-1. Y 값에 따라 SortingOrder 변경
 *     
 *  [스크립트 작성 도움 출처]
 *  1. https://www.youtube.com/watch?v=1CPQzoYFMog
 */

[RequireComponent(typeof(SpriteRenderer))]
public class SortingOrderControler : MonoBehaviour
{
    private SpriteRenderer sr;
    [SerializeField] private SpriteRenderer sd;
        
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        sr.sortingOrder = SortingOrderUtility.UpdateSortingY(transform);
        transform.position = new Vector3(transform.position.x, transform.position.y, SortingOrderUtility.UpdateSortingZ(transform));
    
        if (sd != null)
        {
            sd.sortingOrder = sr.sortingOrder - 1;
        }
    }
}
