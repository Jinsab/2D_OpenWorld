using DG.Tweening;
using System.Collections;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.16 오후 15:00
 *  마지막 수정 일자 : 26.03.26 오후 21:39
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 부유 효과
 *    1-1. 어떤 형태로든 월드 내의 드롭 아이템 형태가 될 때 해당 효과를 부여
 *         - 튀어오르는 효과
 *         - 부유 효과
 *    1-2. 이 때, 정해진 스케일에 따라 위아래(Position Y) 값을 조정함
 *      
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class FloatingResource : MonoBehaviour
{
    // 부유 효과를 부여할 오브젝트 정보
    [Header(" # Floating Value")]
    public Vector3 startPos;
    public float speed = 0.5f;  // 움직이는 속도
    public float amount = 0.2f; // 위아래 움직임 폭 (최대 높이)

    [Header(" # Floating Enable")]
    public bool floatingEnable = true;
    
    private Tween bounceTween;

    private void OnEnable()
    {
        StartCoroutine(nameof(BounceEffect));
    }

    private void Update()
    {
        if (!floatingEnable)
            bounceTween.Kill();
    }

    private IEnumerator BounceEffect()
    {
        floatingEnable = false;
        startPos = transform.localPosition;
        startPos.y += amount;

        transform.localPosition = startPos;

        transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0.2f), speed);
        transform.DOPunchPosition(new Vector3(0, amount * 1.5f, 0), speed)
            .SetEase(Ease.Linear);
        
        yield return new WaitForSeconds(speed); // 튀어오른 이후 ...

        floatingEnable = true;

        // 현재 로컬 Y 위치에서 moveAmount만큼 위아래로 반복
        bounceTween = transform.DOLocalMoveY(startPos.y + amount, speed * 8)
            .SetLoops(-1, LoopType.Yoyo) // -1은 무한 반복, Yoyo는 왔다갔다하는 효과
            .SetEase(Ease.Linear);
    }
}
