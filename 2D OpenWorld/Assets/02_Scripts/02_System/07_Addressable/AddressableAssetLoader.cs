using AYellowpaper.SerializedCollections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets; // Addressables 필수
using UnityEngine.ResourceManagement.AsyncOperations; // 비동기 작업 필수
using UnityEngine.U2D.Animation; // Sprite Library 필수

/*  
 *  [프로젝트 제목]
 *  2D 오픈월드 생존제작
 *             
 *  [프로젝트 일자]
 *  파일 생성 일자 : 26.03.09 오후 14:32
 *  마지막 수정 일자 : 26.03.10 오후 17:55
 *  
 *  [스크립트 목적 및 내용]
 *  1. 유니티 어드레서블 - 에셋 로더
 *    1-1. 유니티 Addressables 시스템을 활용
 *    1-2. 특정 부위의 SpriteLibraryAsset을 String Address 경로로 로드하여 SpriteLibrary에 적용
 *    1-3. 모든 부위도 가능
 *    1-4. 특정 부위의 SpriteLibrary를 String Address 경로로 변환하는 함수도 포함
 *  
 *  [스크립트 작성 도움 출처]
 *  1. 
 */

public class AddressableAssetLoader : MonoBehaviour
{
    [Header("# Addressable Asset Loader")]
    public static AddressableAssetLoader Instance;

    [Header("# Caching Asset Data")]
    [SerializedDictionary("string", "SpriteLibrary")]
    public SerializedDictionary<string, SpriteLibrary> CachingAssetData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴 방지
        }
        else
        {
            Destroy(gameObject);
        }

        CachingAssetData = new SerializedDictionary<string, SpriteLibrary>();
    }

    // 에셋 로드 요청이 들어오면 딕셔너리에 해당 주소(Key)의 에셋이 있는지 확인
    // 이미 있다면 await 없이 즉시 반환
    // 없다면 Addressables로 로드 시도 후 딕셔너리에 저장하고 반환
    public SpriteLibrary TryAssetLoad(string adr)
    {
        // 내부 캐싱에 해당 주소(Key)가 있는지 확인하기
        if (CachingAssetData.ContainsKey(adr))
        {
            if (CachingAssetData.TryGetValue(adr, out SpriteLibrary cachedLib))
            {
                if (cachedLib != null)
                {
                    // 캐싱된 에셋이 유효하다면 즉시 반환
                    Debug.Log($"[Cache] {adr} 에셋이 이미 로드되어 있습니다.");
                    return cachedLib;
                }
                else
                {
                    
                }
            }
        }
        else
        {
            CachingAssetData.Add(adr, );
        }
    }

    // 특정 부위의 SpriteLibraryAsset을 로드하여 적용하는 함수
    public async Awaitable LoadAndApplyAsset(string assetAddress, SpriteLibrary library)
    {
        if (string.IsNullOrEmpty(assetAddress) || library == null) return;

        // Addressables를 통해 비동기로 에셋 로드 시도
        AsyncOperationHandle<SpriteLibraryAsset> handle = Addressables.LoadAssetAsync<SpriteLibraryAsset>(assetAddress);

        // 작업 완료까지 대기 (await를 사용하려면 함수에 async 키워드 필요)
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            // 성공 시에는 라이브러리에 에셋 할당
            // 성공 로그 출력 및 내부 캐싱을 도입하여 딕셔너리에 값을 넣음
            // 최종적으로는 메모리 해제
            library.spriteLibraryAsset = handle.Result;
            CachingAssetData.TryAdd(assetAddress, library);

            Debug.Log($"[Addressables] {assetAddress} 로드 및 적용 완료");
        }
        else
        {
            // 실패 시에는 에러 로그 출력 및 메모리 해제

            Debug.LogError($"[Addressables] {assetAddress} 로드 실패!");

            Addressables.Release(handle);
        }

        // 성공, 실패 상관 없이 메모리 해제
        // Addressables.Release(handle);
    }

    // 모든 부위의 SpriteLibraryAsset을 로드하여 적용하는 함수
    public async Awaitable LoadAndApplyAsset(string[] assetAdrs, SpriteLibrary[] libs)
    {
        if (assetAdrs == null || libs == null) return;

        // 1. 모든 핸들을 리스트에 담아 동시에 실행
        var locationsTask = Addressables.LoadResourceLocationsAsync(assetAdrs, Addressables.MergeMode.Union);
        var locations = await locationsTask.Task;

        // 2. 모든 에셋을 한 번에 로드 (IList<SpriteLibraryAsset>)
        var loadTask = Addressables.LoadAssetsAsync<SpriteLibraryAsset>(locations, null);
        var loadedAssets = await loadTask.Task;

        // 3. 로드된 결과를 부위별 컴포넌트에 매칭하여 할당
        for (int i = 0; i < loadedAssets.Count; i++)
        {
            // 각 부위 컴포넌트에 안전하게 할당
            if (libs[i] != null)
            {
                libs[i].spriteLibraryAsset = loadedAssets[i];
                CachingAssetData.TryAdd(assetAdrs[i], libs[i]);

                Debug.Log($"[Addressables] {assetAdrs[i]} 로드 및 적용 완료");
            }
        }
    }

    //특정 부위의 SpriteLibrary를 String Address 경로로 변환하는 함수
    public void SaveAssetAddress(string assetAddress, SpriteLibrary library)
    {
        if (library == null || library.spriteLibraryAsset == null) return;

        var asset = library.spriteLibraryAsset;

        // 모든 로케이터를 순회하며 해당 에셋의 위치 정보를 찾습니다.
        foreach (var locator in Addressables.ResourceLocators)
        {
            // 에셋의 인스턴스 ID나 참조를 통해 위치 정보를 확인합니다.
            if (locator.Locate(asset, typeof(SpriteLibraryAsset), out var locations))
            {
                // 첫 번째 일치하는 주소(Primary Key)를 반환합니다.
                Debug.Log(locations[0].PrimaryKey);
            }

            Addressables.Release(locator);
        }

        Debug.Log("Address Not Found");
    }

    // 씬이 종료되거나 캐릭터가 파괴될 때 메모리 해제가 필요할 수 있습니다.
    // Addressables.Release(handle); 호출 필요
}