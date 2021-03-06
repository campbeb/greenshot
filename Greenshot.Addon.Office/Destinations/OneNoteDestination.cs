﻿/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2016 Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: http://getgreenshot.org/
 * The Greenshot project is hosted on GitHub: https://github.com/greenshot
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Greenshot.Addon.Configuration;
using Greenshot.Addon.Core;
using Greenshot.Addon.Extensions;
using Greenshot.Addon.Interfaces;
using Greenshot.Addon.Interfaces.Destination;
using Greenshot.Addon.Office.OfficeExport;

namespace Greenshot.Addon.Office.Destinations
{
	/// <summary>
	/// Description of OneNoteDestination.
	/// </summary>
	[Destination(OneNoteDesignation), PartNotDiscoverable]
	public sealed class OneNoteDestination : AbstractDestination
	{
		public const string OneNoteDesignation = "OneNote";
		private static readonly Serilog.ILogger Log = Serilog.Log.Logger.ForContext(typeof(OneNoteDestination));
		private static readonly BitmapSource ApplicationIcon;
		
		static OneNoteDestination()
		{
			var exePath = PluginUtils.GetExePath("ONENOTE.EXE");
			if (exePath != null && File.Exists(exePath))
			{
				WindowDetails.AddProcessToExcludeFromFreeze("onenote");
				ApplicationIcon = PluginUtils.GetCachedExeIcon(exePath, 0).ToBitmapSource();
				IsActive = true;
			}
		}

		/// <summary>
		/// Tells if the destination can be used
		/// </summary>
		public static bool IsActive
		{
			get;
			private set;
		}

		[Import]
		private IOfficeConfiguration OfficeConfiguration
		{
			get;
			set;
		}

		[Import]
		private IGreenshotLanguage GreenshotLanguage
		{
			get;
			set;
		}

		protected override void Initialize()
		{
			base.Initialize();
			Export = async (exportContext, capture, token) => await ExportCaptureAsync(capture, null);
			Text = Text = $"Export to {OneNoteDesignation}";
			Designation = OneNoteDesignation;
			Icon = ApplicationIcon;
		}

		/// <summary>
		/// Load the current documents to export to
		/// </summary>
		/// <param name="caller1"></param>
		/// <param name="token"></param>
		/// <returns>Task</returns>
		public override async Task RefreshAsync(IExportContext caller1, CancellationToken token = default(CancellationToken))
		{
			await Task.Factory.StartNew(
				// this will use current synchronization context
				() =>
				{
					Children.Clear();
					foreach (var page in OneNoteExporter.GetPages())
					{
						var oneNoteDestination = new OneNoteDestination
						{
							Icon = ApplicationIcon,
							Export = async (caller, capture, exportToken) => await ExportCaptureAsync(capture, page),
							Text = page.DisplayName,
							OfficeConfiguration = OfficeConfiguration,
							GreenshotLanguage = GreenshotLanguage
						};
						Children.Add(oneNoteDestination);
					}
				},
				token,
				TaskCreationOptions.None,
				TaskScheduler.FromCurrentSynchronizationContext()
			);
		}

		private Task<INotification> ExportCaptureAsync(ICapture capture, OneNotePage page)
		{
			INotification returnValue = new Notification
			{
				NotificationType = NotificationTypes.Success,
				Source = OneNoteDesignation,
				SourceType = SourceTypes.Destination,
				Text = $"Exported to {OneNoteDesignation}"
			};
			try
			{
				if (page == null)
				{
					OneNoteExporter.ExportToNewPage(capture);
				}
				else
				{
					OneNoteExporter.ExportToPage(capture, page);
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "OneNote export failed");
				returnValue.NotificationType = NotificationTypes.Fail;
				returnValue.ErrorText = ex.Message;
				returnValue.Text = string.Format(GreenshotLanguage.DestinationExportFailed, OneNoteDesignation);
				return Task.FromResult(returnValue);
			}
			return Task.FromResult(returnValue);
		}
	}
}