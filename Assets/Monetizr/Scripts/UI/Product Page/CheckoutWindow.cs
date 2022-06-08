﻿/* Message to future people:
 * I'm sorry about the monolith, please appreciate this from an artistic
   bodge standpoint instead. Thanks. */ 
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Monetizr.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Monetizr.Payments;

namespace Monetizr.UI
{
	[Serializable]
	public class UiAddressFields
	{
		public InputField firstNameField;
		public InputField lastNameField;
		public InputField emailField;
		public InputField address1Field;
		public InputField address2Field;
		public InputField cityField;
		public InputField zipField;
		public Dropdown countryDropdown;
		public Dropdown provinceDropdown;

		public void InitDropdown()
		{
			// Initialize shipping country dropdown
			countryDropdown.options.Clear();
			ShopifyCountries.Collection.ForEach(x =>
			{
				var option = new Dropdown.OptionData {text = x.countryName};
				countryDropdown.options.Add(option);
			});

			countryDropdown.value = countryDropdown.options
				.FindIndex(x => x.text == "United States");
		}

		public void UpdateProvinceDropdown()
		{
			var country = ShopifyCountries.FromName(countryDropdown.options[countryDropdown.value].text);
			provinceDropdown.options.Clear();
			country.regions.ForEach(x =>
			{
				var option = new Dropdown.OptionData {text = x.name};
				provinceDropdown.options.Add(option);
			});
			
			provinceDropdown.value = 0;
			provinceDropdown.RefreshShownValue();
		}
		
		public bool RequiredFieldsFilled()
		{
			if (string.IsNullOrEmpty(address1Field.text))
				return false;
			if (string.IsNullOrEmpty(firstNameField.text))
				return false;
			if (string.IsNullOrEmpty(lastNameField.text))
				return false;
			if (string.IsNullOrEmpty(cityField.text))
				return false;
			if (string.IsNullOrEmpty(zipField.text))
				return false;
			if(emailField != null)
				if (string.IsNullOrEmpty(emailField.text))
					return false;
			return true;
		}
		
		public Dto.ShippingAddress CreateShippingAddress()
		{
			var address = new Dto.ShippingAddress
			{
				address1 = address1Field.text,
				address2 = address2Field.text,
				city = cityField.text,
				country = ShopifyCountries.FromName(countryDropdown.options[countryDropdown.value].text).countryShortCode,
				firstName = firstNameField.text,
				lastName = lastNameField.text,
				zip = zipField.text
			};
			if (address.country.Equals("US"))
			{
				address.province = ShopifyCountries.FromName(
					ShopifyCountries.FromName(countryDropdown.options[countryDropdown.value].text),
					provinceDropdown.options[provinceDropdown.value].text).shortCode;
			}
			else
			{
				address.province = provinceDropdown.options[provinceDropdown.value].text;
			}
			return address;
		}
	}
	
	public class CheckoutWindow : MonoBehaviour
	{
		public bool Working { get; private set; }

		public bool IsOpen
		{
			get { return animator.GetBool(Opened); }
		}

		private Checkout _currentCheckout = null;
		private Dto.ShippingAddress _currentAddress = null;
		private Price _currentTotalPrice = null;
		private Payment _currentPayment = null;
		public ProductPageScript pp;
		public ProductPageLayout layout;
		public Animator animator;
		public Animator loadingSpinnerAnimator;
		public Text loadingText;
		public GameObject loadingContinueButton;
		public GameObject loadingCancelButton;

		private Action loadingContinueAction;
		private string loadingContinueUrl;

		public CanvasGroup windowGroup;
		public CanvasGroup shippingPage;
		public CanvasGroup confirmationPage;
		public CanvasGroup checkoutUpdatePage;
		public CanvasGroup resultPage;

		public Selectable shippingPageNavSelection;
		public Selectable confirmationPageNavSelection;
		public Selectable checkoutUpdatePageNavSelection;
		public Selectable resultPageNavSelection;

		public enum Page
		{
			NoPage,
			ShippingPage,
			ConfirmationPage,
			CheckoutUpdatePage,
			ResultPage,
			SomePage
		}

		public UiAddressFields shipAddressFields;
		public GameObject policyLinks;
		private static readonly int Opened = Animator.StringToHash("Opened");

		public VerticalLayoutGroup confirmationPageLayout;
		public ShippingOptionManager shippingOptionManager;
		public Text confirmProductText;
		public Text confirmVariantText;
		public Text deliveryNameText;
		public Text deliveryAddressText;
		public Text totalPriceText;
		public Toggle billingAddressToggle;
		public GameObject billingAddressPanel;
		public UiAddressFields billingAddressFields;
		
