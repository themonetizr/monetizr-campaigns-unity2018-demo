using Monetizr.Campaigns;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonetizrSample : MonoBehaviour
{
    public Sprite defaultRewardIcon;

    // Start is called before the first frame update
    void Start()
    {
        MonetizrManager.Initialize("N3KA74389AG670040673EFG92L9S7X", () => 
                {   
                    //if we want to show teaser on the screen
                    MonetizrManager.ShowTinyMenuTeaser();
                                        
                    StartCoroutine(ShowOffer());
                }, null);

        MonetizrManager.SetTeaserPosition(new Vector2(-420, 270));

        MonetizrManager.SetGameCoinAsset(RewardType.Coins, defaultRewardIcon, "Snail Costume", () =>
                {
                    return 0;// GameController.I.GetCoinsTotal();
                },
                (int reward) =>
                {
                    //GameController.I.AddCoinsTotal(reward);
                });

        MonetizrManager.defaultRewardAmount = 1;
        MonetizrManager.defaultTwitterLink = "https://twitter.com/omegaxrunner";
    }

    //show notifications manually
    IEnumerator ShowOffer()
    {
        yield return new WaitForSeconds(5);

        MonetizrManager.ShowRewardCenter(null);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
