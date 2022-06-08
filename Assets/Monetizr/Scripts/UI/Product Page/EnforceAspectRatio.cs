﻿using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Monetizr.UI
{
	public class EnforceAspectRatio : MonoBehaviour
	{
		public MonetizrUI ui;
		private RectTransform _rect;
		public float aspectFromHeight = 1.777778F;

		private void Start()
		{
			if (!ui.BigScreen)
			{
				enabled = false;
				return;
			}
			_rect = GetComponent<RectTransform>();
			ui.ScreenResolutionChanged += ScreenResolutionChanged;
			ScreenResolutionChanged();
		}

		private void ScreenResolutionChanged()
		{
			Canvas.ForceUpdateCanvases();
			float actualAspect = Screen.width / (float) Screen.height;
			float desiredWidth = _rect.rect.height * aspectFromHeight;
			float sideOffset = (desiredWidth - _rect.rect.height * actualAspect) / 2f;
				
			_rect.offsetMin = new Vector2(-sideOffset, 0);
			_rect.offsetMax = new Vector2(sideOffset, 0);
			if (actualAspect < aspectFromHeight-0.0001f)
			{
				// Shrink expansion to fit
				// It is assumed that the base scale factor for big screen is 0.7
				float newScaleFactor = ((_rect.rect.height * actualAspect) / _rect.rect.width) * MonetizrClient.Instance.Settings.bigScreenSettings.overlayScale;
				ui.SetScale(newScaleFactor);
			}
			else
			{
				ui.SetScale(MonetizrClient.Instance.Settings.bigScreenSettings.overlayScale);
			}
		}

		private void OnDestroy()
		{
			try
			{
				ui.ScreenResolutionChanged -= ScreenResolutionChanged;
			}
			catch
			{
				// ignored
			}
		}
	}
}
