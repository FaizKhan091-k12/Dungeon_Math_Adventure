using UnityEngine;

public class DoorAnimation : MonoBehaviour
{
    public void PlayDoorAnim()
    {
        ClickMoveXWithSpine.Instance.PlayDoorAnimation();
    }

    public void PlayBuffAnimations()
    {
        ClickMoveXWithSpine.Instance.PlayBuff();
    }

    public void GotoPortal()
    {
        ClickMoveXWithSpine.Instance.GetComponent<Rigidbody2D>().simulated = false;
        Vector3 portalPos = new Vector3(-0.5f, -1.93f, 0f);
        StartCoroutine(ClickMoveXWithSpine.Instance.GoToPortal(portalPos));
    }


}
