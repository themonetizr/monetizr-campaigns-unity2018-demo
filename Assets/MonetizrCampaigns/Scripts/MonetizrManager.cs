//undefine this to test slow internet
//#define TEST_SLOW_LATENCY

//if we define this - video and survey campaigns will work
//#define USING_WEBVIEW

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Monetizr.Campaigns;
using UnityEngine.Networking;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine.UI;
using System.IO;
using UnityEngine.Assertions;
using System.IO.Compression;
using System.Text;

namespace Monetizr.Campaigns
{
    internal enum ErrorType
    {
        NotinitializedSDK,
        SimultaneusAdAssets,
        AdAssetStillShowing,
        ConnectionError,
    };

    internal static class MonetizrErrors
    {
        public static readonly Dictionary<ErrorType, string> msg = new Dictionary<ErrorType, string>()
        {
            { ErrorType.NotinitializedSDK, "You're trying to use Monetizer SDK before it's been initialized. Call MonetizerManager.Initalize first." },
            { ErrorType.SimultaneusAdAssets, "Simultaneous display of multiple ads is not supported!" },
            { ErrorType.AdAssetStillShowing, "Some ad asset are still showing." },
            { ErrorType.ConnectionError, "Connection error while getting list of campaigns!" }

        };
    }

    /// <summary>
    /// Predefined asset types for easier access
    /// </summary>
    public enum AssetsType
    {
        Unknown,
        BrandLogoSprite, //icon
        BrandBannerSprite, //banner
        BrandRewardLogoSprite, //logo
        BrandRewardBannerSprite, //reward_banner
        SurveyURLString, //survey
        //VideoURLString, //video url
        VideoFilePathString, //video url
        BrandTitleString, //text
        TinyTeaserTexture, //text
        //Html5ZipURLString,
        Html5PathString,
        TiledBackgroundSprite,
        CampaignHeaderTextColor,
        CampaignTextColor,
        HeaderTextColor,
        CampaignBackgroundColor,
        CustomCoinSprite,
        CustomCoinString,
        LoadingScreenSprite,
    }

    /// <summary>
    /// ChallengeExtention for easier access to Challenge assets
    /// TODO: merge with Challenge?
    /// </summary>
    internal class ChallengeExtention
    {
        private static readonly Dictionary<AssetsType, System.Type> AssetsSystemTypes = new Dictionary<AssetsType, System.Type>()
        {
            { AssetsType.BrandLogoSprite, typeof(Sprite) },
            { AssetsType.BrandBannerSprite, typeof(Sprite) },
            { AssetsType.BrandRewardLogoSprite, typeof(Sprite) },
            { AssetsType.BrandRewardBannerSprite, typeof(Sprite) },
            { AssetsType.SurveyURLString, typeof(String) },
            //{ AssetsType.VideoURLString, typeof(String) },
            { AssetsType.VideoFilePathString, typeof(String) },
            { AssetsType.BrandTitleString, typeof(String) },
            { AssetsType.TinyTeaserTexture, typeof(Texture2D) },
            //{ AssetsType.Html5ZipURLString, typeof(String) },
            { AssetsType.Html5PathString, typeof(String) },
            { AssetsType.HeaderTextColor, typeof(Color) },
            { AssetsType.CampaignTextColor, typeof(Color) },
            { AssetsType.CampaignHeaderTextColor, typeof(Color) },
            { AssetsType.TiledBackgroundSprite, typeof(Sprite) },
            { AssetsType.CampaignBackgroundColor, typeof(Color) },
            { AssetsType.CustomCoinSprite, typeof(Sprite) },
            { AssetsType.CustomCoinString, typeof(String) },
            { AssetsType.LoadingScreenSprite, typeof(Sprite) },

        };


        public Challenge challenge { get; private set; }
        private Dictionary<AssetsType, object> assets = new Dictionary<AssetsType, object>();
        private Dictionary<AssetsType, string> assetsUrl = new Dictionary<AssetsType, string>();

        public bool isChallengeLoaded;

        public ChallengeExtention(Challenge challenge)
        {
            this.challenge = challenge;
            this.isChallengeLoaded = true;
        }

        public void SetAsset<T>(AssetsType t, object asset)
        {
            if(assets.ContainsKey(t))
            {
                Log.PrintWarning($"An item {t} already exist in the campaign {challenge.id}");
                return;
            }

            assets.Add(t, asset);
        }

        public bool HasAsset(AssetsType t)
        {
            return assets.ContainsKey(t);
        }

        public string GetAssetUrl(AssetsType t)
        {
            return assetsUrl[t];
        }

