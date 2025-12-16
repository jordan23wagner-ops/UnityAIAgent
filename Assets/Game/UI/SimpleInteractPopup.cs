using UnityEngine;
using UnityEngine.UI;

public class SimpleInteractPopup : MonoBehaviour
{
    public GameObject popupRoot;
    public Text popupText;

    public void Show(string message)
    {
        if (popupRoot != null) popupRoot.SetActive(true);
        if (popupText != null) popupText.text = message;
    }

    public void Hide()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }
}
