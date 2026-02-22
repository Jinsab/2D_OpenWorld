using UnityEngine;

public class ShadowCam : MonoBehaviour
{
    public Camera shadowCamera;
    public Material targetMaterial;

    void LateUpdate()
    {
        // 그림자 카메라의 현재 위치와 크기(orthographicSize)를 가져옵니다.
        Vector3 camPos = shadowCamera.transform.position;
        float size = shadowCamera.orthographicSize;

        // 셰이더 프로퍼티로 전달 (가로세로 비율 고려)
        // 이 값들을 이용해 셰이더 내부에서 WorldPos를 UV로 변환합니다.
        targetMaterial.SetVector("_ShadowCamData", new Vector4(camPos.x, camPos.y, size, size));
    }
}