		public Text cuConfirmProductText;
		public Text cuConfirmVariantText;
		public Text cuDeliveryHeaderText;
		public Text cuDeliveryNameText;
		public Text cuDeliveryAddressText;
		public Text cuSubtotalPriceText;
		public Text cuTaxPriceText;
		public Text cuShippingPriceText;
		public Text cuTotalPriceText;
		public Text cuBuyButtonText;

		public Text resultPageHeader;
		public Text resultPageText;

		public Animator errorWindowAnimator;
		public VerticalLayoutGroup errorWindowLayout;
		public Text errorWindowText;
		public Button errorWindowCloseButton;

		private Payment.PaymentResult _lastPaymentResult = Payment.PaymentResult.Successful;

		private const string PrivacyPolicyUrl = "https://www.themonetizr.com/privacy-policy";

		public void OpenPrivacyPolicy()
		{
			MonetizrClient.Instance.OpenURL(PrivacyPolicyUrl);
		}
		
		private const string TermsOfServiceUrl = "https://www.themonetizr.com/terms-of-service";

		public void OpenTermsOfService()
		{
			MonetizrClient.Instance.OpenURL(TermsOfServiceUrl);
		}
		
		public CheckoutWindow()
		{
			Working = false;
		}

		public void Init()
		{
			shipAddressFields.InitDropdown();
			billingAddressFields.InitDropdown();
			policyLinks.SetActive(MonetizrClient.Instance.PolicyLinksEnabled);
		}

		public void UpdateShippingAddressProvince()
		{
			shipAddressFields.UpdateProvinceDropdown();
		}

		public void UpdateBillingAddressProvince()
		{
			billingAddressFields.UpdateProvinceDropdown();
		}

		public void UpdateBillingAddressVisibility()
		{
			billingAddressPanel.SetActive(billingAddressToggle.isOn);
		}

		public void UpdateLoadingText(string text)
		{
			loadingText.text = text ?? "";
		}

		public void CancelPayment()
		{
			if (_currentPayment != null)
			{
				_currentPayment.CancelPayment();
				loadingCancelButton.SetActive(false);
			}
		}

		public void SetupLoadingCancelButton()
		{
			loadingCancelButton.SetActive(true);
		}

		public void SetupLoadingContinueButton(string url, Action afterContinue)
		{
			loadingContinueAction = afterContinue;
			loadingContinueUrl = url;
			loadingContinueButton.SetActive(true);
		}
		
		// This is required for WebGL because the plugin for opening links in new tabs
		// works only from click events (and only from OnPointerDown)
		public void PressLoadingContinueButton()
		{
			MonetizrClient.Instance.OpenURL(loadingContinueUrl);
			loadingContinueAction();
			loadingContinueButton.SetActive(false);
		}

		private void Update()
		{
			if (Input.GetKeyDown(KeyCode.Tab) && layout.layoutKind == ProductPageLayout.Layout.BigScreen)
			{
				// This is spaghetti and could be written much better
				// I'm sorry
				if (IsOpen)
				{
					var s = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
					var dd = EventSystem.current.currentSelectedGameObject.GetComponent<Dropdown>();
					if (s != null)
					{
						var next = s.navigation.selectOnRight;
						if (next.GetComponent<InputField>() || next.GetComponent<Dropdown>())
						{
							EventSystem.current.SetSelectedGameObject(next.gameObject);
							var next_if = next.GetComponent<InputField>();
							if (next_if != null)
								next_if.OnPointerClick(new PointerEventData(EventSystem.current));
							var next_dd = next.GetComponent<Dropdown>();
							if (next_dd != null)
								next_dd.GetComponent<EventTrigger>().OnPointerClick(new PointerEventData(EventSystem.current));
						}
						else
						{
							EventSystem.current.SetSelectedGameObject(next.gameObject);
						}
					}
					else if(dd != null)
					{
						var next = dd.navigation.selectOnRight;
						if (next.GetComponent<InputField>() || next.GetComponent<Dropdown>())
						{
							EventSystem.current.SetSelectedGameObject(next.gameObject);
							var next_if = next.GetComponent<InputField>();
							if (next_if != null)
								next_if.OnPointerClick(new PointerEventData(EventSystem.current));
							var next_dd = next.GetComponent<Dropdown>();
							if (next_dd != null)
								next_dd.GetComponent<EventTrigger>().OnPointerClick(new PointerEventData(EventSystem.current));
						}
						else
						{
							EventSystem.current.SetSelectedGameObject(next.gameObject);
						}
					}
				}
			}
		}

