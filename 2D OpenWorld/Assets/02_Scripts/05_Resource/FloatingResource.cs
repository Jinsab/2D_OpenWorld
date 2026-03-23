using Unity.VisualScripting;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.16 오후 15:00
 *  마지막 수정 일자 : 26.03.22 오후 23:41
 *  
 *  [스크립트 목적 및 내용]
 *  1. 아이템 부유 효과
 *    1-1. 어떤 형태로드 월드 내의 드롭 아이템 형태가 된다면 부유 효과를 부여
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
    public float speed = 1.0f;  // 움직이는 속도
    public float amount = 0.5f; // 위아래 움직임 폭 (최대 높이)

    [Header(" # Floating Enable")]
    public bool floatingEnable = false;

    private void Awake()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        if (floatingEnable)
            return;

        // 사인 함수를 이용해 -amount ~ +amount 사이의 값을 반복하여 Y축에 더함
        float newY = startPos.y + Mathf.Sin(Time.time * speed) * amount;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }
}
