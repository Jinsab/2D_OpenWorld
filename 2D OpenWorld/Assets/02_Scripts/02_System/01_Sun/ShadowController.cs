using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.13 오후 14:37
 *  마지막 수정 일자 : 26.02.13 오후 14:09
 *  
 *  [스크립트 목적 및 내용]
 *  1. 태양 시스템 - 낮과 밤
 *    1-1. 전체 밝기 제어는 Global Light 2D로 진행됨
 *    1-2. 따라서, 시간 흐름에 따라 밝기를 제어하여 낮과 밤을 구분
 *    
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

[RequireComponent(typeof(SpriteRenderer))]
public class ShadowController : MonoBehaviour
{
    [Header("# Length Settings")]
    public float minLength = 0.4f;   // 정오
    public float maxLength = 1.8f;   // 해뜰 때/질 때
    public float shadowOffsetY;
    public int precision = 100;

    [Header("# Shadow")]
    public Transform shadowEnd;

    private SpriteRenderer shadowRenderer;
    private SpriteRenderer parentRenderer;
    
    void Awake()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        parentRenderer = transform.parent.GetComponent<SpriteRenderer>();

        InitializeShadow();
    }

    void LateUpdate()
    {
        UpdateShadow();
        UpdateSorting();
    }

    void InitializeShadow()
    {
        // 부모 스프라이트 복사
        shadowRenderer.sprite = parentRenderer.sprite;
        shadowRenderer.color = new Color(0, 0, 0, 0.5f);

        transform.localPosition = new Vector3(0, 1 * shadowOffsetY, 0.1f);
        transform.localScale = Vector3.one;

        if (shadowEnd != null)
        {
            shadowEnd.transform.localPosition = new Vector3(0, shadowRenderer.bounds.size.y * maxLength, 0);
        }

        // 같은 SortingLayer 사용 권장
        shadowRenderer.sortingLayerID = parentRenderer.sortingLayerID;
    }

    void UpdateShadow()
    {
        Vector2 sunDir = SunSystem.Instance.sunDirection;
        float sunHeight = SunSystem.Instance.GetSunHeight();

        Vector2 shadowDir = -sunDir;

        // 회전
        float angle = Mathf.Atan2(
            shadowDir.y,
            shadowDir.x
        ) * Mathf.Rad2Deg;

        transform.localRotation = Quaternion.Euler(0, 0, angle);

        // 길이 계산
        float lengthFactor = Mathf.Lerp(maxLength, minLength, sunHeight);

        // Pivot이 Bottom이므로 Y만 늘리면 위쪽으로 늘어남
        transform.localScale = new Vector3(1f, lengthFactor, 1f);

        // 밤에는 그림자 약하게
        float alpha = Mathf.Lerp(0.3f, 0.1f, sunHeight);
        shadowRenderer.color = new Color(0, 0, 0, alpha);
    }

    void UpdateSorting()
    {
        // 그림자 스프라이트 그려지는 우선순위

        // 
        if (shadowEnd.position.y > 0)
        {
            shadowRenderer.sortingOrder = parentRenderer.sortingOrder;
        }
        else
        {
            shadowRenderer.sortingOrder = Mathf.RoundToInt(-shadowEnd.position.y * precision) - 1;
        }
    }
}