		private void OpenPage(Page page)
		{
			SetPageState(shippingPage, page == Page.ShippingPage);
			SetPageState(confirmationPage, page == Page.ConfirmationPage);
			SetPageState(resultPage, page == Page.ResultPage);
			SetPageState(checkoutUpdatePage, page == Page.CheckoutUpdatePage);
			layout.UpdateButtons();
			if (layout.layoutKind == ProductPageLayout.Layout.BigScreen)
			{
				switch (page)
				{
					case Page.NoPage:
						break;
					case Page.ShippingPage:
						pp.ui.SelectWhenInteractable(shippingPageNavSelection);
						break;
					case Page.ConfirmationPage:
						pp.ui.SelectWhenInteractable(confirmationPageNavSelection);
						break;
					case Page.ResultPage:
						pp.ui.SelectWhenInteractable(resultPageNavSelection);
						break;
					case Page.CheckoutUpdatePage:
						pp.ui.SelectWhenInteractable(checkoutUpdatePageNavSelection);
						break;
					case Page.SomePage:
						break;
					default:
						throw new ArgumentOutOfRangeException("page", page, null);
				}
			}
		}

		private void SetLoading(bool state)
		{
			UpdateLoadingText(null);
			loadingContinueButton.SetActive(false);
			loadingCancelButton.SetActive(false);
			loadingSpinnerAnimator.SetBool(Opened, state);
			layout.UpdateButtons();
		}

		public Page CurrentPage()
		{
			if (!IsOpen)
				return Page.NoPage;
			if (loadingSpinnerAnimator.GetBool(Opened))
				return Page.SomePage;
			if (shippingPage.alpha > 0.01)
				return Page.ShippingPage;
			if (confirmationPage.alpha > 0.01)
				return Page.ConfirmationPage;
			if (checkoutUpdatePage.alpha > 0.01)
				return Page.CheckoutUpdatePage;
			if (resultPage.alpha > 0.01)
				return Page.ResultPage;
			return Page.SomePage; //Not open already checked at first line
		}
		
		private void SetPageState(CanvasGroup page, bool state)
		{
			page.alpha = state ? 1 : 0;
			page.interactable = state;
			page.blocksRaycasts = state;
		}

		public void CloseWindow()
		{
			if (Working) return;
			if (!IsOpen) return;
			animator.SetBool(Opened, false);
			SetErrorWindowState(false);
			if (layout.layoutKind == ProductPageLayout.Layout.BigScreen)
			{
				pp.ui.SelectWhenInteractable(((ProductPageBigScreen) layout).firstSelection);
			}
			layout.UpdateButtons();
		}

		public void FinishButtonInResultPage()
		{
			if (_lastPaymentResult == Payment.PaymentResult.Successful)
			{
				CloseWindow();
				return;
			}
			OpenShipping();
		}
		
		public void OpenShipping()
		{
			SetLoading(false);
			animator.SetBool(Opened, true);
			pp.ui.SelectWhenInteractable(shipAddressFields.firstNameField);
			OpenPage(Page.ShippingPage);
		}

		public void ConfirmShipping()
		{
			_currentCheckout = null;
			if (!shipAddressFields.RequiredFieldsFilled())
			{
				SetErrorWindowState(true);
				var e = new Checkout.Error("Please fill all required fields", "aaa");
				var l = new List<Checkout.Error> {e};
				WriteErrorWindow(l);
				return;
			}
			var address = shipAddressFields.CreateShippingAddress();
			SetLoading(true);
			shippingPage.interactable = false;
			Working = true;
			pp.product.CreateCheckout(pp.CurrentVariant, address, create =>
			{
				OpenConfirmation(create);
				Working = false;
			});
		}

		public void ConfirmConfirmation()
		{
			if (!billingAddressFields.RequiredFieldsFilled() && billingAddressToggle.isOn)
			{
				SetErrorWindowState(true);
				var e = new Checkout.Error("Please fill all required fields", "aaa");
				var l = new List<Checkout.Error> {e};
				WriteErrorWindow(l);
				return;
			}
			var billingAddress = billingAddressToggle.isOn ? billingAddressFields.CreateShippingAddress() : null;
			SetLoading(true);
			Working = true;
			confirmationPage.interactable = false;
			_currentCheckout.UpdateCheckout(billingAddress, create =>
			{
				if (create)
				{
					OpenCheckoutUpdate();
				}
				else
				{
					SetErrorWindowState(true);
					WriteErrorWindow(_currentCheckout.Errors);
					OpenShipping();
				}
				Working = false;
			});
		}

