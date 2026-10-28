using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int totalCoins = 10;
    public int coinsCollected = 0;
    public TMP_Text coinText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateUI();
    }

    public void CollectCoin()
    {
        coinsCollected++;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (coinText != null)
        {
            coinText.text = "Coins: " +
                coinsCollected + " / " + totalCoins;
        }
    }

    public bool AllCoinsCollected()
    {
        return coinsCollected >= totalCoins;
    }
}