        public T GetAsset<T>(AssetsType t)
        {
            if (AssetsSystemTypes[t] != typeof(T))
                throw new ArgumentException($"AssetsType {t} and {typeof(T)} do not match!");

            if (!assets.ContainsKey(t))
                //throw new ArgumentException($"Requested asset {t} doesn't exist in challenge!");
                return default(T);

            return (T)Convert.ChangeType(assets[t], typeof(T));
        }

        internal void SetAssetUrl(AssetsType t, string url)
        {
            assetsUrl.Add(t, url);
        }
    }

    /// <summary>
    /// Extention to support async/await in the DownloadAssetData
    /// </summary>
    internal static class ExtensionMethods
    {
        public static TaskAwaiter GetAwaiter(this AsyncOperation asyncOp)
        {
            var tcs = new TaskCompletionSource<object>();
            asyncOp.completed += obj => { tcs.SetResult(null); };
            return ((Task)tcs.Task).GetAwaiter();
        }
    }

    internal class DownloadHelper
    {
        /// <summary>
        /// Downloads any type of asset and returns its data as an array of bytes
        /// </summary>
        public static async Task<byte[]> DownloadAssetData(string url, Action onDownloadFailed = null)
        {
            UnityWebRequest uwr = UnityWebRequest.Get(url);

            await uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Log.PrintError($"Network error {uwr.error} with {url}");
                onDownloadFailed?.Invoke();
                return null;
            }

