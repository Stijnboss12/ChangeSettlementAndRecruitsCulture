using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ChangeSettlementCulture
{
    public class ChangeSettlementCultureBehavior : CampaignBehaviorBase
    {
        List<SettlementChangeTimer> SettlementChangeTimers = new List<SettlementChangeTimer>();

        Settings Settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText($@"../../Modules/ChangeSettlementCulture/Settings.json"));

        string SaveFilePath = "";

        public override void SyncData(IDataStore dataStore)
        {

        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener((object)this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(this.OnGameLoaded));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener((object)this, new Action(this.OnDayPassed));
            CampaignEvents.OnSaveStartedEvent.AddNonSerializedListener((object)this, new Action(this.OnSave));
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener((object)this, new Action(this.OnBeforeSave));
            CampaignEvents.OnSaveOverEvent.AddNonSerializedListener((object)this, new Action<bool, string>(this.OnAfterSave));
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(this.OnNewGameStart));
        }

        public void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (!Campaign.Current.GameStarted)
            {
                return;
            }

            StartTimer(settlement);
        }

        public void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            if (!Campaign.Current.GameStarted || Campaign.Current.Settlements == null)
            {
                return;
            }

            SaveFilePath = $@"../../Modules/ChangeSettlementCulture/saves/SettlementChangeTimers-{Campaign.Current.UniqueGameId}.json";

            ResetNotables();

            if (!File.Exists(SaveFilePath))
            {
                foreach (Settlement settlement in Campaign.Current.Settlements.Where(x => x.IsTown || x.IsCastle))
                {
                    StartTimer(settlement);
                }

                return;
            }

            string json = File.ReadAllText(SaveFilePath);
            SettlementChangeTimers = JsonConvert.DeserializeObject<List<SettlementChangeTimer>>(json);

            SetCultureOnLoad();
        }

        public void OnDayPassed()
        {
            if (!Campaign.Current.GameStarted || Campaign.Current.Settlements == null)
            {
                return;
            }

            CheckAndChangeSettlements();
        }

        public void OnSave()
        {
            string json = JsonConvert.SerializeObject(SettlementChangeTimers);
            File.WriteAllText(SaveFilePath, json);
        }

        public void OnBeforeSave()
        {
            ResetNotables();
        }

        public void OnAfterSave(bool saveOver, string saveName)
        {
            if (saveOver)
            {
                File.WriteAllText($@"../../Modules/ChangeSettlementCulture/saves/SettlementChangeTimers-{saveName}.txt", SaveFilePath);
            }

            SetNotablesAfterSave();
        }

        public void OnNewGameStart(CampaignGameStarter campaignGameStarter)
        {
            if (!Campaign.Current.GameStarted || Campaign.Current.Settlements == null)
            {
                return;
            }

            if (!File.Exists($@"../../Modules/ChangeSettlementCulture/saves/SettlementChangeTimers-{Campaign.Current.UniqueGameId}.json"))
            {
                foreach (Settlement settlement in Campaign.Current.Settlements.Where(x => x.IsTown || x.IsCastle))
                {
                    StartTimer(settlement);
                }

                return;
            }

            string json = File.ReadAllText($@"../../Modules/ChangeSettlementCulture/saves/SettlementChangeTimers-{Campaign.Current.UniqueGameId}.json");
            SettlementChangeTimers = JsonConvert.DeserializeObject<List<SettlementChangeTimer>>(json);

            SetCultureOnLoad();
        }

        private void ResetNotables()
        {
            foreach (Settlement settlement in Campaign.Current.Settlements.Where(x => x.IsTown || x.IsCastle || x.IsVillage))
            {
                foreach (Hero notable in settlement.Notables)
                {
                    if (notable.Culture == notable.CharacterObject.OriginalCharacter.Culture)
                    {
                        continue;
                    }

                    notable.Culture = notable.CharacterObject.OriginalCharacter.Culture;
                }
            }
        }

        private void CheckAndChangeSettlements()
        {
            foreach (SettlementChangeTimer settlementChangeTimer in SettlementChangeTimers)
            {
                if (!CheckChangeSettlementCulture(settlementChangeTimer))
                {
                    settlementChangeTimer.DaysSinceOwnerChanged++;
                    continue;
                }

                ChangeSettlementCulture(settlementChangeTimer.SettlementId, false);
            }
        }

        private void SetCultureOnLoad()
        {
            foreach (SettlementChangeTimer settlementChangeTimer in SettlementChangeTimers)
            {
                if (!CheckChangeSettlementCulture(settlementChangeTimer))
                {
                    continue;
                }

                ChangeSettlementCulture(settlementChangeTimer.SettlementId, true);
            }
        }

        private void SetNotablesAfterSave()
        {
            if (!Settings.ConvertRecruitableTroops)
            {
                return;
            }

            foreach (SettlementChangeTimer settlementChangeTimer in SettlementChangeTimers)
            {
                if (!CheckChangeSettlementCulture(settlementChangeTimer))
                {
                    continue;
                }

                ChangeSettlementNotablesCulture(settlementChangeTimer.SettlementId);
            }
        }

        private void StartTimer(Settlement settlement)
        {
            SettlementChangeTimer existingTimer = SettlementChangeTimers.Where(x => x.SettlementId == settlement.Id.InternalValue).FirstOrDefault();

            if (existingTimer != null)
            {
                existingTimer.DaysSinceOwnerChanged = 0;
                return;
            }

            SettlementChangeTimer newTimer = new SettlementChangeTimer(settlement.Id.InternalValue);

            SettlementChangeTimers.Add(newTimer);

            InformationManager.DisplayMessage(new InformationMessage($"{settlement.Name} Will be converted to {settlement.Owner.Culture.Name} in {Settings.TimeToConvertInDays + 1} days."));
        }

        private bool CheckChangeSettlementCulture(SettlementChangeTimer settlementChangeTimer)
        {
            return settlementChangeTimer.DaysSinceOwnerChanged >= Settings.TimeToConvertInDays;
        }

        private void ChangeSettlementCulture(uint settlementId, bool calledByOnGameLoad)
        {
            Settlement settlement = Campaign.Current.Settlements.Where(x => x.Id.InternalValue == settlementId).FirstOrDefault();

            if (!(settlement.IsTown || settlement.IsCastle))
            {
                return;
            }

            var ownerCulture = settlement.Owner.Culture ?? settlement.OwnerClan.Culture;

            if (settlement.Culture == ownerCulture)
            {
                return;
            }

            // Do not convert the last remaining town of a culture. Companions need a place to spawn or there will be crashes
            if (settlement.IsTown)
            {
                var remainingTowns = Campaign.Current.Settlements.Where(s => s.IsTown && s.Culture == settlement.Culture).Count();
                if (remainingTowns == 1)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"{settlement.Name} can't be converted to {settlement.Owner.Culture.Name} becuase it is the last Town of {settlement.Culture} culture."));
                    return;
                }
            }

            settlement.Culture = ownerCulture;

            if (Settings.ConvertRecruitableTroops)
            {
                foreach (Village village in settlement.BoundVillages)
                {
                    if (village.Settlement.Culture == ownerCulture)
                    {
                        continue;
                    }

                    village.Settlement.Culture = ownerCulture;

                    foreach (Hero notable in village.Settlement.Notables)
                    {
                        if (notable.Culture == ownerCulture)
                        {
                            continue;
                        }

                        notable.Culture = ownerCulture;
                    }
                }

                foreach (Hero notable in settlement.Notables)
                {
                    if (notable.Culture == ownerCulture)
                    {
                        continue;
                    }

                    notable.Culture = ownerCulture;
                }
            }

            if (!calledByOnGameLoad)
            {
                InformationManager.DisplayMessage(new InformationMessage($"{settlement.Name}'s culture is converted to {settlement.Owner.Culture.Name}."));
            }
        }

        public void ChangeSettlementNotablesCulture(uint settlementId)
        {
            if (Campaign.Current is null)
            {
                return;
            }

            Settlement settlement = Campaign.Current.Settlements.Where(x => x.Id.InternalValue == settlementId).FirstOrDefault();

            if (!(settlement.IsTown || settlement.IsCastle))
            {
                return;
            }

            foreach (Village village in settlement.BoundVillages)
            {
                village.Settlement.Culture = settlement.Culture;

                foreach (Hero notable in village.Settlement.Notables)
                {
                    if (notable.Culture == settlement.Culture)
                    {
                        continue;
                    }

                    notable.Culture = settlement.Culture;
                }
            }

            foreach (Hero notable in settlement.Notables)
            {
                if (notable.Culture == settlement.Culture)
                {
                    continue;
                }

                notable.Culture = settlement.Culture;
            }
        }
    }
}