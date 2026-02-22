using UnityEngine;
using UnityEngine.Rendering;

public class PlayerFollower : MonoBehaviour
{
    public SortingGroup sortingGroup;
    public Transform target;
    public Transform sortTarget;

    void LateUpdate()
    {
        // transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        // transform.position = new Vector3(0f, 0f, UpdateSortingZ);
        sortingGroup.sortingOrder = SortingOrderUtility.UpdateSortingY(sortTarget);
    }
}
