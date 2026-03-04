using UnityEngine;
using UnityEngine.AddressableAssets; // Addressables 필수
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 작업 필수
using UnityEngine.U2D.Animation; // Sprite Library 필수

public class AddressableAssetLoader : MonoBehaviour
{
    // 특정 부위의 SpriteLibraryAsset을 로드하여 적용하는 함수
    public async void LoadAndApplyAsset(string assetAddress, SpriteLibrary library)
    {
        if (string.IsNullOrEmpty(assetAddress) || library == null) return;

        // Addressables를 통해 비동기로 에셋 로드 시도
        AsyncOperationHandle<SpriteLibraryAsset> handle = Addressables.LoadAssetAsync<SpriteLibraryAsset>(assetAddress);

        // 작업 완료까지 대기 (await를 사용하려면 함수에 async 키워드 필요)
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            // 성공 시 라이브러리에 에셋 할당
            library.spriteLibraryAsset = handle.Result;
            Debug.Log($"[Addressables] {assetAddress} 로드 및 적용 완료");
        }
        else
        {
            Debug.LogError($"[Addressables] {assetAddress} 로드 실패!");
            // 실패 시 메모리 해제
            Addressables.Release(handle);
        }
    }

    // 씬이 종료되거나 캐릭터가 파괴될 때 메모리 해제가 필요할 수 있습니다.
    // Addressables.Release(handle); 호출 필요
}