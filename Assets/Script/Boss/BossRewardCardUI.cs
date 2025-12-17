using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class BossRewardCardUI : MonoBehaviour
{
    public Image frameImage;
    public TMP_Text titleText;
    public TMP_Text descText;
    public Button button;

    private BossRewardDefinition reward;
    private Action<BossRewardDefinition> onClick;

    public void Bind(BossRewardDefinition r, Action<BossRewardDefinition> onClicked)
    {
        reward = r;
        onClick = onClicked;

        if (titleText != null) titleText.text = r != null ? r.title : "—";
        if (descText != null) descText.text = r != null ? r.description : "";

        if (frameImage != null)
            frameImage.color = GetRarityColor(r != null ? r.rarity : BossRewardRarity.Common);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (reward != null) onClick?.Invoke(reward);
            });
        }
    }

    private Color GetRarityColor(BossRewardRarity rarity)
    {
        return rarity switch
        {
            BossRewardRarity.Common => new Color(0.7f, 0.7f, 0.7f, 1f),     // серый
            BossRewardRarity.Rare => new Color(0.3f, 0.55f, 1f, 1f),        // синий
            BossRewardRarity.Legendary => new Color(1f, 0.55f, 0.15f, 1f),  // оранжевый
            _ => Color.white
        };
    }
}
