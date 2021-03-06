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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Greenshot.Windows;
using System.Threading;
using Greenshot.Addon.Configuration;
using Greenshot.Addon.Interfaces;
using Greenshot.Addon.Interfaces.Destination;


namespace Greenshot.Destinations
{
	/// <summary>
	/// The PickerDestination shows a context menu with all possible destinations, so the user can "pick" one
	/// </summary>
	[Destination(PickerDesignation)]
	public sealed class PickerDestination : AbstractDestination
	{
		private const string PickerDesignation = "Picker";
		private static readonly Serilog.ILogger Log = Serilog.Log.Logger.ForContext(typeof(PickerDestination));

		[Import]
		private ICoreConfiguration CoreConfiguration
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

		[Import]
		private ExportFactory<ExportWindow> ExportWindowFactory
		{
			get;
			set;
		} 

		[ImportMany(AllowRecomposition = true)]
		private IEnumerable<Lazy<IDestination, IDestinationMetadata>> Destinations
		{
			get;
			set;
		}

		protected override void Initialize()
		{
			base.Initialize();
			Export = async (exportContext, capture, token) => await ShowExport(capture, token);
			Text = GreenshotLanguage.SettingsDestinationPicker;
			Designation = PickerDesignation;
		}

		private async Task<INotification> ShowExport(ICapture capture, CancellationToken token = default(CancellationToken))
		{
			using (var exportWindowContext = ExportWindowFactory.CreateExport())
			{
				var exportWindow = exportWindowContext.Value;
				exportWindow.Capture = capture;
				exportWindow.Show();

				foreach (var destination in Destinations.Where(destination => destination.Metadata.Name != PickerDesignation))
				{
					exportWindow.Children.Add(destination.Value);
					await destination.Value.RefreshAsync(null, token);
                }
				INotification exportResult = null;
				do
				{
					exportWindow.SelectedDestination = null;
					await exportWindow.AwaitSelection();
					if (exportWindow.SelectedDestination == null)
					{
						break;
					}
					try
					{
						exportResult = await exportWindow.SelectedDestination.Export(this, capture, token);
						if (token.IsCancellationRequested || exportResult.NotificationType == NotificationTypes.Success)
						{
							return exportResult;
						}
						// TODO: When fail, there should be a message displayed somewhere?!
					}
					catch (Exception ex)
					{
						Log.Error(ex, "Picker export failed");
						//return new Notification
						//{
						//	NotificationType = NotificationTypes.Fail,
						//	Source = PickerDesignation,
						//	SourceType = SourceTypes.Destination,
						//	Text = ex.Message
						//};
					}
				}
				while (exportResult != null && exportResult.NotificationType != NotificationTypes.Cancel);
				exportWindow.Close();
			}
			return new Notification
			{
				NotificationType = NotificationTypes.Cancel,
				Source = PickerDesignation,
				SourceType = SourceTypes.Destination,
				Text = "Cancelled"
			};
		}
	}
}