		public void SetDefaultShipping()
		{
			shippingOptionManager.SetFirstEnabled();
		}

		public void UpdatePricing()
		{
			var selected = shippingOptionManager.SelectedOption();
			if (selected != null)
			{
				_currentTotalPrice = new Price();
				_currentTotalPrice.CurrencyCode = selected.Price.CurrencyCode;
				_currentTotalPrice.CurrencySymbol = selected.Price.CurrencySymbol;
				decimal total = selected.Price.Amount;
				total += _currentCheckout.Total.Amount;
				// Only allowing string assignments is a weakness of the
				// Price object, but it's a problem we can live with and doesn't pose
				// as a huge performance issue.
				_currentTotalPrice.AmountString = total.ToString(CultureInfo.InvariantCulture);

				//shippingPriceText.text = selected.Price.FormattedPrice;
				if (_currentCheckout.Product.Claimable)
				{
					if (_currentTotalPrice.Amount == 0)
					{
						totalPriceText.text = _currentCheckout.Variant.Price.FormattedPrice;
					}
					else
					{
						totalPriceText.text = _currentCheckout.Variant.Price.FormattedPrice +
						                      " + " + _currentTotalPrice.FormattedPrice;
					}
					
				}
				else
				{
					totalPriceText.text = _currentTotalPrice.FormattedPrice;
				}

				_currentCheckout.SetShippingLine(selected);
			}
			else
			{
				_currentTotalPrice = null;
			}
		}

		private void ForceUpdateConfirmationLayout()
		{
			// Content Size Fitters are nasty things that never work as you would
			// expect them to if you build your UI automatically :<
			
			LayoutRebuilder.ForceRebuildLayoutImmediate(pp.GetComponent<RectTransform>());
			//Canvas.ForceUpdateCanvases();
			confirmationPageLayout.enabled = false;
			confirmationPageLayout.enabled = true;
			LayoutRebuilder.ForceRebuildLayoutImmediate(pp.GetComponent<RectTransform>());
			//Canvas.ForceUpdateCanvases();
		}

		public void OpenConfirmation(Checkout checkout)
		{
			if (checkout == null)
			{
				SetErrorWindowState(true);
				var e = new Checkout.Error("Internal server error", "aaa");
				var l = new List<Checkout.Error> {e};
				WriteErrorWindow(l);
				OpenShipping();
				return;
			}

			if (checkout.Errors.Count > 0)
			{
				SetErrorWindowState(true);
				WriteErrorWindow(checkout.Errors);
				OpenShipping();
				return;
			}
			
			_currentCheckout = checkout;
			_currentCheckout.SetEmail(shipAddressFields.emailField.text);
			_currentAddress = _currentCheckout.ShippingAddress;
			shippingOptionManager.UpdateOptions(checkout.ShippingOptions);
			confirmProductText.text = checkout.Items.First().Title;
			confirmVariantText.text = "1x " + _currentCheckout.Variant;
			deliveryNameText.text = _currentCheckout.ShippingAddress.firstName + " "
             + _currentCheckout.ShippingAddress.lastName
             + "\n" + _currentCheckout.RecipientEmail;
			deliveryAddressText.text = _currentAddress.address1 + '\n'
                                        + (string.IsNullOrEmpty(_currentAddress.address2)
                                            ? ""
                                            : (_currentAddress.address2 + '\n'))
                                        + _currentAddress.city +
                                        (string.IsNullOrEmpty(_currentAddress.province)
                                            ? ""
                                            : (", " + _currentAddress.province)) + '\n'
                                        + _currentAddress.zip + '\n'
                                        + ShopifyCountries.FromShortCode(_currentAddress.country).countryName;

			/*if (checkout.Product.Claimable)
			{
				subtotalPriceText.text = checkout.Variant.Price.FormattedPrice;
			}
			else
			{
				subtotalPriceText.text = _currentCheckout.Subtotal.FormattedPrice;
			}
			taxPriceText.text = _currentCheckout.Tax.FormattedPrice;
			shippingPriceText.text = "Not set!";*/
			totalPriceText.text = "Not set!";
			SetDefaultShipping();
			SetLoading(false);
			OpenPage(Page.ConfirmationPage);
			ForceUpdateConfirmationLayout();
		}
		
		
		