            return uwr.downloadHandler.data;
        }
    }

    public enum RewardType
    {
        Coins,
        PremiumCurrency
    }

    /// <summary>
    /// Main manager for Monetizr
    /// </summary>
    public class MonetizrManager : MonoBehaviour
    {
        internal static bool keepLocalClaimData;
        internal static bool serverClaimForCampaigns;
        internal static bool claimForSkippedCampaigns;


        //position relative to center with 1080x1920 screen resolution
        private static Vector2 tinyTeaserPosition = new Vector2(-430, 600);

        internal ChallengesClient _challengesClient { get; private set; }

        private static MonetizrManager instance = null;

        private UIController uiController = null;

        private string activeChallengeId = null;

        private Action<bool> soundSwitch;
        private Action<bool> onRequestComplete;

        private bool isActive = false;
        private bool isMissionsIsOudated = true;

        //Storing ids in separate list to get faster access (the same as Keys in challenges dictionary below)
        private List<string> challengesId = new List<string>();
        private Dictionary<String, ChallengeExtention> challenges = new Dictionary<String, ChallengeExtention>();
        internal static bool tinyTeaserCanBeVisible;

        internal MissionsManager missionsManager = null;

        internal class GameReward
        {
            internal Sprite icon;
            internal string title;
            internal Func<int> GetCurrencyFunc;
            internal Action<int> AddCurrencyAction;
        }

        public static int defaultRewardAmount = 1000;
        public static string defaultTwitterLink = "";

        internal static Dictionary<RewardType, GameReward> gameRewards = new Dictionary<RewardType, GameReward>();
        private static int debugAttempt = 0;
        internal static int abTestSegment = 0;
        
        public static void SetGameCoinAsset(RewardType rt, Sprite defaultRewardIcon, string title, Func<int> GetCurrencyFunc, Action<int> AddCurrencyAction)
        {
            GameReward gr = new GameReward()
            {
                icon = defaultRewardIcon,
                title = title,
                GetCurrencyFunc = GetCurrencyFunc,
                AddCurrencyAction = AddCurrencyAction,
            };

            gameRewards[rt] = gr;
        }


        public static MonetizrManager Initialize(string apiKey, Action onRequestComplete, Action<bool> soundSwitch)
        {
            if (instance != null)
            {
                //instance.RequestChallenges(onRequestComplete);
                return instance;
            }

#if UNITY_EDITOR
            keepLocalClaimData = true;
            serverClaimForCampaigns = false;
            claimForSkippedCampaigns = true;
#else
            keepLocalClaimData = true;
            serverClaimForCampaigns = true;
            claimForSkippedCampaigns = false;
#endif
                       

            Log.Print($"MonetizrManager Initialize: {apiKey}");

            var monetizrObject = new GameObject("MonetizrManager");
            var monetizrManager = monetizrObject.AddComponent<MonetizrManager>();

            DontDestroyOnLoad(monetizrObject);
            instance = monetizrManager;

            monetizrManager.Initalize(apiKey, onRequestComplete, soundSwitch);



            return instance;
        }

        public static MonetizrManager Instance
        {
            get
            {
                return instance;
            }
        }

        internal static MonetizrAnalytics Analytics
        {
            get
            {
                return instance._challengesClient.analytics;
            }
        }

        void OnApplicationQuit()
        {
            Analytics.OnApplicationQuit();
        }

        /// <summary>
        /// Initialize
        /// </summary>
        private void Initalize(string apiKey, Action gameOnInitSuccess, Action<bool> soundSwitch)
        {
#if USING_WEBVIEW
            if (!UniWebView.IsWebViewSupported)
            {
                Log.Print("WebView isn't supported on current platform!");
            }
#endif

            missionsManager = new MissionsManager();

            //missionsManager.CleanUp();

            this.soundSwitch = soundSwitch;

            _challengesClient = new ChallengesClient(apiKey);

            InitializeUI();

            onRequestComplete = (bool isOk) => {

                if(!isOk)
                {
                    Log.Print("ERROR: Request complete is not okay!");
                    return;
                }

                Log.Print("MonetizrManager initialization okay!");

                isActive = true;

//moved together with showing teaser, because here in-game logic may not be ready
//                createEmbedMissions();

                gameOnInitSuccess?.Invoke();

                if (tinyTeaserCanBeVisible)
                    ShowTinyMenuTeaser(null);

            };

            RequestChallenges(onRequestComplete);
        }

        //TODO: add defines

        public void initializeBuiltinMissions()
        {
            if(isMissionsIsOudated)
                missionsManager.AddMissionsToCampaigns();

            isMissionsIsOudated = false;
            //RegisterSponsoredMission(RewardType.Coins, 1000);

            //RegisterSponsoredMission2(RewardType.Coins, 500);

        }


        //check if all mission with current campain claimed
        internal bool CheckFullCampaignClaim(Mission m)
        {
            return missionsManager.CheckFullCampaignClaim(m);
        }

        internal void SaveClaimedReward(Mission m)
        {
            missionsManager.SaveClaimedReward(m);
        }

        internal void CleanRewardsClaims()
        {
            missionsManager.CleanRewardsClaims();
        }
              
        internal string GetCurrentAPIkey()
        {
            return _challengesClient.currentApiKey;
        }

        internal void ChangeAPIKey(string apiKey)
        {
            if (apiKey == _challengesClient.currentApiKey)
                return;
            
            _challengesClient.Close();

            _challengesClient = new ChallengesClient(apiKey);
                        
            RequestCampaigns();
        }

        internal void RequestCampaigns()
        {
            isActive = false;

            uiController.DestroyTinyMenuTeaser();

            missionsManager.CleanUp();

            challenges.Clear();
            challengesId.Clear();

            RequestChallenges(onRequestComplete);
        }

        public void SoundSwitch(bool on)
        {
            soundSwitch?.Invoke(on);
        }

        private void InitializeUI()
        {
            uiController = new UIController();
        }

        private static void FillInfo(Mission m)
        {
            var ch = m.campaignId;//MonetizrManager.Instance.GetActiveChallenge();

            if(!MonetizrManager.Instance.HasCampaign(ch))
            {
                m.brandBanner = MonetizrManager.Instance.LoadSpriteFromCache(m.campaignId, m.brandBannerUrl);
                m.brandLogo = MonetizrManager.Instance.LoadSpriteFromCache(m.campaignId, m.brandLogoUrl);
                m.brandRewardBanner = MonetizrManager.Instance.LoadSpriteFromCache(m.campaignId, m.brandRewardBannerUrl);
                return;
            }

            m.brandBanner = MonetizrManager.Instance.GetAsset<Sprite>(ch, AssetsType.BrandBannerSprite);
            m.brandLogo = MonetizrManager.Instance.GetAsset<Sprite>(ch, AssetsType.BrandLogoSprite);
            m.brandName = MonetizrManager.Instance.GetAsset<string>(ch, AssetsType.BrandTitleString);
            m.brandRewardBanner = MonetizrManager.Instance.GetAsset<Sprite>(ch, AssetsType.BrandRewardBannerSprite);
        }

        internal static void ShowNotification(Action<bool> onComplete, Mission m, PanelId panelId)
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);


            /*MissionUIDescription sponsoredMsns = null;

            //no mission for notification, get active
            if (m == null)
            {
                if (!instance.HasChallengesAndActive())
                {
                    onComplete?.Invoke(false);
                    return;
                }

                sponsoredMsns = SelectSponsoredMissionAndFillInfo();

                if (sponsoredMsns == null)
                    return;
            }
            else //otherwise use predefined
            {
                sponsoredMsns = m;
            }

            //no survey link for survey notification
            if (panelId == PanelId.SurveyNotification)
            {
                if (MonetizrManager.Instance.GetAsset<string>(sponsoredMsns.campaignId, AssetsType.SurveyURLString) == null)
                {
                    onComplete?.Invoke(false);
                    return;
                }
            
                onComplete = (bool _) =>
                {
                    ShowSurvey(onComplete, sponsoredMsns);
                };
            }*/

            instance.uiController.ShowPanelFromPrefab("MonetizrNotifyPanel",
                panelId,
                onComplete,
                true,
                m);
        }

        internal static void ShowDebug()
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);

            debugAttempt++;

            if (debugAttempt != 10)
                return;

            debugAttempt = 0;            

            instance.uiController.ShowPanelFromPrefab("MonetizrDebugPanel");
        }

        public static void ShowStartupNotification(Action<bool> onComplete)
        {
            Mission sponsoredMsns = instance.missionsManager.missions.Find((Mission item) => { return item.isSponsored; });

            if (sponsoredMsns == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            FillInfo(sponsoredMsns);

            ShowNotification(onComplete, sponsoredMsns, PanelId.StartNotification);
        }

        internal static void ShowCongratsNotification(Action<bool> onComplete, Mission m)
        {
            ShowNotification(onComplete, m, PanelId.CongratsNotification);
        }

        internal static void TryShowSurveyNotification(Action onComplete)
        {
            //MissionUIDescription sponsoredMsns = instance.missionsManager.getCampaignReadyForSurvey();

            Mission sponsoredMsns = instance.missionsManager.FindActiveSurveyMission();

            if (sponsoredMsns == null)
            {
                onComplete?.Invoke();
                return;
            }
                        
            FillInfo(sponsoredMsns);

            Action<bool> onSurveyComplete = (bool isSkipped) =>
            {
                if (MonetizrManager.claimForSkippedCampaigns)
                    isSkipped = false;

                if (!isSkipped)
                {
                    //sponsoredMsns.AddPremiumCurrencyAction.Invoke(sponsoredMsns.reward);

                    //MonetizrManager.gameRewards[sponsoredMsns.rewardType].AddCurrencyAction(sponsoredMsns.reward);

                    //ShowCongratsNotification(onComplete, sponsoredMsns);

                    Instance.ClaimMission(sponsoredMsns, isSkipped, true, onComplete);
                }
                else
                {
                    onComplete?.Invoke();
                }

                
            };

            ShowNotification((bool _) => { ShowSurvey(onSurveyComplete, sponsoredMsns); }, 
                sponsoredMsns, 
                PanelId.SurveyNotification);
        }

        internal void ClaimMissionData(Mission m)
        {
            if (m.type == MissionType.VideoReward)
            {
                ShowRewardCenter(null);
                //m.AddPremiumCurrencyAction.Invoke(m.reward);

                gameRewards[m.rewardType].AddCurrencyAction(m.reward);
            }
            else if (m.type == MissionType.MutiplyReward)
            {
                m.reward *= 2;

                //m.AddNormalCurrencyAction.Invoke(m.reward);

                gameRewards[m.rewardType].AddCurrencyAction(m.reward);
            }
            else if (m.type == MissionType.SurveyReward)
            {
                gameRewards[m.rewardType].AddCurrencyAction(m.reward);

                //ShowRewardCenter(null);
            }
            else if (m.type == MissionType.TwitterReward)
            {
                gameRewards[m.rewardType].AddCurrencyAction(m.reward);

                //ShowRewardCenter(null);
            }

            if (keepLocalClaimData)
                Instance.SaveClaimedReward(m);
        }

        internal void ClaimMission(Mission m, bool isSkipped, bool showCongratsScreen, Action onComplete)
        {
            if (claimForSkippedCampaigns)
                isSkipped = false;

            if (isSkipped)
                return;


            //m.isDisabled = true;

            ClaimMissionData(m);

            if (Instance.missionsManager.TryToActivateSurvey(m))
            {
                //UpdateUI();
            }

            if (!showCongratsScreen)
            {
                onComplete?.Invoke();
                return;
            }

            ShowCongratsNotification((bool _) =>
            {

                onComplete?.Invoke();

            }, m);



        }


        public static void RegisterUserDefinedMission(string missionTitle, string missionDescription, Sprite missionIcon, RewardType rt, int reward, float progress, Action onClaimButtonPress)
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);

            Mission m = new Mission()
            {
                missionTitle = missionTitle,
                missionDescription = missionDescription,
                missionIcon = missionIcon,
                rewardType = rt,
                reward = reward,
                progress = progress,
                isSponsored = false,
                onClaimButtonPress = onClaimButtonPress,
                brandBanner = null,
            };

            instance.missionsManager.AddMission(m);
        }

        //TODO: need to connect now this mission and campaign from the server
        //next time once we register the mission it should connect with the same campaign
        public static void RegisterSponsoredMission(RewardType rt, int rewardAmount)
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);

            Mission m = new Mission()
            {
                //sponsoredId = id,
                rewardType = rt,
                type = MissionType.VideoReward,
                //rewardIcon = rewardIcon,
                reward = rewardAmount,
                isSponsored = true,
                // AddPremiumCurrencyAction = AddPremiumCurrencyAction,
                //rewardTitle = rewardTitle,
            };

            //
            instance.missionsManager.AddMissionAndBindToCampaign(m);
        }

        /// <summary>
        /// You need to earn goal amount of money to double it
        /// </summary>
        /// <param name="rewardIcon">Coins icon</param>
        /// <param name="goalAmount">How much you need to earn</param>
        /// <param name="rewardTitle">Coins</param>
        /// <param name="GetNormalCurrencyFunc">Get coins func</param>
        /// <param name="AddNormalCurrencyAction">Add coins to user account</param>
        public static void RegisterSponsoredMission2(RewardType rt, int goalAmount)
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);

            Mission m = new Mission()
            {
                //sponsoredId = id,
                rewardType = rt,
                startMoney = gameRewards[rt].GetCurrencyFunc(),
                type = MissionType.MutiplyReward,
                //rewardIcon = rewardIcon,
                reward = goalAmount,
                isSponsored = true,
                //AddNormalCurrencyAction = AddNormalCurrencyAction,
                //GetNormalCurrencyFunc = GetNormalCurrencyFunc,
                //rewardTitle = rewardTitle,
            };

            //
            instance.missionsManager.AddMissionAndBindToCampaign(m);
        }


        internal static void CleanUserDefinedMissions()
        {
            instance.missionsManager.CleanUserDefinedMissions();
        }

        public static void ShowRewardCenter(Action UpdateGameUI, Action<bool> onComplete = null)
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);

            UpdateGameUI?.Invoke();

            var challengeId = MonetizrManager.Instance.GetActiveChallenge();

            var m = instance.missionsManager.GetMission(challengeId);

            if (m == null)
                return;

            if (Instance.missionsManager.missions.Count == 1)
            {
                Instance._PressSingleMission(m);
                return;
            }
            
            

            Log.Print($"ShowRewardCenter with {m?.campaignId}");

            instance.uiController.ShowPanelFromPrefab("MonetizrRewardCenterPanel", PanelId.RewardCenter, onComplete, true, m);
        }

        internal static void HideRewardCenter()
        {
            instance.uiController.HidePanel(PanelId.RewardCenter);
        }

        internal void _PressSingleMission(Mission m)
        {
            //if notification is alredy visible - do nothing
            if (uiController.panels.ContainsKey(PanelId.TwitterNotification))
                return;

            if (m.isClaimed == ClaimState.Claimed)
                return;

            Action<bool> onTaskComplete = (bool isSkipped) =>
            {
                MonetizrManager.Analytics.TrackEvent("Campaign rewarded", m);

                m.isClaimed = ClaimState.Claimed;
                missionsManager.SaveAll();

                OnClaimRewardComplete(m, isSkipped, null);

                HideTinyMenuTeaser();
            };


            if (m.isClaimed == ClaimState.NotClaimed)
            {
                MonetizrManager.Analytics.TrackEvent("Campaign shown", m);

                ShowNotification((bool isSkipped) => 
                    {
                        if (!isSkipped)
                        {
                            m.isClaimed = ClaimState.CompletedNotClaimed;
                            missionsManager.SaveAll();

                            MonetizrManager.Analytics.TrackEvent("Campaign claimed", m);

                            MonetizrManager.GoToLink(onTaskComplete, m);
                        }
                    },
                    
                    m,
                    PanelId.TwitterNotification);
            }
            else
            {
                onTaskComplete.Invoke(false);
            }
            

        }

        internal static void _ShowWebView(Action<bool> onComplete, PanelId id, Mission m = null)
        {
            Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);

            if (!instance.isActive)
                return;

            instance.uiController.ShowPanelFromPrefab("MonetizrWebViewPanel", id, onComplete, false, m);
        }

        internal static void GoToLink(Action<bool> onComplete, Mission m = null)
        {
            Application.OpenURL(m.surveyUrl);
            onComplete.Invoke(false);
        }

        internal static void ShowSurvey(Action<bool> onComplete, Mission m = null)
        {
            _ShowWebView(onComplete, PanelId.SurveyWebView, m);
        }

        internal static void ShowHTML5(Action<bool> onComplete, Mission m = null)
        {
            _ShowWebView(onComplete, PanelId.Html5WebView, m);
        }

        internal static void ShowWebVideo(Action<bool> onComplete, Mission m = null)
        {
            _ShowWebView(onComplete, PanelId.VideoWebView, m);
        }

        public static void SetTeaserPosition(Vector2 pos)
        {
            tinyTeaserPosition = pos;
        }

        public static void OnStartGameLevel(Action onComplete)
        {
            if (instance == null)
            {
                onComplete?.Invoke();
                return;
            }

            TryShowSurveyNotification(onComplete);
        }

        public static void ShowTinyMenuTeaser(Action UpdateGameUI = null)
        {
            //Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);
            if (instance == null)
                return;

            tinyTeaserCanBeVisible = true;

            instance.initializeBuiltinMissions();

            //has some challanges
            if (!instance.HasChallengesAndActive())
                return;

            //has some active missions
            if (instance.missionsManager.missions.Find((Mission m) => { return m.isClaimed != ClaimState.Claimed; }) == null)
                return;

            var challengeId = MonetizrManager.Instance.GetActiveChallenge();
            if(!instance.HasAsset(challengeId,AssetsType.TinyTeaserTexture))
            {
                Log.Print("No texture for tiny teaser!");
                return;
            }

            instance.uiController.ShowTinyMenuTeaser(tinyTeaserPosition, UpdateGameUI);
        }

        public static void HideTinyMenuTeaser()
        {
            //Assert.IsNotNull(instance, MonetizrErrors.msg[ErrorType.NotinitializedSDK]);
            if (instance == null)
                return;

            tinyTeaserCanBeVisible = false;

            if (!instance.isActive)
                return;

            instance.uiController.HidePanel(PanelId.TinyMenuTeaser);
        }

        internal void OnClaimRewardComplete(Mission mission, bool isSkipped, Action updateUIDelegate)
        {
            if (claimForSkippedCampaigns)
                isSkipped = false;

            if (isSkipped)
                return;

            ShowCongratsNotification((bool _) =>
            {
                bool updateUI = false;

                mission.state = MissionUIState.ToBeHidden;

                mission.isClaimed = ClaimState.Claimed;

                ClaimMissionData(mission);

                if (missionsManager.TryToActivateSurvey(mission))
                {
                    //UpdateUI();
                    updateUI = true;
                }

                if (serverClaimForCampaigns && CheckFullCampaignClaim(mission))
                {
                    ClaimReward(mission.campaignId);
                    RequestCampaigns();
                }

                if (!updateUI)
                    return;

                updateUIDelegate?.Invoke();


            }, mission);

        }

        //TODO: shouldn't have possibility to show video directly by game
        /*internal static void _PlayVideo(string videoPath, Action<bool> onComplete)
        {
            instance.uiController.PlayVideo(videoPath, onComplete);
        }*/


        public Sprite LoadSpriteFromCache(string campaignId, string assetUrl)
        {
            string fname = Path.GetFileName(assetUrl);
            string fpath = Application.persistentDataPath + "/" + campaignId + "/" + fname;

            if (!File.Exists(fpath))
                return null;

            byte[] data = File.ReadAllBytes(fpath);

            Texture2D tex = new Texture2D(0, 0);
            tex.LoadImage(data);
            tex.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);
        }

        /// <summary>
        /// Helper function to download and assign graphics assets
        /// </summary>
        private async Task AssignAssetTextures(ChallengeExtention ech, Challenge.Asset asset, AssetsType texture, AssetsType sprite, bool isOptional = false)
        {
            string path = Application.persistentDataPath + "/" + ech.challenge.id;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string fname = Path.GetFileName(asset.url);
            string fpath = path + "/" + fname;

            //Log.Print(fname);

            byte[] data = null;

            if (!File.Exists(fpath))
            {
                data = await DownloadHelper.DownloadAssetData(asset.url);

                if (data == null)
                {
                    if (!isOptional)
                        ech.isChallengeLoaded = false;

                    return;
                }

                File.WriteAllBytes(fpath, data);

                //Log.Print("saving: " + fpath);
            }
            else
            {
                data = File.ReadAllBytes(fpath);

                if (data == null)
                {
                    if (!isOptional)
                        ech.isChallengeLoaded = false;

                    return;
                }

                //Log.Print("reading: " + fpath);
            }

#if TEST_SLOW_LATENCY
            await Task.Delay(1000);
#endif

            Texture2D tex = new Texture2D(0, 0);
            tex.LoadImage(data);
            tex.wrapMode = TextureWrapMode.Clamp;

            if (texture != AssetsType.Unknown)
                ech.SetAsset<Texture2D>(texture, tex);

            if (sprite != AssetsType.Unknown)
            {
                Sprite s = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100.0f);

                ech.SetAsset<Sprite>(sprite, s);
            }

            ech.SetAssetUrl(sprite,asset.url);
        }

        private async Task PreloadAssetToCache(ChallengeExtention ech, Challenge.Asset asset, /*AssetsType urlString,*/ AssetsType fileString, bool required = true)
        {
            string path = Application.persistentDataPath + "/" + ech.challenge.id;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            string fname = Path.GetFileName(asset.url);
            string fpath = path + "/" + fname;
            string zipFolder = null;
            string fileToCheck = fpath;

            //Log.Print(fname);

            if (fname.Contains("zip"))
            {
                zipFolder = path + "/" + fname.Replace(".zip", "");
                fileToCheck = zipFolder + "/index.html";

                //Log.Print($"archive: {zipFolder} {fileToCheck} {File.Exists(fileToCheck)}");
            }

            byte[] data = null;

            if (!File.Exists(fileToCheck))
            {
                data = await DownloadHelper.DownloadAssetData(asset.url);

                if (data == null)
                {
                    if (required)
                        ech.isChallengeLoaded = false;

                    return;
                }

                File.WriteAllBytes(fpath, data);

                if (zipFolder != null)
                {
                    //Log.Print("extracting to: " + zipFolder);

                    if (Directory.Exists(zipFolder))
                        DeleteDirectory(zipFolder);

                    //if (!Directory.Exists(zipFolder))
                    Directory.CreateDirectory(zipFolder);

                    ZipFile.ExtractToDirectory(fpath, zipFolder);

                    File.Delete(fpath);
                }


                //Log.Print("saving: " + fpath);
            }

            if (zipFolder != null)
                fpath = fileToCheck;

            Log.Print("resource: " + fpath);

            //ech.SetAsset<string>(urlString, asset.url);
            ech.SetAsset<string>(fileString, fpath);
        }

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                //File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        /// <summary>
        /// Request challenges from the server
        /// </summary>
        public async void RequestChallenges(Action<bool> onRequestComplete)
        {
            List<Challenge> _challenges = new List<Challenge>();

            try
            {

                _challenges = await _challengesClient.GetList();
            }
            catch (Exception e)
            {
                Log.Print($"{MonetizrErrors.msg[ErrorType.ConnectionError]} {e}");
                onRequestComplete?.Invoke(false);
            }

            if (_challenges == null)
            {
                Log.Print($"{MonetizrErrors.msg[ErrorType.ConnectionError]}");
                onRequestComplete?.Invoke(false);
            }

            challengesId.Clear();

#if TEST_SLOW_LATENCY
            await Task.Delay(10000);
            Log.Print(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
#endif
            Color c;

            foreach (var ch in _challenges)
            {
                var ech = new ChallengeExtention(ch);
                            
                if (this.challenges.ContainsKey(ch.id))
                    continue;

                foreach (var asset in ch.assets)
                {
                    switch (asset.type)
                    {
                        case "icon":
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.BrandLogoSprite);

                            break;
                        case "banner":
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.BrandBannerSprite);

                            break;
                        case "logo":
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.BrandRewardLogoSprite);

                            break;
                        case "reward_banner":
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.BrandRewardBannerSprite);

                            break;

                        case "tiny_teaser":
                            await AssignAssetTextures(ech, asset, AssetsType.TinyTeaserTexture, AssetsType.Unknown);

                            break;

                        case "survey":
                            ech.SetAsset<string>(AssetsType.SurveyURLString, asset.url);

                            break;
                        case "video":
                            await PreloadAssetToCache(ech, asset, AssetsType.VideoFilePathString, true);

                            break;
                        case "text":
                            ech.SetAsset<string>(AssetsType.BrandTitleString, asset.title);

                            break;

                        case "html":
                            await PreloadAssetToCache(ech, asset, AssetsType.Html5PathString, false);

                            break;

                        case "campaign_text_color":

                            if (ColorUtility.TryParseHtmlString(asset.title, out c))
                                ech.SetAsset<Color>(AssetsType.CampaignTextColor, c);

                            break;

                        case "campaign_header_text_color":

                            if (ColorUtility.TryParseHtmlString(asset.title, out c))
                                ech.SetAsset<Color>(AssetsType.CampaignHeaderTextColor, c);

                            break;

                        case "header_text_color":

                            if (ColorUtility.TryParseHtmlString(asset.title, out c))
                                ech.SetAsset<Color>(AssetsType.HeaderTextColor, c);

                            break;

                        case "campaign_background_color":

                            if (ColorUtility.TryParseHtmlString(asset.title, out c))
                                ech.SetAsset<Color>(AssetsType.CampaignBackgroundColor, c);

                            break;

                        case "tiled_background":
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.TiledBackgroundSprite, true);

                            break;

                        case "custom_coin_title":
                            ech.SetAsset<string>(AssetsType.CustomCoinString, asset.title);

                            break;

                        case "custom_coin_icon":
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.CustomCoinSprite, true);

                            break;

                        case "loading_screen":
                            
                            await AssignAssetTextures(ech, asset, AssetsType.Unknown, AssetsType.LoadingScreenSprite, true);

                            break;

                    }

                }


                //TODO: check if all resources available

                /*if (!ech.HasAsset(AssetsType.VideoFilePathString) && !ech.HasAsset(AssetsType.Html5PathString))
                {
                    Log.Print($"ERROR: Campaign {ch.id} has neither video, nor html5 asset");
                    ech.isChallengeLoaded = false;
                }*/

                if(ech.HasAsset(AssetsType.SurveyURLString) && ech.GetAsset<string>(AssetsType.SurveyURLString).Length == 0)
                {
                    Log.Print($"ERROR: Campaign {ch.id} has survey asset, but url is empty");
                    ech.isChallengeLoaded = false;
                }

                if (ech.isChallengeLoaded)
                {
                    
                    this.challenges.Add(ch.id, ech);
                    challengesId.Add(ch.id);
                }
            }

            activeChallengeId = challengesId.Count > 0 ? challengesId[0] : null;

            isMissionsIsOudated = true;

