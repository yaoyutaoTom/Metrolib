﻿using System.Windows;

namespace Metrolib.Controls
{
	/// <summary>
	///     An extended toggle button that offers an IsInverted property to control how the content shall be displayed.
	/// </summary>
	public class FlatToggleButton
		: ToggleButtonBase
	{
		static FlatToggleButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatToggleButton),
													 new FrameworkPropertyMetadata(typeof(FlatToggleButton)));
		}

	}
}