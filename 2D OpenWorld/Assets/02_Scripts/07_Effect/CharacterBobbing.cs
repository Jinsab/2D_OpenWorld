using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.02.19 오후 20:43
 *  마지막 수정 일자 : 26.02.19 오후 22:22
 *  
 *  [스크립트 목적 및 내용]
 *  1. 이펙트 스크립트
 *    1-1. 
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class CharacterBobbing : MonoBehaviour
{
    [Header("# Shadow")]
    public Transform shadowRoot;

    [Header("# Movement Data")]
    public PlayerController Player;

    [Header("# Idle Bobbing")]
    public float idleSpeed = 2f;
    public float idleAmount = 0.05f;

    [Header("# Move Bobbing")]
    public float walkSpeed = 10f;
    public float walkAmount = 0.15f;
    public float runSpeed = 15f;
    public float runAmount = 0.2f;

    private Vector3 startPos;
    private Vector3 shadowPos;
    private float timer;

    [Header("# Squash and Stretch")]
    public float stretchAmount = 0.1f; // 얼마나 길쭉/넓적해질지

    [Header("# Effects")]
    public ParticleSystem dustParticle;
    public int particleCount;

    private bool hasEmittedDust = false;
    private ParticleSystemRenderer particleRenderer;

    private void Awake()
    {
        particleRenderer = dustParticle.GetComponent<ParticleSystemRenderer>();
    }

    void Start()
    {
        startPos = transform.localPosition;
        shadowPos = shadowRoot.localPosition;
    }

    void Update()
    {
        ApplyBobbing();
    }

    private void ApplyBobbing()
    {
        float currentSpeed = idleSpeed;
        float currentAmount = idleAmount;

        if (Player.PlayerMovement.IsMoving)
        {
            if (Player.PlayerMovement.IsRunning)
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
        float sinValue = Mathf.Sin(timer);
        float newY = startPos.y + Mathf.Sin(timer) * currentAmount;

        // 실제 위치 적용
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
        // shadowRoot.localPosition = new Vector3(shadowPos.x, newY + shadowPos.y, shadowPos.z);
        
        // 모양 변형
        ApplySquashAndStretch(sinValue);

        // 먼지 체크
        CheckForDust(sinValue);
    }

    private void ApplySquashAndStretch(float sinValue)
    {
        // 이동 중일 때만 더 강하게 적용하고 싶다면 isMoving 체크
        float multiplier = Player.PlayerMovement.IsMoving ? 1.0f : 0.2f;

        // sinValue는 -1 ~ 1 사이의 값
        float stretch = sinValue * stretchAmount * multiplier;

        // Y가 늘어날 때 X는 줄어들어야 부피가 유지되어 자연스러움
        if (Player.PlayerLook.CurrentLookDirection == LookDirection.Right)
            transform.localScale = new Vector3(-1f + stretch, 1f + stretch, 1f);
        else
            transform.localScale = new Vector3(1f - stretch, 1f + stretch, 1f);
    }

    private void CheckForDust(float sinValue)
    {
        if (!Player.PlayerMovement.IsMoving) return;

        // Sin 곡선이 거의 최하단(-1에 가까움)일 때 발이 땅에 닿았다고 판단
        if (sinValue < -0.9f)
        {
            if (!hasEmittedDust)
            {
                particleRenderer.sortingOrder = Player.PlayerMovement.PlayerOrder;
                dustParticle.Emit(particleCount); // 먼지 n개 생성
                hasEmittedDust = true;
            }
        }
        else
        {
            // Sin 값이 다시 올라오면 플래그 리셋
            hasEmittedDust = false;
        }
    }

    // 상태가 바뀔 때 어색하게 튀는 것을 방지하기 위해 타이머를 초기화하거나 보간할 수 있습니다.
    public void ResetTimer() => timer = 0f;
}
