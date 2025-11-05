using System.Collections;
using UnityEngine;

public class DealerButton : MonoBehaviour
{
    [Header("Move")]
    public float moveDuration = 0.75f;
    public Vector3 offset = new Vector3(0f, 0.35f, 0f); // ÁÂ¼® À§Ä¡¿¡¼­ »ìÂ¦ À§·Î

    Coroutine moveCo;

    public void TeleportTo(Transform seat)
    {
        if (seat == null) return;
        if (moveCo != null) StopCoroutine(moveCo);
        transform.position = SeatWorldPos(seat);
    }

    public void MoveTo(Transform seat)
    {
        if (seat == null) return;
        if (moveCo != null) StopCoroutine(moveCo);
        moveCo = StartCoroutine(Co_Move(SeatWorldPos(seat)));
    }

    Vector3 SeatWorldPos(Transform seat)
    {
        var p = seat.position + offset;
        p.z = 0f;
        return p;
    }

    IEnumerator Co_Move(Vector3 target)
    {
        Vector3 start = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, moveDuration);
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.position = target;
        moveCo = null;
    }
}
