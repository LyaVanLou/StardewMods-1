﻿using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FelixDev.StardewMods.FeTK.Framework.Services
{
    /// <summary>
    /// Provides an API to add mails to the player's mailbox.
    /// </summary>
    public interface IMailService
    {
        /// <summary>
        /// Raised when a mail begins to open. The mail content can still be changed at this point.
        /// </summary>
        event EventHandler<MailOpeningEventArgs> MailOpening;

        /// <summary>
        /// Raised when a mail has been closed.
        /// </summary>
        event EventHandler<MailClosedEventArgs> MailClosed;

        /// <summary>
        /// Add a mail to the player's mailbox.
        /// </summary>
        /// <param name="daysFromNow">The day offset when the mail will arrive in the mailbox.</param>
        /// <param name="id">The ID of the mail.</param>
        /// <param name="content">The mail content.</param>
        /// <param name="attachedItem">The mail's attached item. Can be <c>null</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="daysFromNow"/> is less than or equal to <c>0</c>.</exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="id"/> is <c>null</c> or does not contain at least one non-whitespace character -or-
        /// a mail with the specified <paramref name="id"/> already exists for the day specified by <paramref name="daysFromNow"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">The <paramref name="content"/> is be <c>null</c>.</exception>
        void AddMail(int daysFromNow, string id, string content, Item attachedItem = null);

        /// <summary>
        /// Add a mail to the player's mailbox.
        /// </summary>
        /// <param name="arrivalDay">The day when the mail will arrive in the mailbox.</param>
        /// <param name="id">The ID of the mail.</param>
        /// <param name="content">The mail content.</param>
        /// <param name="attachedItem">The mail's attached item. Can be <c>null</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="arrivalDay"/> is in the past.</exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="id"/> is <c>null</c> or does not contain at least one non-whitespace character -or-
        /// a mail with the specified <paramref name="id"/> already exists for the specified <paramref name="arrivalDay"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">The <paramref name="content"/> is be <c>null</c>.</exception>
        void AddMail(SDate arrivalDay, string id, string content, Item attachedItem = null);

        /// <summary>
        /// Add a mail to the player's mailbox.
        /// </summary>
        /// <param name="daysFromNow">The day offset when the mail will arrive in the mailbox.</param>
        /// <param name="id">The ID of the mail.</param>
        /// <param name="content">The mail content.</param>
        /// <param name="attachedItems">The mail's attached items. Can be <c>null</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="daysFromNow"/> has to be greater than or equal to <c>0</c>.</exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="id"/> is <c>null</c> or does not contain at least one non-whitespace character -or-
        /// a mail with the specified <paramref name="id"/> already exists for the day specified by <paramref name="daysFromNow"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">The <paramref name="content"/> is <c>null</c>.</exception>
        void AddMail(int daysFromNow, string id, string content, List<Item> attachedItems);

        /// <summary>
        /// Add a mail to the player's mailbox.
        /// </summary>
        /// <param name="arrivalDay">The day when the mail will arrive in the mailbox.</param>
        /// <param name="id">The ID of the mail.</param>
        /// <param name="content">The mail content.</param>
        /// <param name="attachedItems">The mail's attached items. Can be <c>null</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">The <paramref name="arrivalDay"/> is in the past.</exception>
        /// <exception cref="ArgumentException">
        /// The <paramref name="id"/> is <c>null</c> or does not contain at least one non-whitespace character -or-
        /// a mail with the specified <paramref name="id"/> already exists for the specified <paramref name="arrivalDay"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">The <paramref name="content"/> is be <c>null</c>.</exception>
        /// </exception>
        void AddMail(SDate arrivalDay, string id, string content, List<Item> attachedItems);

        /// <summary>
        /// Determine if a mail added by this mail service already exists for a day.
        /// </summary>
        /// <param name="day">The day to check for.</param>
        /// <param name="mailId">The ID of the mail.</param>
        /// <returns>
        /// <c>true</c> if a mail with the specified <paramref name="mailId"/> has already been added for the specified <paramref name="day"/>;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">The specified <paramref name="day"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// The specified <paramref name="mailId"/> is <c>null</c> or does not contain at least one 
        /// non-whitespace character.
        /// </exception>
        bool HasMailForDay(SDate day, string mailId);

        /// <summary>
        /// Determine if a mail with the given <paramref name="mailId"/> added by this mail service is currently in the mailbox.
        /// </summary>
        /// <param name="mailId">The ID of the mail.</param>
        /// <returns><c>true</c> if a mail with the specified <paramref name="mailId"/> has already been registered and 
        /// is currently in the mailbox; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentException">
        /// The specified <paramref name="mailId"/> is <c>null</c> or does not contain at least one 
        /// non-whitespace character.
        /// </exception>
        bool HasRegisteredMailInMailbox(string mailId);
    }
}