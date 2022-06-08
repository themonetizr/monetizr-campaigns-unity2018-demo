﻿using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Monetizr.Testing
{
	public class TestingSceneScript : MonoBehaviour
	{
		public InputField tokenField;
		public InputField productField;

		public string testingToken;
		public string sampleShirtTag;

		private void OrderCallback(Product p)
        {
			MonetizrClient.Instance.ShowError("ORDER CALLBACK " + p.Title);
        }

        private void Start()
        {
			MonetizrClient.Instance.MonetizrOrderConfirmed += OrderCallback;
        }

        private void OnDestroy()
        {
			MonetizrClient.Instance.MonetizrOrderConfirmed -= OrderCallback;
		}

        // It's not nice to mess with others build settings so we're just going to hope that the missing scene error gets noticed.
        public void SwitchScene(string scene)
		{
			MonetizrClient.Instance.SetAccessTokenOverride(tokenField != null ? tokenField.text : null);
			SceneManager.LoadScene(scene);
		}

		public void ShowSampleShirt()
		{
			MonetizrClient.Instance.SetAccessTokenOverride(testingToken);
			MonetizrClient.Instance.ShowProductForTag(sampleShirtTag);
		}

		public void SetTestingToken()
		{
			tokenField.text = testingToken;
		}
		
		public void ShowButton()
		{
			MonetizrClient.Instance.SetAccessTokenOverride(tokenField.text);
			MonetizrClient.Instance.ShowProductForTag(productField.text);
		}
		
		public void ShowLockedButton()
		{
			MonetizrClient.Instance.SetAccessTokenOverride(tokenField.text);
			MonetizrClient.Instance.ShowProductForTag(productField.text, true);
		}
	}
}
