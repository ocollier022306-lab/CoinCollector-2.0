
using UnityEngine;
using TMPro;

public class LevelExit : MonoBehaviour
{
    public TMP_Text messageText;

    void Start()
    {
        if (messageText != null)
            messageText.text = "Collect all coins to unlock the exit!";
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance.AllCoinsCollected())
        {
            if (messageText != null)
                messageText.text = "YOU WIN! All coins collected!";
        }
        else
        {
            if (messageText != null)
                messageText.text = "You need more coins!";
        }
    }
}
