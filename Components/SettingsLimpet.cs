using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace OS_Chronopost2.Components
{
    public class SettingsLimpet
    {
        private NBrightBuyController _objCtrl;
        public SettingsLimpet(CartData cart)
        {
            // cart data 
            TotalWeight = cart.PurchaseInfo.GetXmlPropertyDouble("genxml/totalweight");
            SubTotal = cart.PurchaseInfo.GetXmlPropertyDouble("genxml/subtotal");
            ArrivalPostCode = Utils.StripAccents(cart.PurchaseInfo.GetXmlProperty("genxml/shipaddress/genxml/textbox/postalcode"));
            if (ArrivalPostCode == "") ArrivalPostCode = Utils.StripAccents(cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/textbox/postalcode"));
            ArrivalCountryCode = Utils.StripAccents(cart.PurchaseInfo.GetXmlProperty("genxml/shipaddress/genxml/dropdownlist/country"));
            if (ArrivalCountryCode == "") ArrivalCountryCode = Utils.StripAccents(cart.PurchaseInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country"));
            ShippingProvider = cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/radiobuttonlist/shippingprovider");
            ShippingOption = cart.PurchaseInfo.GetXmlProperty("genxml/extrainfo/genxml/radiobuttonlist/rblshippingoptions");

            // Load Settings
            _objCtrl = new NBrightBuyController();
            Info = _objCtrl.GetPluginSinglePageData("chronopost2", "SHIPPING", Utils.GetCurrentCulture());

        }

        public void Save(HttpContext context)
        {
            _objCtrl.SavePluginSinglePageData(context);
        }

        public NBrightInfo Info { set; get;}
        public string AccountNumber { get { return Info.GetXmlProperty("genxml/textbox/chronopostaccountnumber"); } }
        public string Password { get { return Info.GetXmlProperty("genxml/textbox/chronopostpassword"); } }
        public Double FreeShippingLimit { get { return Info.GetXmlPropertyDouble("genxml/textbox/freeshippinglimit"); } }
        public Double PickUpCost { get { return Info.GetXmlPropertyDouble("genxml/textbox/pickupcost"); } }
        public string DistributionCountryCode { get { return Info.GetXmlProperty("genxml/textbox/distributioncountrycode"); } }
        public string DistributionPostCode { get { return Info.GetXmlProperty("genxml/textbox/distributionpostcode"); } }
        public int LeadDays { get { return Info.GetXmlPropertyInt("genxml/textbox/chronopostleaddays"); } }
        public string PrintMode { get { return Info.GetXmlProperty("genxml/dropdownlist/printmode"); } }
        public string ProductCode { get { return Info.GetXmlProperty("genxml/textbox/chronopostproductcode"); } }
        public string ProductType { get { return Info.GetXmlProperty("genxml/dropdownlist/chronoposttype"); } }        

        // CART
        public string ShippingProvider { set; get; }
        public string ArrivalPostCode { set; get; }
        public string ArrivalCountryCode { set; get; }
        public string ShippingOption { set; get; }
        public double TotalWeight { set; get; }
        public double SubTotal { set; get; }
    }
}
