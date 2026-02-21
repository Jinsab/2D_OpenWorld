using UnityEditor.Experimental.GraphView;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.13 오후 14:37
 *  마지막 수정 일자 : 26.02.19 오후 22:08
 *  
 *  [스크립트 목적 및 내용]
 *  1. 태양 시스템 - 그림자 효과
 *    1-1. 부모의 스프라이트를 복제하고, 검은색 지정 및 알파 값을 낮춰 그림자를 생성함
 *    1-2. 태양의 위치에 따라 그림자의 크기 및 회전이 결정됨
 *    1-3. 스프라이트가 변경 시 그림자의 스프라이트도 동일하게 변경됨 
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
    public bool autoSet = true;
    [SerializeField] private SpriteRenderer shadowRenderer;
    public SpriteRenderer parentRenderer;
    public Sprite lastSprite;
    
    void Awake()
    {
        if (autoSet)
        {
            shadowRenderer = GetComponent<SpriteRenderer>();
            parentRenderer = transform.parent.GetComponent<SpriteRenderer>();
        }
        else
        {
            shadowRenderer = GetComponent<SpriteRenderer>();
        }

        InitializeShadow();
    }

    void LateUpdate()
    {
        UpdateSprite();
        UpdateShadow();
    }

    void InitializeShadow()
    {
        // 부모 스프라이트 복사
        shadowRenderer.sprite = parentRenderer.sprite;
        lastSprite = parentRenderer.sprite;
        shadowRenderer.color = new Color(0, 0, 0, 0.5f);

        transform.localPosition = new Vector3(0, 1 * shadowOffsetY, 0.0001f);
        transform.localScale = Vector3.one;

        if (shadowEnd != null)
        {
            shadowEnd.transform.localPosition = new Vector3(0, shadowRenderer.bounds.size.y * maxLength, 0);
        }

        // 같은 SortingLayer 사용 권장
        // 또한, sortingOrder와 함께 Z축을 사용해야 함
        shadowRenderer.sortingLayerID = parentRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = Mathf.RoundToInt(shadowRenderer.bounds.size.y * maxLength * precision) - 1;
    }

    private void UpdateSprite()
    {
        if (parentRenderer == null || shadowRenderer == null)
            return;

        if (parentRenderer.sprite != lastSprite)
        {
            lastSprite = parentRenderer.sprite; // 갱신
            shadowRenderer.sprite = lastSprite;

            // Flip 상태도 변경 시점에만 체크 (필요시)
            shadowRenderer.flipX = parentRenderer.flipX;
            shadowRenderer.flipY = parentRenderer.flipY;
        }
    }

    private void UpdateShadow()
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
        float alpha = Mathf.Lerp(0.6f, 0.3f, sunHeight);
        shadowRenderer.color = new Color(0, 0, 0, alpha);
    }
}
