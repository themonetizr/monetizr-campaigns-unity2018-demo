﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Monetizr.UI
{
    public class VariantsDropdown : MonoBehaviour
    {
        public List<string> Options;
        public string OptionName;
        private List<VariantsDropdown> _allDropdowns;

        public List<VariantsDropdown> AllDropdowns
        {
            get { return _allDropdowns; }
        }

        public Text OptionText;
        public Text OptionNameText;
        private string _selectedOption;
        public string SelectedOption
        {
            get { return _selectedOption; }
            set
            {
                _selectedOption = value;
                if (Alternate != null) Alternate.ForEach(x => x.OptionText.text = _selectedOption);
            }
        }
        public SelectionManager SelectionManager;

        public List<AlternateVariantsDropdown> Alternate;
        public VariantsDropdown previous;

        private AlternateVariantsDropdown _bigScreenAlternate;

        public AlternateVariantsDropdown BigScreenAlternate
        {
            get
            {
                if (_bigScreenAlternate == null)
                {
                    _bigScreenAlternate = Alternate.First(x => x.selectionManagerBigScreen != null);
                }

                return _bigScreenAlternate;
            }
        }

        public void Init(List<string> options, string optionName, List<VariantsDropdown> allDropdowns)
        {
            Options = options;
            OptionName = optionName;
            _allDropdowns = allDropdowns;
            SelectedOption = options.FirstOrDefault() ?? null;
            OptionText.text = SelectedOption;
            if (optionName != null) OptionNameText.text = optionName.ToUpper();
            if (Alternate != null) Alternate.ForEach(x => x.OptionNameText.text = OptionNameText.text);
            Canvas.ForceUpdateCanvases(); // Resize the selector buttons to proper size
        }

        public void SelectValue()
        {
            SelectionManager.ShowSelection();
            SelectionManager.InitOptions(Options, OptionName, this, _allDropdowns);
        }

        public string GetBreadcrumbs(string fromPrevious)
        {
            if (previous == null)
            {
                //We don't care about the breadcrums from the current item
                if (string.IsNullOrEmpty(fromPrevious))
                    return "";
                else
                    return fromPrevious;
            }
            
            if(string.IsNullOrEmpty(fromPrevious))
                return previous.GetBreadcrumbs(previous.SelectedOption);
            else
                return previous.GetBreadcrumbs(previous.SelectedOption + " + " + fromPrevious);
        }

        public Dictionary<string, string> GetVariantBreadcrumbs(Dictionary<string, string> fromPrevious)
        {
            if (previous == null)
            {
                //We don't care about the breadcrums from the current item
                if (fromPrevious == null)
                    return new Dictionary<string, string>();
                else
                    return fromPrevious;
            }

            var dict = fromPrevious;
            if (fromPrevious == null)
            {
                dict = new Dictionary<string, string>();
            }

            dict[previous.OptionName] = previous.SelectedOption;

            return previous.GetVariantBreadcrumbs(dict);
        }
    }
}