		public void OpenCheckoutUpdate()
		{
			if (_currentCheckout.Errors.Count > 0)
			{
				SetErrorWindowState(true);
				WriteErrorWindow(_currentCheckout.Errors);
				OpenShipping();
				return;
			}
			
			cuConfirmProductText.text = _currentCheckout.Items.First().Title;
			cuConfirmVariantText.text = "1x " + _currentCheckout.Variant;
			cuDeliveryHeaderText.text = "Shipping: " + _currentCheckout.SelectedShippingRate.Title;
			cuDeliveryNameText.text = _currentCheckout.ShippingAddress.firstName + " "
			    + _currentCheckout.ShippingAddress.lastName
				+ "\n" + _currentCheckout.RecipientEmail;
			cuDeliveryAddressText.text = _currentAddress.address1 + '\n'
			                                                    + (string.IsNullOrEmpty(_currentAddress.address2)
				                                                    ? ""
				                                                    : (_currentAddress.address2 + '\n'))
			                                                    + _currentAddress.city +
			                                                    (string.IsNullOrEmpty(_currentAddress.province)
				                                                    ? ""
				                                                    : (", " + _currentAddress.province)) + '\n'
			                                                    + _currentAddress.zip + '\n'
			                                                    + ShopifyCountries.FromShortCode(_currentAddress.country).countryName;

			if (_currentCheckout.Product.Claimable)
			{
				cuSubtotalPriceText.text = _currentCheckout.Variant.Price.FormattedPrice;
			}
			else
			{
				cuSubtotalPriceText.text = _currentCheckout.Subtotal.FormattedPrice;
			}
			cuTaxPriceText.text = _currentCheckout.Tax.FormattedPrice;
			cuShippingPriceText.text = _currentCheckout.SelectedShippingRate.Price.FormattedPrice;
			if (_currentCheckout.Product.Claimable)
			{
				if (_currentCheckout.Total.Amount == 0)
				{
					cuTotalPriceText.text = _currentCheckout.Variant.Price.FormattedPrice;
				}
				else
				{
					cuTotalPriceText.text = _currentCheckout.Variant.Price.FormattedPrice +
					                      " + " + _currentCheckout.Total.FormattedPrice;
				}
					
			}
			else
			{
				cuTotalPriceText.text = _currentCheckout.Total.FormattedPrice;
			}

			cuBuyButtonText.text = _currentCheckout.Product.Claimable ? "Claim" : "Purchase";
			SetLoading(false);
			OpenPage(Page.CheckoutUpdatePage);
		}

		public void ConfirmCheckout()
		{
			SetLoading(true);
			confirmationPage.interactable = false;
			_currentPayment = new Payment(_currentCheckout, this);
			Working = true;
			_currentPayment.Initiate();
		}

		public void FinishCheckout(Payment.PaymentResult result, string msg = null)
		{
			SetLoading(false);
			OpenPage(Page.ResultPage);
			Working = false;
			_lastPaymentResult = result;
			var message = msg ?? "";
			if (msg == null)
			{
				switch (result)
				{
					case Payment.PaymentResult.Successful:
						message = "Thank you for your order!";
						break;
					case Payment.PaymentResult.FailedPayment:
						message = "An error occurred while processing the payment.";
						break;
					case Payment.PaymentResult.Unimplemented:
						message = "Payments currently unimplemented.";
						break;
					default:
						throw new ArgumentOutOfRangeException("result", result, null);
				}
			}

			resultPageHeader.text = result == Payment.PaymentResult.Successful ? "Awesome!" : "Oops!";
			if (result == Payment.PaymentResult.Successful && msg == null)
			{
				message += " Your " + _currentCheckout.Items.First().Title + " is on it's way!";
			}
			resultPageText.text = message;

			if (result == Payment.PaymentResult.Successful)
			{
				if (MonetizrClient.Instance.MonetizrOrderConfirmed != null)
					MonetizrClient.Instance.MonetizrOrderConfirmed(_currentCheckout.Product);
			}
		}

		public void SetErrorWindowState(bool state)
		{
			errorWindowAnimator.SetBool(Opened, state);
			if (layout.layoutKind == ProductPageLayout.Layout.BigScreen)
			{
				pp.ui.SelectWhenInteractable(errorWindowCloseButton);
			}
		}

		public void WriteErrorWindow(List<Checkout.Error> errors)
		{
			string s = "";
			errors.ForEach(x =>
			{
				s += x.Message;
				s += '\n';
			});
			// Remove the last newline
			s = s.Substring(0, s.Length - 1);
			errorWindowText.text = s;
			Canvas.ForceUpdateCanvases();
			errorWindowLayout.enabled = false;
			errorWindowLayout.enabled = true;
		}
	}
}
