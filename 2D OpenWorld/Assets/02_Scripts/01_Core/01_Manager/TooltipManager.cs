using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.17 오후 22:22
 *  마지막 수정 일자 : 26.03.30 오후 17:24
 *  
 *  [스크립트 목적 및 내용]
 *  1. 툴팁 위치 제어
 *    1-1. 툴팁이 화면 밖으로 나가지 않게 하는 화면 내 배치(Screen Clipping) 로직
 *    1-2. 툴팁의 Pivot을 조절하여 화면 안쪽으로 꺾여 들어오게 함
 *    1-3. pivot을 (0,0)에서 (1,1)로 실시간 변경하면,
 *         툴팁이 마우스 커서를 기준으로 4개의 사분면 중
 *         가장 넓은 공간이 확보된 쪽으로 자동 배치됩니다.
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class TooltipManager : MonoBehaviour
{
    [SerializeField] private RectTransform tooltipRectTransform;
    [SerializeField] private Canvas canvas; // Canvas Scaler 영향을 계산하기 위함

    private void Update()
    {
        if (!tooltipRectTransform.gameObject.activeSelf)
            return;

        UpdateTooltipPosition();
    }

    private void UpdateTooltipPosition()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 1. 화면 해상도 대비 마우스 위치 비율 계산 (0.0 ~ 1.0)
        float pivotX = mousePos.x / Screen.width;
        float pivotY = mousePos.y / Screen.height;

        // 2. 마우스가 우측에 있으면 피벗을 1(오른쪽 끝)로, 좌측이면 0(왼쪽 끝)으로 설정
        // 상단/하단도 동일하게 적용하여 마우스 반대 방향으로 패널이 펼쳐지게 함
        float finalPivotX = pivotX > 0.5f ? 1.1f : -0.1f; // 마우스와 약간의 간격을 위해 0.1 여유
        float finalPivotY = pivotY > 0.5f ? 1.1f : -0.1f;

        tooltipRectTransform.pivot = new Vector2(pivotX > 0.5f ? 1 : 0, pivotY > 0.5f ? 1 : 0);

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);

        // 3. 위치 적용
        tooltipRectTransform.position = mousePos;
    }
}
