using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using System.Xml;
using DotNetNuke.Entities.Portals;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;
using Nevoweb.DNN.NBrightBuy.Components.Interfaces;
using System.Globalization;
using Nevoweb.OS_Chronopost2.Components;

namespace Nevoweb.OS_Chronopost2
{
    public class Provider : ShippingInterface 
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cartInfo"></param>
        /// <returns></returns>
        public override NBrightInfo CalculateShipping(NBrightInfo cartInfo)
        {
            var chronoData = new ChronopostLimpet(cartInfo);
            chronoData.UpdateShippingCost();
            return chronoData.CartInfo;
        }

        public override string Shippingkey { get; set; }

        public override string Name()
        {
            return "Chronopost";
        }

        public override string GetTemplate(NBrightInfo cartInfo)
        {
            var chronoData = new ChronopostLimpet(cartInfo);
            return NBrightBuyUtils.RazorTemplRender("carttemplate.cshtml", 0, "", chronoData, "/DesktopModules/NBright/OS_Chronopost2", "config", Utils.GetCurrentCulture(), StoreSettings.Current.Settings());
        }

        public override string GetDeliveryLabelUrl(NBrightInfo cartInfo)
        {
            var chronoData = new ChronopostLimpet(cartInfo);
            return "LABEL TEST";
        }
        public override bool IsValid(NBrightInfo cartInfo)
        {
            // check if this provider is valid for the counrty in the checkout
            var shipoption = cartInfo.GetXmlProperty("genxml/extrainfo/genxml/radiobuttonlist/rblshippingoptions");
            var countrycode = "";
            switch (shipoption)
            {
                case "1":
                    countrycode = cartInfo.GetXmlProperty("genxml/billaddress/genxml/dropdownlist/country");
                    break;
                case "2":
                    countrycode = cartInfo.GetXmlProperty("genxml/shipaddress/genxml/dropdownlist/country");
                    break;
            }

            var isValid = true;
            var modCtrl = new NBrightBuyController();
            var info = modCtrl.GetByGuidKey(PortalSettings.Current.PortalId, -1, "SHIPPING", Shippingkey);
            if (info != null)
            {
                var validlist = "," + info.GetXmlProperty("genxml/textbox/validcountrycodes") + ",";
                var notvalidlist = "," + info.GetXmlProperty("genxml/textbox/notvalidcountrycodes") + ",";
                if (validlist.Trim(',') != "")
                {
                    isValid = false;
                    if (validlist.Contains("," + countrycode + ",")) isValid = true;
                }
                if (notvalidlist.Trim(',') != "" && notvalidlist.Contains("," + countrycode + ",")) isValid = false;                
            }

            return isValid;
        }

    }


}
