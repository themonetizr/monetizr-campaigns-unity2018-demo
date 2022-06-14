using Monetizr.Campaigns;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonetizrSample : MonoBehaviour
{
    //reward icon (coin, gem, whatever)
    public Sprite defaultRewardIcon;

    void Start()
    {
        //initilization should be done before calling everything
        //please keep in mind, that it will start downloading resources in the background and
        //it will take sometime
        MonetizrManager.Initialize("N3KA74389AG670040673EFG92L9S7X", () => 
                {       
                    //Show small banner
                    //MonetizrManager.ShowTinyMenuTeaser();


                    //if we want we don't want to show teaser on the screen, we must call initializeBuiltinMissions 
                    MonetizrManager.Instance.initializeBuiltinMissions();

                    if (MonetizrManager.Instance.missionsManager.IsAllMissionsClaimed())
                    {
                        Debug.LogWarning("No active campaings");
                    }

                    StartCoroutine(ShowOffer());
                }, null);

        MonetizrManager.SetTeaserPosition(new Vector2(-420, 270));

        MonetizrManager.SetGameCoinAsset(RewardType.Coins, defaultRewardIcon, "King Suit", () =>
                {
                    //here we should return amount of item that player already have
                    return 0;// GameController.I.GetCoinsTotal();
                },
                (int reward) =>
                {
                    //here we should specify how we can give reward to player
                    //GameController.I.AddCoinsTotal(reward);
                });

        //How much of game currency we will get
        MonetizrManager.defaultRewardAmount = 1;

        //Game twitter link
        MonetizrManager.defaultTwitterLink = "https://twitter.com/omegaxrunner";
    }

    //show notifications manually
    IEnumerator ShowOffer()
    {
        yield return new WaitForSeconds(1);

        MonetizrManager.ShowRewardCenter(null);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
