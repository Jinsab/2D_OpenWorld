using JeffGrawAssets.FlexibleUI;
using UnityEngine;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.20 오전 01:47
 *  마지막 수정 일자 : 26.03.20 오전 01:47
 *  
 *  [스크립트 목적 및 내용]
 *  1. 정보창 UI - 스탯 스크롤 표기하기
 *    1-1. 최상단에 도달한 경우
 *         - 상단 화살표 비활성화
 *         - 하단 화살표 활성화
 *    1-2. 중간에 있는 경우
 *         - 상단 화살표 활성화
 *         - 하단 화살표 비활성화
 *    1-3. 최하단에 도달한 경우
 *         - 상단 화살표 활성화
 *         - 하단 화살표 비활성화
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class ScrollChevron : MonoBehaviour
{
    public ScrollRect scrollRect;
    public FlexibleImage upIcon;
    public FlexibleImage downIcon;

    void Start()
    {
        scrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    private void OnScrollChanged(Vector2 pos)
    {
        // 0.0f는 최하단, 1.0f는 최상단
        if (pos.y <= 0.001f) // 약간의 오차 허용
        {
            // Log.UI("최하단 도달");
            upIcon.enabled = true;
            downIcon.enabled = false;
        }
        else if (pos.y >= 0.999f)
        {
            // Log.UI("최상단 도달");
            upIcon.enabled = false;
            downIcon.enabled = true;
        }
        else
        {
            // Log.UI("탐색 중");
            upIcon.enabled = true;
            downIcon.enabled = true;
        }
    }
}
