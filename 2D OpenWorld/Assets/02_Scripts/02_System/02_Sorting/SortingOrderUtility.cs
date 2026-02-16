using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오전 00:47
 *  마지막 수정 일자 : 26.02.17 오전 01:21
 *  
 *  [스크립트 목적 및 내용]
 *  1. Sorting Order 유틸리티 스크립트
 *     
 *  [스크립트 작성 도움 출처]
 *  1. https://www.youtube.com/watch?v=1CPQzoYFMog
 */

public static class SortingOrderUtility
{
    public static int precisionY = 100;
    public static float precisionZ = 0.01f;

    public static int UpdateSortingY(Transform transform)
    {
        return -(int)(transform.position.y * precisionY);
    }

    public static float UpdateSortingZ(Transform transform)
    {
        return transform.position.y * precisionZ;
    }
}