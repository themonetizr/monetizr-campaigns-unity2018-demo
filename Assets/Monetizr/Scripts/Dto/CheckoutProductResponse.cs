using System;
using System.Collections.Generic;

namespace Monetizr.Dto
{
    [Serializable]
    public class CheckoutProductResponse
    {
        [Serializable]
        public class CheckoutUserError
        {
            public string code;
            public List<string> field;
            public string message;
        }
        
        [Serializable]
        public class Checkout
        {
            public string id;
            public string webUrl;
            public CompareAtPriceV2 subtotalPriceV2;
            public bool taxExempt;
            public bool taxesIncluded;
            public CompareAtPriceV2 totalPriceV2;
            public CompareAtPriceV2 totalTaxV2;
            public bool requiresShipping;
            public AvailableShippingRates availableShippingRates;
            public ShippingLine shippingLine;
            public LineItems lineItems;
        }
        
        [Serializable]
        public class AvailableShippingRates
        {
            public bool ready;

            [Serializable]
            public class ShippingRate
            {
                public string handle;
                public string title;
                public CompareAtPriceV2 priceV2;
            }

            public List<ShippingRate> shippingRates;
        }
        
        [Serializable]
        public class ShippingLine
        {
            public string handle;
            public string title;
            public CompareAtPriceV2 priceV2;
        }

        [Serializable]
        public class CheckoutCreate
        {
            public List<CheckoutUserError> checkoutUserErrors;
            public Checkout checkout;
        }
        
        [Serializable]
        public class Data
        {
            public CheckoutCreate checkoutCreate;
        }

        public Data data;
    }
    
    [Serializable]
    public class UpdateCheckoutResponse
    {
        [Serializable]
        public class UpdateShippingLine
        {
            public List<CheckoutProductResponse.CheckoutUserError> checkoutUserErrors;
            public CheckoutProductResponse.Checkout checkout;
        }
        
        [Serializable]
        public class UpdateShippingAddress
        {
            public List<CheckoutProductResponse.CheckoutUserError> checkoutUserErrors;
        }
        
        [Serializable]
        public class Data
        {
            public UpdateShippingAddress updateShippingAddress;
            public UpdateShippingLine updateShippingLine;
        }

        public Data data;
    }
}