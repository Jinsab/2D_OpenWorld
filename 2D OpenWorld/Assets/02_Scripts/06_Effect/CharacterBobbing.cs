using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.15 오후 15:51
 *  마지막 수정 일자 : 26.02.15 오후 15:51
 *  
 *  [스크립트 목적 및 내용]
 *  1. 이펙트 스크립트
 *    1-1. 아이템에 대한 기본 정보
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterBobbing : MonoBehaviour
{
    [Header("Idle Bobbing")]
    public float idleSpeed = 2f;
    public float idleAmount = 0.05f;

    [Header("Move Bobbing")]
    public float walkSpeed = 10f;
    public float walkAmount = 0.15f;
    public float runSpeed = 15f;
    public float runAmount = 0.2f;

    private Vector3 startPos;
    private float timer;

    // 현재 플레이어의 상태를 받아오기 위한 참조 (필요에 따라 수정)
    // 예: PlayerController에서 이동 속도나 상태를 가져옴
    private Rigidbody2D rb;
    private bool isMoving => rb != null && rb.linearVelocity.magnitude > 0.1f;
    private bool isRunning => rb != null && rb.linearVelocity.magnitude > 6f; // 달리기 기준 속도

    void Start()
    {
        startPos = transform.localPosition;
        rb = GetComponentInParent<Rigidbody2D>();
    }

    void Update()
    {
        ApplyBobbing();
    }

    private void ApplyBobbing()
    {
        float currentSpeed = idleSpeed;
        float currentAmount = idleAmount;

        if (isMoving)
        {
            if (isRunning)
            {
                currentSpeed = runSpeed;
                currentAmount = runAmount;
            }
            else
            {
                currentSpeed = walkSpeed;
                currentAmount = walkAmount;
            }
        }

        // 시간의 흐름에 따라 타이머 증가
        timer += Time.deltaTime * currentSpeed;

        // Sin 곡선을 이용한 Y축 이동 계산
        float newY = startPos.y + Mathf.Sin(timer) * currentAmount;

        // 실제 위치 적용
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
    }

    // 상태가 바뀔 때 어색하게 튀는 것을 방지하기 위해 타이머를 초기화하거나 보간할 수 있습니다.
    public void ResetTimer() => timer = 0f;
}
