using System.Collections;
using UnityEngine;

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.03 오후 16:00
 *  마지막 수정 일자 : 26.03.03 오후 16:34
 *  
 *  [스크립트 목적 및 내용]
 *  1. 백그라운드 위치 조정 스크립트
 *    1-1. 백그라운드가 카메라보다 느리게 움직이도록 하는 패럴랙스 효과를 위해
 *    1-2. 카메라 이동에 따라 백그라운드 위치를 조정함
 *    1-3. Y축을 맞춰둔 이후에는 X축을 일정 시간마다 좌우로 움직이도록 설정하여,
 *         바람에 흔들리는 효과를 줄 수 있음
 * 
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class ViewBackground : MonoBehaviour
{
    [Header("# Background Offset")]
    public float startValueX = 0f;
    public float endValueX = 0f;

    [Header("# Background Move Data")]
    public float durationX = 2.0f; // 이동 시간 (기본 2초)
    public WaitForSeconds wait = new WaitForSeconds(1f); // 이동 후 대기 시간 (기본 1초)

    private void Start()
    {
        StartCoroutine(nameof(MoveBackground)); // 백그라운드 움직이는 코루틴 시작
    }

    IEnumerator MoveBackground()
    {
        // yield return wait;

        float elapsed = 0f;

        while (elapsed < durationX)
        {
            elapsed += Time.deltaTime;

            // 0~1 사이의 비율 계산
            float t = Mathf.Clamp01(elapsed / durationX);

            // Mathf.Lerp(a, b, t) -> a + (b - a) * t
            float currentValueX = Mathf.Lerp(startValueX, endValueX, t);

            // currentValue를 사용하여 물체 이동
            transform.position = new Vector3(currentValueX, transform.position.y, transform.position.z);

            yield return null; // 다음 프레임까지 대기
        }

        // 최종값 보장
        transform.position = new Vector3(endValueX, transform.position.y, transform.position.z);

        float nextValueX = startValueX; // 시작값을 저장
        startValueX = endValueX;        // 시작값을 현재 위치로 업데이트
        endValueX = nextValueX;         // 끝값을 이전 시작값으로 업데이트

        yield return wait; // 1초 대기

        StartCoroutine(nameof(MoveBackground));
    }
}