#if TEST_SLOW_LATENCY
            Log.Print(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
#endif

            Log.Print($"RequestChallenges completed with count: {_challenges.Count} {challengesId.Count} active: {activeChallengeId}");

            //Ok, even if response empty
            onRequestComplete?.Invoke(/*challengesId.Count > 0*/true);
        }

        /// <summary>
        /// Get Challenge by Id
        /// TODO: Don't give access to challenge itself, update progress internally
        /// </summary>
        /// <returns></returns>
        [Obsolete("This Method is obsolete and don't recommended for use")]
        internal Challenge GetChallenge(String chId)
        {
            return challenges[chId].challenge;
        }

        /// <summary>
        /// Get list of the available challenges
        /// </summary>
        /// <returns></returns>
        public List<string> GetAvailableChallenges()
        {
            return challengesId;
        }

        public bool HasChallengesAndActive()
        {
            return isActive && challengesId.Count > 0;
        }

        public string GetActiveChallenge()
        {
            return activeChallengeId;
        }
            
        public void SetActiveChallengeId(string id)
        {
            activeChallengeId = id;
        }

        public void Enable(bool enable)
        {
            isActive = enable;
        }

        /// <summary>
        /// Get Asset from the challenge
        /// </summary>
        public T GetAsset<T>(String challengeId, AssetsType t)
        {
            if(challengeId == null)
            {
                Log.Print($"You requesting asset for empty challenge.");
                return default(T);
            }

            if(!challenges.ContainsKey(challengeId))
            {
                Log.Print($"You requesting asset for challenge {challengeId} that not exist!");
                return default(T);
            }

            if(!HasAsset(challengeId,t))
            {
                Log.Print($"{challengeId} has no asset {t}");
                return default(T);
            }

            return challenges[challengeId].GetAsset<T>(t);
        }

        public string GetAssetUrl(String challengeId, AssetsType t)
        {
            return challenges[challengeId].GetAssetUrl(t);
        }

        public bool HasCampaign(String challengeId)
        {
            return challenges.ContainsKey(challengeId);
        }

        public bool HasAsset(String challengeId, AssetsType t)
        {            
            return challenges[challengeId].HasAsset(t);
        }

        /// <summary>
        /// Single update for reward and claim
        /// </summary>
        public async void ClaimReward(String challengeId)
        {
            var challenge = challenges[challengeId].challenge;

            try
            {
                await _challengesClient.Claim(challenge);
            }
            catch (Exception e)
            {
                Log.Print($"An error occured: {e.Message}");
            }
        }
               
    }

}