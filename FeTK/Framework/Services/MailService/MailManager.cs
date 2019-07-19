﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FelixDev.StardewMods.FeTK.Framework.Helpers;
using FelixDev.StardewMods.FeTK.Framework.UI;
using FelixDev.StardewMods.FeTK.ModHelpers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace FelixDev.StardewMods.FeTK.Framework.Services
{
    internal class MailManager : IMailManager
    {
        private const string MAIL_ID_SEPARATOR = "@@@";

        /// <summary>The prefix of the key used to identify the save data created by this mail manager.</summary>
        private const string SAVE_DATA_KEY = "FelixDev.StardewMods.FeTK.Framework.Services.MailManagerCore";

        /// <summary>Provides access to the <see cref="IModEvents"/> API provided by SMAPI.</summary>
        private static readonly IModEvents events = ToolkitMod.ModHelper.Events;

        /// <summary>Provides access to the <see cref="IMonitor"/> API provided by SMAPI.</summary>
        private static readonly IMonitor monitor = ToolkitMod._Monitor;

        /// <summary>Provides access to the <see cref="IReflectionHelper"/> API provided by SMAPI.</summary>
        private static readonly IReflectionHelper reflectionHelper = ToolkitMod.ModHelper.Reflection;

        private readonly MailAssetEditor mailAssetEditor;

        /// <summary>The save data manager for this mail manager.</summary>
        private readonly ModSaveDataHelper saveDataHelper;

        private readonly IDictionary<string, IMailSender> mailSenders = new Dictionary<string, IMailSender>();

        private IDictionary<int, IList<string>> registeredMailsForDay = new Dictionary<int, IList<string>>();

        private IDictionary<string, MailMetaData> registeredMailsMetaData = new Dictionary<string, MailMetaData>();

        /// <summary>
        /// Create a new instance of the <see cref="MailManager"/> class.
        /// </summary>
        public MailManager()
        {
            this.mailAssetEditor = new MailAssetEditor();
            this.saveDataHelper = ModSaveDataHelper.GetSaveDataHelper();

            mailAssetEditor.MailAssetLoading += OnMailDataLoading;

            events.GameLoop.DayStarted += OnDayStarted;
            events.GameLoop.DayEnding += OnDayEnding;

            events.Display.MenuChanged += OnMenuChanged;

            events.GameLoop.Saving += OnSaving;
            events.GameLoop.SaveLoaded += OnSaveLoaded;
        }

        /// <summary>
        /// Add a mail to the game.
        /// </summary>
        /// <param name="modId">The ID of the mod which wants to add the mail.</param>
        /// <param name="mailId">The ID of the mail.</param>
        /// <param name="arrivalDay">The day of arrival of the mail.</param>
        /// <exception cref="ArgumentException">
        /// The specified <paramref name="modId"/> is <c>null</c>, does not contain at least one 
        /// non-whitespace character or contains an invalid character sequence -or-
        /// the specified <paramref name="mailId"/> is <c>null</c>, does not contain at least one 
        /// non-whitespace character or contains an invalid character sequence -or-
        /// a mail with the specified <paramref name="mailId"/> provided by the mod with the specified <paramref name="modId"/> 
        /// for the specified <paramref name="arrivalDay"/> already exists.
        /// </exception>
        /// <exception cref="ArgumentNullException">The specified <paramref name="arrivalDay"/> is <c>null</c>.</exception>
        public void Add(string modId, string mailId, SDate arrivalDay)
        {
            if (string.IsNullOrWhiteSpace(modId) || modId.Contains(MAIL_ID_SEPARATOR))
            {
                throw new ArgumentException($"The mod ID \"{modId}\" has to contain at least one non-whitespace character and cannot " +
                    $"contain the string {MAIL_ID_SEPARATOR}", nameof(modId));
            }

            if (mailId.Contains(MAIL_ID_SEPARATOR) || mailId.Contains(MAIL_ID_SEPARATOR))
            {
                throw new ArgumentException($"The mail ID \"{mailId}\" has to contain at least one non-whitespace character and cannot " +
                    $"contain the string {MAIL_ID_SEPARATOR}", nameof(mailId));
            }

            if (arrivalDay == null)
            {
                throw new ArgumentNullException(nameof(arrivalDay));
            }

            /*
             * Components for the internal mail ID: MOD_ID + user ID + Arrival Day.
             * 
             * Multiple mods can add mails with the same IDs for the same day, so in order to have
             * a straightforward relation between mail and the mod which added it, we need to add the mod ID 
             * to the internal mail ID.
             * 
             * We also add the arrival day to the internal mail ID because for each mod, mails with the 
             * same ID for different arrival days can be added. The user cannot, however, have multiple mails 
             * with the same ID for the same day for the same mod.
             */
            int absoluteArrivalDay = arrivalDay.DaysSinceStart;
            string internalMailId = modId + MAIL_ID_SEPARATOR + mailId + MAIL_ID_SEPARATOR + absoluteArrivalDay;

            if (registeredMailsMetaData.ContainsKey(internalMailId))
            {
                throw new ArgumentException($"A mail with the specified ID \"{mailId}\" for the given mod \"{modId}\" for the " +
                    $"specified arrival day \"{arrivalDay}\" already exists!");
            }

            registeredMailsMetaData[internalMailId] = new MailMetaData(modId, mailId, absoluteArrivalDay);

            if (!registeredMailsForDay.ContainsKey(absoluteArrivalDay))
            {
                registeredMailsForDay[absoluteArrivalDay] = new List<string>();
            }
            registeredMailsForDay[absoluteArrivalDay].Add(internalMailId);

            if (arrivalDay.Equals(SDate.Now()))
            {
                Game1.mailbox.Add(internalMailId);

                mailAssetEditor.RequestAssetCacheRefresh();
                monitor.Log($"Added the mail with ID \"{mailId}\" to the player's mailbox.");
            }
        }

        /// <summary>
        /// Register a mail sender with the mail manager.
        /// </summary>
        /// <param name="modId">The ID of the mod using the specified mail sender.</param>
        /// <param name="mailSender">The <see cref="IMailSender"/> instance to register.</param>
        /// <exception cref="ArgumentException">
        /// The specified <paramref name="modId"/> is <c>null</c> or does not contain at least one 
        /// non-whitespace character -or-
        /// a mail sender with the specified <paramref name="modId"/> has already been registered.
        /// </exception>
        /// <exception cref="ArgumentNullException">The specified <paramref name="mailSender"/> is <c>null</c>.</exception>
        public void RegisterMailSender(string modId, IMailSender mailSender)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("The mod ID needs to contain at least one non-whitespace character!", nameof(modId));
            }

            if (mailSenders.ContainsKey(modId))
            {
                throw new ArgumentException($"A mail sender for the mod with ID \"{modId}\" has already been registered.", nameof(modId));
            }

            mailSenders[modId] = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
        }

        /// <summary>
        /// Determine whether the player's mailbox contains the specified mail.
        /// </summary>
        /// <param name="modId">The ID of the mod which created this mail.</param>
        /// <param name="mailId">The ID of the mail.</param>
        /// <returns>
        /// <c>true</c> if a mail with the specified <paramref name="mailId"/> created by the mod with the 
        /// specified <paramref name="modId"/> is in the player's mailbox; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// The specified <paramref name="modId"/> is <c>null</c> or does not contain at least one 
        /// non-whitespace character -or-
        /// the specified <paramref name="mailId"/> is <c>null</c> or does not contain at least one 
        /// non-whitespace character.
        /// </exception>
        public bool HasMailInMailbox(string modId, string mailId)
        {
            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("The mod ID needs to contain at least one non-whitespace character!", nameof(modId));
            }

            if (string.IsNullOrWhiteSpace(mailId))
            {
                throw new ArgumentException("The mail ID needs to contain at least one non-whitespace character!", nameof(mailId));
            }

            return Game1.mailbox.Any(s => s.StartsWith(modId + MAIL_ID_SEPARATOR + mailId));
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (!(e.OldMenu is LetterViewerMenu) && e.NewMenu is LetterViewerMenu letterMenu)
            {
                var mailId = reflectionHelper.GetField<string>(letterMenu, "mailTitle").GetValue();

                // If the opened LetterViewerMenu instance does not represent a mail -> do nothing
                if (mailId == null)
                {
                    return;
                }

                // If the opened mail is not a mail registered via the framework, we still parse the mail content
                // for the framework's text coloring API and provide the item selection fix (only showing the last 
                // item.
                if (!this.registeredMailsMetaData.TryGetValue(mailId, out MailMetaData mailMetaData))
                {
                    string mailContent = GetContentForGameMail(mailId);

                    // Create and show the menu for this mail.
                    var gLetterMenu = LetterViewerMenuWrapper.CreateMenuForGameMail(mailId, mailContent);
                    gLetterMenu.Show();

                    return;
                }

                // If a mail with the given ID has been registered in the framework, but no mail sender has been found,
                // we remove the mail ID from the framework and don't proceed further.
                if (!this.mailSenders.TryGetValue(mailMetaData.ModId, out IMailSender mailSender))
                {
                    // A mail with this mailId was added to the framework at some point, but there is no sender
                    // owning this mail any longer. This can be due to the removal of a mod consuming the mail API of FeTK
                    // by the user. We can thus savely remove this mail from the framework on saving, as even if the consuming
                    // mod will be added back, for this save, the mail won't be displayed any longer (because it was already shown).
                    this.registeredMailsMetaData.Remove(mailId);
                    this.registeredMailsForDay[mailMetaData.ArrivalDay].Remove(mailId);

                    monitor.Log($"The mail \"{mailMetaData.UserId}\" was added by the mod {mailMetaData.ModId} which seems to be no longer present.");
                    return;
                }

                // Request the actual mail data from the mail service which was used to add this mail to the game.
                var arrivalDate = SDateHelper.GetDateFromDay(mailMetaData.ArrivalDay);
                var mail = mailSender.GetMailFromId(mailMetaData.UserId, arrivalDate);
                if (mail == null)
                {
                    monitor.Log($"An unexpected error occured. The mail \"{mailId}\" could not be retrieved from the mail service it was registered with.");
                    return;
                }

                // Raise the mail-opening event for this mail.
                mailSender.OnMailOpening(new MailOpeningEventArgs(mail));

                // Create the menu for this mail.
                var fLetterMenu = LetterViewerMenuWrapper.CreateMenuForFrameworkMail(mailId, mail.Content, mail.AttachedItems);

                // Setup the mail-closed event for this mail.
                fLetterMenu.MenuClosed += (s, e2) =>
                {
                    // Remove the closed mail from the mail manager.
                    RemoveMail(mailId, mailMetaData.ArrivalDay);

                    // Notify its sender that the mail has been read.
                    mailSender.OnMailClosed(new MailClosedCoreEventArgs(mailMetaData.UserId, arrivalDate, e2.SelectedItems));
                };

                monitor.Log($"Opening custom mail with the ID \"{mailMetaData.UserId}\".");

                // Show the menu for this mail.
                fLetterMenu.Show();
            }
        }

        /// <summary>
        /// Remove the specified mail from the mail manager.
        /// </summary>
        /// <param name="mailId">The ID of the mail.</param>
        /// <param name="arrivalDay">The arrival day of the mail.</param>
        private void RemoveMail(string mailId, int arrivalDay)
        {
            registeredMailsMetaData.Remove(mailId);

            registeredMailsForDay[arrivalDay].Remove(mailId);
            if (registeredMailsForDay[arrivalDay].Count == 0)
            {
                registeredMailsForDay.Remove(arrivalDay);
            }

            // When testing in Multiplayer, it was noticed that apparently for non-host players,
            // already seen mails won't be removed from their [mailForTomorrow] list. This resulted
            // in adding already seen mails to the mailbox again and again. This caused "zombie" mails 
            // where the players's mailbox would indicate a mail, but nothing was shown for that mail (because its 
            // ID was no longer in the system).
            // Clearing the [mailForTomorrow] list manually will prevent the above described "zombie" mails.
            if (!Context.IsMainPlayer && Game1.player.mailForTomorrow.Contains(mailId))
            {
                Game1.player.mailForTomorrow.Remove(mailId);
            }
        }

        /// <summary>
        /// Inject the registered mails into the game's cached mail asset list.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnMailDataLoading(object sender, MailAssetLoadingEventArgs e)
        {
            var currentDay = SDate.Now().DaysSinceStart;
            List<MailAssetDataEntry> customMailData = new List<MailAssetDataEntry>();

            foreach (var day in registeredMailsForDay.Keys)
            {
                if (day > currentDay)
                {
                    // The list of keys is not guaranteed to be sorted from [earlier] to [later], 
                    // so we have to iterate through all entries. 
                    continue;
                }

                customMailData.AddRange(registeredMailsForDay[day].Select(mailId => new MailAssetDataEntry(mailId, "PlaceholderContent")));
            }

            mailAssetEditor.AddMailAssetData(customMailData);
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Update the loaded mail game asset to include the IDs of our custom mails
            // which are in the player's mailbox for today.
            mailAssetEditor.RequestAssetCacheRefresh();
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            var nextDay = SDate.Now().AddDays(1).DaysSinceStart;

            if (!registeredMailsForDay.TryGetValue(nextDay, out IList<string> mailIdsForDay))
            {
                return;
            }

            foreach (var mailId in mailIdsForDay)
            {
                Game1.addMailForTomorrow(mailId);

                // TODO: comment why we need no checks here (or put differently, why we will let exceptions surface)
                var userId = registeredMailsMetaData[mailId].UserId;
                monitor.Log($"Added the mail with ID \"{userId}\" to tomorrow's inbox.");
            }
        }

        private void OnSaving(object sender, SavingEventArgs e)
        {
            saveDataHelper.WriteData(SAVE_DATA_KEY, new SaveData(registeredMailsForDay, registeredMailsMetaData));
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            var saveData = saveDataHelper.ReadData<SaveData>(SAVE_DATA_KEY);
            if (saveData != null)
            {
                this.registeredMailsForDay = saveData.MailPerDay;
                this.registeredMailsMetaData = saveData.MailMetaData;
            }
            else
            {
                this.registeredMailsForDay = new Dictionary<int, IList<string>>();
                this.registeredMailsMetaData = new Dictionary<string, MailMetaData>();
            }
        }

        /// <summary>
        /// Get the content for a game mail.
        /// </summary>
        /// <param name="mailId">The mail ID.</param>
        /// <returns>The content of the mail.</returns>
        /// <remarks>Code copied over from <see cref="GameLocation.mailbox"/>.</remarks>
        private string GetContentForGameMail(string mailId)
        {
            Dictionary<string, string> dictionary = Game1.content.Load<Dictionary<string, string>>("Data\\mail");
            string str = dictionary.ContainsKey(mailId) ? dictionary[mailId] : "";

            if (mailId.Contains("passedOut"))
            {
                int int32 = Convert.ToInt32(mailId.Split(' ')[1]);
                switch (new Random(int32).Next(Game1.player.getSpouse() == null || !Game1.player.getSpouse().Name.Equals("Harvey") ? 3 : 2))
                {
                    case 0:
                        str = string.Format(dictionary["passedOut1_" + (int32 > 0 ? "Billed" : "NotBilled") + "_" + (Game1.player.IsMale ? "Male" : "Female")], (object)int32);
                        break;
                    case 1:
                        str = string.Format(dictionary["passedOut2"], (object)int32);
                        break;
                    case 2:
                        str = string.Format(dictionary["passedOut3_" + (int32 > 0 ? "Billed" : "NotBilled")], (object)int32);
                        break;
                }
            }

            string mail = str.Replace("@", Game1.player.Name);

            if (mail.Contains("%update"))
            {
                mail = mail.Replace("%update", Utility.getStardewHeroStandingsString());
            }

            return mail;
        }

        private class MailMetaData
        {
            public MailMetaData(string modId, string userId, int arrivalDay)
            {
                ModId = modId;
                UserId = userId;
                ArrivalDay = arrivalDay;
            }

            public string ModId { get; }

            public string UserId { get; }

            public int ArrivalDay { get; }
        }

        private class SaveData
        {
            public SaveData() { }

            public SaveData(IDictionary<int, IList<string>> mailPerDay, IDictionary<string, MailMetaData> mailMetaData)
            {
                MailPerDay = mailPerDay;
                MailMetaData = mailMetaData;
            }

            public IDictionary<int, IList<string>> MailPerDay { get; set; }

            public IDictionary<string, MailMetaData> MailMetaData { get; set; }
        }
    }
}